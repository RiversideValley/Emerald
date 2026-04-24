using System.Collections.ObjectModel;
using CmlLib.Core.Auth;
using Emerald.CoreX.Helpers;
using Emerald.CoreX.Models;
using Emerald.CoreX.Notifications;
using Emerald.Services;
using Microsoft.Extensions.Logging;

namespace Emerald.CoreX.Services;

public sealed class AccountService : IAccountService
{
    public bool RequireMicrosoftAccountForOfflineAccounts => true;

    private readonly ILogger<AccountService> _logger;
    private readonly IBaseSettingsService _settingsService;
    private readonly IUiDispatcher _uiDispatcher;
    private readonly IMicrosoftAccountClient _microsoftAccountClient;
    private readonly INotificationService? _notificationService;
    private readonly string _accountStorePath;

    // Protects mutations of _accounts and _selectedAccountId.
    private readonly SemaphoreSlim _gate = new(1, 1);

    // Serializes account reloads independently from other mutations.
    private readonly SemaphoreSlim _loadGate = new(1, 1);

    private readonly ObservableCollection<EAccount> _accounts = new();

    private readonly object _initLock = new();
    private Task? _initializationTask;

    private string? _selectedAccountId;

    public AccountService(ILogger<AccountService> logger, IBaseSettingsService settingsService)
        : this(logger, settingsService, new InlineUiDispatcher(), notificationService: null)
    {
    }

    internal AccountService(
        ILogger<AccountService> logger,
        IBaseSettingsService settingsService,
        IUiDispatcher uiDispatcher,
        string? accountStorePath = null,
        IMicrosoftAccountClient? microsoftAccountClient = null,
        INotificationService? notificationService = null)
    {
        _logger = logger;
        _settingsService = settingsService;
        _uiDispatcher = uiDispatcher;
        _microsoftAccountClient = microsoftAccountClient ?? new CmlLibMicrosoftAccountClient(logger);
        _notificationService = notificationService;
        _accountStorePath = string.IsNullOrWhiteSpace(accountStorePath)
            ? GetDefaultAccountStorePath()
            : accountStorePath;
        _selectedAccountId = _settingsService.Get<string?>(SettingsKeys.SelectedMinecraftAccount, null);
    }

    public ObservableCollection<EAccount> Accounts => _accounts;

