using System.Collections.ObjectModel;
using CmlLib.Core.Auth;
using CmlLib.Core.Auth.Microsoft;
using CmlLib.Core.Auth.Microsoft.Sessions;
using Emerald.CoreX.Helpers;
using Emerald.CoreX.Models;
using Emerald.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using XboxAuthNet.Game.Accounts;
using XboxAuthNet.Game.Msal;
using XboxAuthNet.Game.Msal.OAuth;

namespace Emerald.CoreX.Services;

public sealed class AccountService : IAccountService
{
    // ──────────────────────────────────────────────
    // Policy
    // ──────────────────────────────────────────────

    public bool RequireMicrosoftAccountForOfflineAccounts => true;

    // ──────────────────────────────────────────────
    // Dependencies
    // ──────────────────────────────────────────────

    private readonly ILogger<AccountService> _logger;
    private readonly IBaseSettingsService _settingsService;

    // ──────────────────────────────────────────────
    // State
    // ──────────────────────────────────────────────

    // Protects all mutations of _accounts and _selectedAccountId.
    private readonly SemaphoreSlim _gate = new(1, 1);

    // Serialises LoadAllAccountsAsync calls independently so they can be
    // awaited without blocking every other mutating operation.
    private readonly SemaphoreSlim _loadGate = new(1, 1);

    private readonly ObservableCollection<EAccount> _accounts = new();

    // Init state — reset to null when init fails so callers can retry.
    private readonly object _initLock = new();
    private Task? _initializationTask;

    private JELoginHandler? _loginHandler;
    private IPublicClientApplication? _msalApp;
    private string? _selectedAccountId;

    // ──────────────────────────────────────────────
    // Constructor
    // ──────────────────────────────────────────────

    public AccountService(ILogger<AccountService> logger, IBaseSettingsService settingsService)
    {
        _logger = logger;
        _settingsService = settingsService;
        _selectedAccountId = _settingsService.Get<string?>(SettingsKeys.SelectedMinecraftAccount, null);
    }

    // ──────────────────────────────────────────────
    // IAccountService – Public surface
    // ──────────────────────────────────────────────

    public ObservableCollection<EAccount> Accounts => _accounts;

    public Task InitializeAsync(string clientId)
    {
        lock (_initLock)
        {
            // Start a fresh task only when there is none, or the previous one faulted.
            if (_initializationTask is null || _initializationTask.IsFaulted)
            {
                _initializationTask = InitializeCoreAsync(clientId);
            }

            return _initializationTask;
        }
    }

    public async Task LoadAllAccountsAsync()
    {
        await EnsureInitializedAsync().ConfigureAwait(false);
        await _loadGate.WaitAsync().ConfigureAwait(false);

        try
        {
            _logger.LogInformation("Loading accounts.");

            if (_loginHandler is null)
                throw new InvalidOperationException("LoginHandler was not initialized.");

            var storedAccounts = _settingsService.Get(SettingsKeys.MinecraftAccounts, new List<EAccount>());
            var onlineAccounts  = _loginHandler.AccountManager
                .GetAccounts()
                .OfType<JEGameAccount>()
                .ToArray();

            _logger.LogInformation(
                "Found {StoredCount} stored accounts and {OnlineCount} online accounts.",
                storedAccounts.Count, onlineAccounts.Length);

            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                _accounts.Clear();

                // Add offline accounts from local storage.
                foreach (var offline in storedAccounts.Where(a => a.Type == AccountType.Offline))
                    _accounts.Add(EnsureUniqueId(offline));

                // Merge Microsoft accounts from the login handler.
                // Use a set keyed on UniqueId to avoid O(n²) lookups.
                var existingIds = new HashSet<string>(
                    _accounts.Select(a => a.UniqueId),
                    StringComparer.Ordinal);

                foreach (var online in onlineAccounts)
                {
                    if (existingIds.Contains(online.Identifier))
                        continue;

                    _accounts.Add(new EAccount(
                        online.Profile?.Username ?? "Microsoft Account",
                        AccountType.Microsoft,
                        online.Profile?.UUID ?? string.Empty,
                        online.Identifier)
                    {
                        LastUsed = online.LastAccess
                    });

                    existingIds.Add(online.Identifier);
                }

                RestoreSelectedAccount();
                EnforceOfflineSelectionPolicy(persist: false);
            }
            finally
            {
                _gate.Release();
            }

            PersistAccounts();
            _logger.LogInformation("Loaded {TotalCount} accounts.", _accounts.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load accounts.");
            throw;
        }
        finally
        {
            _loadGate.Release();
        }
    }

    public void CreateOfflineAccount(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username cannot be empty.", nameof(username));

        EnsureOfflineAccountPolicyMet("Creating offline accounts requires at least one Microsoft account.");

        _gate.Wait();
        try
        {
            if (_accounts.Any(a => a.Name.Equals(username, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"An account named '{username}' already exists.");

            var account = new EAccount(username, AccountType.Offline);
            _accounts.Add(account);

            // Auto-select first account.
            if (GetSelectedAccount() is null)
                ApplySelectedAccount(account.UniqueId, persist: false);
        }
        finally
        {
            _gate.Release();
        }

        PersistAccounts();
        _logger.LogInformation("Created offline account '{Username}'.", username);
    }

    public async Task SignInMicrosoftAccountAsync()
    {
        await EnsureInitializedAsync().ConfigureAwait(false);

        if (_loginHandler is null)
            throw new InvalidOperationException("LoginHandler not initialized.");

        _logger.LogInformation("Starting interactive Microsoft sign-in.");
        var session = await _loginHandler.AuthenticateInteractively().ConfigureAwait(false);
        _logger.LogInformation("Signed in as '{Username}'.", session.Username);

        await LoadAllAccountsAsync().ConfigureAwait(false);

        // Auto-select the account that was just added.
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (GetSelectedAccount() is null)
            {
                var matched = _accounts.FirstOrDefault(a =>
                    a.Type == AccountType.Microsoft &&
                    string.Equals(a.Name, session.Username, StringComparison.OrdinalIgnoreCase));

                if (matched is not null)
                    ApplySelectedAccount(matched.UniqueId, persist: true);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RemoveAccountAsync(EAccount account)
    {
        _logger.LogInformation("Removing account '{Name}' ({Type}).", account.Name, account.Type);

        if (account.Type == AccountType.Microsoft && _loginHandler is not null)
        {
            var onlineAccount = _loginHandler.AccountManager
                .GetAccounts()
                .OfType<JEGameAccount>()
                .FirstOrDefault(a => a.Profile?.UUID == account.UUID);

            if (onlineAccount is not null)
            {
                await _loginHandler.Signout(onlineAccount).ConfigureAwait(false);
                _logger.LogInformation("Signed out Microsoft account '{Name}'.", account.Name);
            }
        }

        bool wasSelected;

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            wasSelected = string.Equals(account.UniqueId, _selectedAccountId, StringComparison.Ordinal);
            _accounts.Remove(account);

            if (wasSelected)
                ApplySelectedAccount(null, persist: false);

            EnforceOfflineSelectionPolicy(persist: false);
        }
        finally
        {
            _gate.Release();
        }

        PersistAccounts();
    }

    public async Task<MSession> AuthenticateAccountAsync(EAccount account)
    {
        _logger.LogInformation("Authenticating '{Name}' ({Type}).", account.Name, account.Type);

        MSession session;

        if (account.Type == AccountType.Offline)
        {
            EnsureOfflineAccountPolicyMet("Offline accounts require at least one Microsoft account.");
            session = MSession.CreateOfflineSession(account.Name);
        }
        else if (account.Type == AccountType.Microsoft)
        {
            await EnsureInitializedAsync().ConfigureAwait(false);

            if (_loginHandler is null)
                throw new InvalidOperationException("LoginHandler not initialized.");

            var onlineAccount = _loginHandler.AccountManager
                .GetAccounts()
                .OfType<JEGameAccount>()
                .FirstOrDefault(a => a.Profile?.UUID == account.UUID)
                ?? throw new InvalidOperationException(
                    $"Microsoft account '{account.Name}' was not found in the login handler.");

            session = await _loginHandler.Authenticate(onlineAccount).ConfigureAwait(false);
        }
        else
        {
            throw new ArgumentException($"Unknown account type: {account.Type}");
        }

        account.LastUsed = DateTime.UtcNow;
        PersistAccounts();
        return session;
    }

    public EAccount? GetMostRecentlyUsedAccount()
    {
        // Snapshot to avoid enumerating a live collection.
        var snapshot = _accounts.ToList();
        return snapshot.Count == 0
            ? null
            : snapshot.OrderByDescending(a => a.LastUsed).First();
    }

    public EAccount? GetSelectedAccount()
        => string.IsNullOrWhiteSpace(_selectedAccountId)
            ? null
            : _accounts.FirstOrDefault(a =>
                string.Equals(a.UniqueId, _selectedAccountId, StringComparison.Ordinal));

    public void SetSelectedAccount(EAccount? account)
    {
        if (account is null)
        {
            _gate.Wait();
            try { ApplySelectedAccount(null, persist: true); }
            finally { _gate.Release(); }
            return;
        }

        _gate.Wait();
        try
        {
            // Resolve against the live collection by UniqueId only.
            // Fallback by reference in case it was added directly.
            var matched = _accounts.FirstOrDefault(a =>
                ReferenceEquals(a, account) ||
                string.Equals(a.UniqueId, account.UniqueId, StringComparison.Ordinal));

            if (matched is null)
            {
                _logger.LogWarning(
                    "SetSelectedAccount: account '{Name}' (id={Id}) not found in the collection.",
                    account.Name, account.UniqueId);
                return;
            }

            if (matched.Type == AccountType.Offline)
                EnsureOfflineAccountPolicyMet("Selecting an offline account requires at least one Microsoft account.");

            ApplySelectedAccount(matched.UniqueId, persist: true);
        }
        finally
        {
            _gate.Release();
        }
    }

    // ──────────────────────────────────────────────
    // Private – Initialisation
    // ──────────────────────────────────────────────

    private async Task InitializeCoreAsync(string clientId)
    {
        _logger.LogInformation("Initializing AccountService (clientId={ClientId}).", clientId);

        try
        {
            _msalApp  = await MsalClientHelper.BuildApplicationWithCache(clientId).ConfigureAwait(false);
            _loginHandler = new JELoginHandlerBuilder()
                .WithLogger(_logger)
                .WithOAuthProvider(new MsalCodeFlowProvider(_msalApp))
                .WithAccountManager(new InMemoryXboxGameAccountManager(JEGameAccount.FromSessionStorage))
                .Build();

            _logger.LogInformation("AccountService initialized.");
        }
        catch (Exception ex)
        {
            // Allow callers to retry by resetting the task on failure.
            lock (_initLock) { _initializationTask = null; }
            _logger.LogError(ex, "AccountService initialization failed.");
            throw;
        }
    }

    private async Task EnsureInitializedAsync()
    {
        Task? task;
        lock (_initLock) { task = _initializationTask; }

        if (task is null)
            throw new InvalidOperationException(
                "AccountService.InitializeAsync must be called before using account operations.");

        await task.ConfigureAwait(false);
    }

    // ──────────────────────────────────────────────
    // Private – Selection helpers (call under _gate)
    // ──────────────────────────────────────────────

    private void RestoreSelectedAccount()
    {
        // Reload persisted id – it may differ from what was set in the constructor
        // if LoadAllAccountsAsync is called after the constructor.
        _selectedAccountId = _settingsService.Get<string?>(SettingsKeys.SelectedMinecraftAccount, null);

        if (!string.IsNullOrWhiteSpace(_selectedAccountId) && GetSelectedAccount() is null)
        {
            _logger.LogInformation("Previously selected account no longer exists; clearing selection.");
            ApplySelectedAccount(null, persist: false);
            return;
        }

        // Re-apply to refresh IsSelected flags.
        ApplySelectedAccount(_selectedAccountId, persist: false);
    }

    /// <summary>
    /// Updates <see cref="_selectedAccountId"/> and refreshes the <c>IsSelected</c> flag on
    /// every account in the collection. Must be called under <see cref="_gate"/>.
    /// </summary>
    private void ApplySelectedAccount(string? uniqueId, bool persist)
    {
        _selectedAccountId = string.IsNullOrWhiteSpace(uniqueId) ? null : uniqueId;

        foreach (var a in _accounts)
            a.IsSelected = string.Equals(a.UniqueId, _selectedAccountId, StringComparison.Ordinal);

        if (persist)
            _settingsService.Set(SettingsKeys.SelectedMinecraftAccount, _selectedAccountId);
    }

    // ──────────────────────────────────────────────
    // Private – Policy
    // ──────────────────────────────────────────────

    private bool HasMicrosoftAccount()
        => _accounts.Any(a => a.Type == AccountType.Microsoft);

    private bool IsOfflineAccountAllowed()
        => !RequireMicrosoftAccountForOfflineAccounts || HasMicrosoftAccount();

    private void EnsureOfflineAccountPolicyMet(string message)
    {
        if (!IsOfflineAccountAllowed())
            throw new InvalidOperationException(message);
    }

    /// <summary>
    /// If an offline account is selected but the policy no longer allows it,
    /// clears the selection. Must be called under <see cref="_gate"/>.
    /// </summary>
    private void EnforceOfflineSelectionPolicy(bool persist)
    {
        if (IsOfflineAccountAllowed())
            return;

        if (GetSelectedAccount()?.Type == AccountType.Offline)
        {
            _logger.LogInformation("Clearing offline account selection due to policy.");
            ApplySelectedAccount(null, persist);
        }
    }

    // ──────────────────────────────────────────────
    // Private – Persistence
    // ──────────────────────────────────────────────

    private void PersistAccounts()
    {
        try
        {
            _settingsService.Set(SettingsKeys.MinecraftAccounts, _accounts.ToList());
            _settingsService.Set(SettingsKeys.SelectedMinecraftAccount, _selectedAccountId);

            // Note: InMemoryXboxGameAccountManager.SaveAccounts() is intentionally
            // not called – it is a no-op and Microsoft auth tokens are re-fetched
            // from the MSAL token cache on the next authentication.

            _logger.LogDebug("Persisted {Count} accounts.", _accounts.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist accounts.");
            throw;
        }
    }

    // ──────────────────────────────────────────────
    // Private – Utilities
    // ──────────────────────────────────────────────

    /// <summary>
    /// Ensures the account has a non-empty UniqueId, generating one if needed.
    /// Returns the same account for fluent use.
    /// </summary>
    private EAccount EnsureUniqueId(EAccount account)
    {
        if (string.IsNullOrWhiteSpace(account.UniqueId))
        {
            account.UniqueId = Guid.NewGuid().ToString();
            _logger.LogInformation(
                "Generated missing UniqueId for account '{Name}' ({Type}).",
                account.Name, account.Type);
        }

        return account;
    }
}