    public Task InitializeAsync(string clientId)
    {
        lock (_initLock)
        {
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
            _logger.LogInformation("Loading accounts from Emerald settings and the CmlLib account store.");
            var loadState = BuildAccountLoadState();

            _logger.LogInformation(
                "Found {OfflineCount} offline accounts and {StoredMicrosoftCount} stored Microsoft accounts in settings, and found {OnlineCount} Microsoft accounts in CmlLib.",
                loadState.OfflineCount,
                loadState.StoredMicrosoftCount,
                loadState.MicrosoftCount);

            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                await _uiDispatcher.InvokeAsync(() =>
                {
                    ApplyLoadedAccountsCore(loadState.Accounts);
                }).ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }

            PersistAccounts();
            NotifyLoggedOutMicrosoftAccounts(loadState.LoggedOutMicrosoftAccountNames);
            _logger.LogInformation(
                "Loaded {TotalCount} accounts ({OfflineCount} offline, {MicrosoftCount} Microsoft). Logged out Microsoft accounts detected: {LoggedOutMicrosoftCount}.",
                loadState.TotalCount,
                loadState.OfflineCount,
                loadState.MicrosoftCount,
                loadState.LoggedOutMicrosoftCount);
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
            _uiDispatcher.Invoke(() =>
            {
                if (_accounts.Any(account => account.Name.Equals(username, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException($"An account named '{username}' already exists.");

                var account = new EAccount(username, AccountType.Offline);
                _accounts.Add(account);

                if (GetSelectedAccountCore() is null)
                    ApplySelectedAccountCore(account.UniqueId, persist: false);
            });
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

        _logger.LogInformation("Starting interactive Microsoft sign-in.");
        var beforeIdentifiers = _microsoftAccountClient
            .GetAccounts()
            .Select(account => account.Identifier)
            .Where(identifier => !string.IsNullOrWhiteSpace(identifier))
            .ToHashSet(StringComparer.Ordinal);

        var signInResult = await _microsoftAccountClient.SignInInteractivelyAsync().ConfigureAwait(false);
        _logger.LogInformation(
            "Interactive Microsoft sign-in completed for '{Username}' (candidate identifier: {Identifier}).",
            signInResult.Username ?? "Unknown",
            signInResult.Identifier ?? "None");

        var afterAccounts = _microsoftAccountClient.GetAccounts();
        await LoadAllAccountsAsync().ConfigureAwait(false);

        var candidateIdentifiers = BuildMaterializationCandidates(
            signInResult,
            beforeIdentifiers,
            afterAccounts,
            _microsoftAccountClient.GetDefaultAccountIdentifier());

        EAccount? materializedAccount = null;
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await _uiDispatcher.InvokeAsync(() =>
            {
                materializedAccount = ResolveMaterializedMicrosoftAccountCore(candidateIdentifiers);
                if (materializedAccount is null)
                {
                    throw new InvalidOperationException(
                        "Microsoft sign-in completed, but Emerald could not materialize the signed-in account.");
                }

                if (GetSelectedAccountCore() is null)
                    ApplySelectedAccountCore(materializedAccount.UniqueId, persist: true);
            }).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }

        _logger.LogInformation(
            "Microsoft account '{Name}' materialized with identifier '{Identifier}'.",
            materializedAccount!.Name,
            materializedAccount.UniqueId);
    }

    public async Task RemoveAccountAsync(EAccount account)
    {
        _logger.LogInformation("Removing account '{Name}' ({Type}).", account.Name, account.Type);

        if (account.Type == AccountType.Microsoft)
        {
            await EnsureInitializedAsync().ConfigureAwait(false);
            await _microsoftAccountClient.SignOutAsync(account.UniqueId).ConfigureAwait(false);
            _logger.LogInformation("Signed out Microsoft account '{Name}' ({Identifier}).", account.Name, account.UniqueId);
            await LoadAllAccountsAsync().ConfigureAwait(false);
            return;
        }

        var wasSelected = false;
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await _uiDispatcher.InvokeAsync(() =>
            {
                wasSelected = string.Equals(account.UniqueId, _selectedAccountId, StringComparison.Ordinal);
                _accounts.Remove(account);

                if (wasSelected)
                    ApplySelectedAccountCore(null, persist: false);

                EnforceOfflineSelectionPolicyCore(persist: false);
            }).ConfigureAwait(false);
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
            session = await _microsoftAccountClient.AuthenticateAsync(account.UniqueId).ConfigureAwait(false);
        }
        else
        {
            throw new ArgumentException($"Unknown account type: {account.Type}");
        }

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await _uiDispatcher.InvokeAsync(() =>
            {
                var matched = _accounts.FirstOrDefault(candidate =>
                    ReferenceEquals(candidate, account) ||
                    string.Equals(candidate.UniqueId, account.UniqueId, StringComparison.Ordinal));

                if (matched is not null)
                    matched.LastUsed = DateTime.UtcNow;
            }).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }

        PersistAccounts();
        return session;
    }

    public EAccount? GetMostRecentlyUsedAccount()
    {
        EAccount? account = null;
        _uiDispatcher.Invoke(() =>
        {
            account = _accounts.Count == 0
                ? null
                : _accounts.OrderByDescending(candidate => candidate.LastUsed).First();
        });

        return account;
    }

    public EAccount? GetSelectedAccount()
    {
        EAccount? account = null;
        _uiDispatcher.Invoke(() => account = GetSelectedAccountCore());
        return account;
    }

    public void SetSelectedAccount(EAccount? account)
    {
        _gate.Wait();
        try
        {
            _uiDispatcher.Invoke(() =>
            {
                if (account is null)
                {
                    ApplySelectedAccountCore(null, persist: true);
                    return;
                }

                var matched = _accounts.FirstOrDefault(candidate =>
                    ReferenceEquals(candidate, account) ||
                    string.Equals(candidate.UniqueId, account.UniqueId, StringComparison.Ordinal));

                if (matched is null)
                {
                    _logger.LogWarning(
                        "SetSelectedAccount: account '{Name}' (id={Id}) not found in the collection.",
                        account.Name,
                        account.UniqueId);
                    return;
                }

                matched = EnsureUniqueId(matched);

                if (matched.Type == AccountType.Offline)
                    EnsureOfflineAccountPolicyMet("Selecting an offline account requires at least one Microsoft account.");

                ApplySelectedAccountCore(matched.UniqueId, persist: true);
            });
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task InitializeCoreAsync(string clientId)
    {
        _logger.LogInformation(
            "Initializing AccountService (clientId={ClientId}, accountStorePath={AccountStorePath}).",
            clientId,
            _accountStorePath);

        try
        {
            await _microsoftAccountClient.InitializeAsync(clientId, _accountStorePath).ConfigureAwait(false);
            _logger.LogInformation("AccountService initialized.");
        }
        catch (Exception ex)
        {
            lock (_initLock)
            {
                _initializationTask = null;
            }

            _logger.LogError(ex, "AccountService initialization failed.");
            throw;
        }
    }

    private async Task EnsureInitializedAsync()
    {
        Task? task;
        lock (_initLock)
        {
            task = _initializationTask;
        }

        if (task is null)
            throw new InvalidOperationException(
                "AccountService.InitializeAsync must be called before using account operations.");

        await task.ConfigureAwait(false);
    }

    private void RestoreSelectedAccountCore()
    {
        _selectedAccountId = _settingsService.Get<string?>(SettingsKeys.SelectedMinecraftAccount, null);

        if (!string.IsNullOrWhiteSpace(_selectedAccountId) && GetSelectedAccountCore() is null)
        {
            _logger.LogInformation("Previously selected account no longer exists; clearing selection.");
            ApplySelectedAccountCore(null, persist: false);
            return;
        }

        ApplySelectedAccountCore(_selectedAccountId, persist: false);
    }

    private EAccount? GetSelectedAccountCore()
        => string.IsNullOrWhiteSpace(_selectedAccountId)
            ? null
            : _accounts.FirstOrDefault(account =>
                string.Equals(account.UniqueId, _selectedAccountId, StringComparison.Ordinal));

    private void ApplySelectedAccountCore(string? uniqueId, bool persist)
    {
        _selectedAccountId = string.IsNullOrWhiteSpace(uniqueId) ? null : uniqueId;

        foreach (var account in _accounts)
            account.IsSelected = string.Equals(account.UniqueId, _selectedAccountId, StringComparison.Ordinal);

        if (persist)
            _settingsService.Set(SettingsKeys.SelectedMinecraftAccount, _selectedAccountId);
    }

    private bool HasMicrosoftAccountCore()
        => _accounts.Any(account => account.Type == AccountType.Microsoft);

    private bool IsOfflineAccountAllowed()
        => !RequireMicrosoftAccountForOfflineAccounts || HasMicrosoftAccountCore();

    private void EnsureOfflineAccountPolicyMet(string message)
    {
        if (!IsOfflineAccountAllowed())
            throw new InvalidOperationException(message);
    }

    private void EnforceOfflineSelectionPolicyCore(bool persist)
    {
        if (IsOfflineAccountAllowed())
            return;

        if (GetSelectedAccountCore()?.Type == AccountType.Offline)
        {
            _logger.LogInformation("Clearing offline account selection due to policy.");
            ApplySelectedAccountCore(null, persist);
        }
    }

    private void PersistAccounts()
    {
        try
        {
            List<EAccount> storedAccounts = [];
            string? selectedAccountId = null;
            _uiDispatcher.Invoke(() =>
            {
                storedAccounts = _accounts
                    .Select(CloneStoredAccount)
                    .ToList();
                selectedAccountId = _selectedAccountId;
            });

            _settingsService.Set(SettingsKeys.MinecraftAccounts, storedAccounts);
            _settingsService.Set(SettingsKeys.SelectedMinecraftAccount, selectedAccountId);
            var offlineCount = storedAccounts.Count(account => account.Type == AccountType.Offline);
            var microsoftCount = storedAccounts.Count(account => account.Type == AccountType.Microsoft);
            _logger.LogDebug(
                "Persisted {TotalCount} accounts ({OfflineCount} offline, {MicrosoftCount} Microsoft). SelectedAccountId: {SelectedAccountId}.",
                storedAccounts.Count,
                offlineCount,
                microsoftCount,
                selectedAccountId ?? "None");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist accounts.");
            throw;
        }
    }

    private EAccount EnsureUniqueId(EAccount account)
    {
        if (string.IsNullOrWhiteSpace(account.UniqueId))
        {
            account.UniqueId = Guid.NewGuid().ToString();
            _logger.LogInformation(
                "Generated missing UniqueId for account '{Name}' ({Type}).",
                account.Name,
                account.Type);
        }

        return account;
    }

    private EAccount CloneStoredAccount(EAccount account)
    {
        var storedAccount = EnsureUniqueId(account);
        return new EAccount(storedAccount.Name, storedAccount.Type, storedAccount.UUID, storedAccount.UniqueId)
        {
            LastUsed = storedAccount.LastUsed
        };
    }

    private AccountLoadState BuildAccountLoadState()
    {
        var storedAccounts = _settingsService.Get(SettingsKeys.MinecraftAccounts, new List<EAccount>());
        var offlineAccounts = storedAccounts
            .Where(account => account.Type == AccountType.Offline)
            .Select(CloneStoredAccount)
            .ToList();
        var storedMicrosoftAccounts = storedAccounts
            .Where(account => account.Type == AccountType.Microsoft)
            .Select(CloneStoredAccount)
            .ToList();
        var onlineMicrosoftAccounts = _microsoftAccountClient.GetAccounts()
            .Where(account =>
            {
                if (!string.IsNullOrWhiteSpace(account.Identifier))
                    return true;

                _logger.LogWarning("Skipping a Microsoft account with a missing identifier.");
                return false;
            })
            .Select(CreateMicrosoftAccount)
            .ToList();

        var onlineIdentifiers = new HashSet<string>(
            onlineMicrosoftAccounts.Select(account => account.UniqueId),
            StringComparer.Ordinal);
        var loggedOutAccountNames = storedMicrosoftAccounts
            .Where(account => !onlineIdentifiers.Contains(account.UniqueId))
            .Select(account => account.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var loadedAccounts = new List<EAccount>(offlineAccounts.Count + onlineMicrosoftAccounts.Count);
        loadedAccounts.AddRange(offlineAccounts);
        loadedAccounts.AddRange(onlineMicrosoftAccounts);

        return new AccountLoadState(
            loadedAccounts,
            offlineAccounts.Count,
            storedMicrosoftAccounts.Count,
            onlineMicrosoftAccounts.Count,
            loggedOutAccountNames);
    }

    private void NotifyLoggedOutMicrosoftAccounts(IReadOnlyList<string> loggedOutMicrosoftAccountNames)
    {
        if (_notificationService is null || loggedOutMicrosoftAccountNames.Count == 0)
            return;

        if (loggedOutMicrosoftAccountNames.Count == 1)
        {
            _notificationService.Warning(
                "Microsoft account signed out",
                $"'{loggedOutMicrosoftAccountNames[0]}' is no longer signed in and was removed from Accounts.");
            return;
        }

        _notificationService.Warning(
            "Microsoft accounts signed out",
            $"{loggedOutMicrosoftAccountNames.Count} Microsoft accounts are no longer signed in and were removed from Accounts.");
    }

    private static EAccount CreateMicrosoftAccount(MicrosoftAccountInfo account)
        => new(
            account.Name,
            AccountType.Microsoft,
            string.IsNullOrWhiteSpace(account.UUID) ? account.Identifier : account.UUID,
            account.Identifier)
        {
            LastUsed = account.LastAccess == default ? DateTime.UtcNow : account.LastAccess
        };

    private void ApplyLoadedAccountsCore(IEnumerable<EAccount> accounts)
    {
        _accounts.Clear();

        foreach (var account in accounts)
            _accounts.Add(account);

        RestoreSelectedAccountCore();
        EnforceOfflineSelectionPolicyCore(persist: false);
    }

    private static IReadOnlyList<string> BuildMaterializationCandidates(
        MicrosoftInteractiveSignInResult signInResult,
        ISet<string> beforeIdentifiers,
        IReadOnlyList<MicrosoftAccountInfo> afterAccounts,
        string? defaultAccountIdentifier)
    {
        var candidates = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        static void AddCandidate(List<string> candidates, HashSet<string> seen, string? identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier) || !seen.Add(identifier))
                return;

            candidates.Add(identifier);
        }

        AddCandidate(candidates, seen, signInResult.Identifier);
        AddCandidate(candidates, seen, signInResult.UUID);

        foreach (var addedAccount in afterAccounts
                     .Where(account => !beforeIdentifiers.Contains(account.Identifier))
                     .OrderByDescending(account => account.LastAccess))
        {
            AddCandidate(candidates, seen, addedAccount.Identifier);
        }

        AddCandidate(candidates, seen, defaultAccountIdentifier);
        AddCandidate(candidates, seen, afterAccounts.OrderByDescending(account => account.LastAccess).FirstOrDefault()?.Identifier);

        return candidates;
    }

    private EAccount? ResolveMaterializedMicrosoftAccountCore(IEnumerable<string> candidateIdentifiers)
    {
        foreach (var identifier in candidateIdentifiers)
        {
            var matched = _accounts.FirstOrDefault(account =>
                account.Type == AccountType.Microsoft &&
                string.Equals(account.UniqueId, identifier, StringComparison.Ordinal));

            if (matched is not null)
                return matched;
        }

        return null;
    }

    private static string GetDefaultAccountStorePath()
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Emerald",
            "accounts",
            "cml_accounts.json");

    private sealed record AccountLoadState(
        List<EAccount> Accounts,
        int OfflineCount,
        int StoredMicrosoftCount,
        int MicrosoftCount,
        IReadOnlyList<string> LoggedOutMicrosoftAccountNames)
    {
        public int TotalCount => Accounts.Count;
        public int LoggedOutMicrosoftCount => LoggedOutMicrosoftAccountNames.Count;
    }
}
