using System.Collections.ObjectModel;
using CmlLib.Core.Auth;
using Emerald.CoreX.Helpers;
using Emerald.CoreX.Models;
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
        : this(logger, settingsService, new InlineUiDispatcher())
    {
    }

    internal AccountService(
        ILogger<AccountService> logger,
        IBaseSettingsService settingsService,
        IUiDispatcher uiDispatcher,
        string? accountStorePath = null,
        IMicrosoftAccountClient? microsoftAccountClient = null)
    {
        _logger = logger;
        _settingsService = settingsService;
        _uiDispatcher = uiDispatcher;
        _microsoftAccountClient = microsoftAccountClient ?? new CmlLibMicrosoftAccountClient(logger);
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

            var storedAccounts = _settingsService.Get(SettingsKeys.MinecraftAccounts, new List<EAccount>());
            var offlineAccounts = storedAccounts
                .Where(account => account.Type == AccountType.Offline)
                .Select(EnsureUniqueId)
                .ToList();
            var ignoredLegacyMicrosoftCount = storedAccounts.Count - offlineAccounts.Count;
            var onlineAccounts = _microsoftAccountClient.GetAccounts();

            _logger.LogInformation(
                "Found {OfflineCount} offline accounts, ignored {IgnoredLegacyMicrosoftCount} legacy Microsoft settings entries, and found {OnlineCount} Microsoft accounts in CmlLib.",
                offlineAccounts.Count,
                ignoredLegacyMicrosoftCount,
                onlineAccounts.Count);

            var totalCount = 0;
            var microsoftCount = 0;

            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                await _uiDispatcher.InvokeAsync(() =>
                {
                    _accounts.Clear();

                    foreach (var offline in offlineAccounts)
                        _accounts.Add(EnsureUniqueId(offline));

                    var existingIds = new HashSet<string>(
                        _accounts.Select(account => account.UniqueId),
                        StringComparer.Ordinal);

                    foreach (var online in onlineAccounts)
                    {
                        if (string.IsNullOrWhiteSpace(online.Identifier))
                        {
                            _logger.LogWarning("Skipping a Microsoft account with a missing identifier.");
                            continue;
                        }

                        if (existingIds.Contains(online.Identifier))
                            continue;

                        _accounts.Add(new EAccount(
                            online.Name,
                            AccountType.Microsoft,
                            string.IsNullOrWhiteSpace(online.UUID) ? online.Identifier : online.UUID,
                            online.Identifier)
                        {
                            LastUsed = online.LastAccess == default ? DateTime.UtcNow : online.LastAccess
                        });

                        existingIds.Add(online.Identifier);
                    }

                    RestoreSelectedAccountCore();
                    EnforceOfflineSelectionPolicyCore(persist: false);

                    totalCount = _accounts.Count;
                    microsoftCount = _accounts.Count(account => account.Type == AccountType.Microsoft);
                }).ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }

            PersistAccounts();
            _logger.LogInformation(
                "Loaded {TotalCount} accounts ({OfflineCount} offline, {MicrosoftCount} Microsoft).",
                totalCount,
                offlineAccounts.Count,
                microsoftCount);
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
            List<EAccount> offlineAccounts = [];
            string? selectedAccountId = null;
            _uiDispatcher.Invoke(() =>
            {
                offlineAccounts = _accounts
                    .Where(account => account.Type == AccountType.Offline)
                    .Select(CloneStoredAccount)
                    .ToList();
                selectedAccountId = _selectedAccountId;
            });

            _settingsService.Set(SettingsKeys.MinecraftAccounts, offlineAccounts);
            _settingsService.Set(SettingsKeys.SelectedMinecraftAccount, selectedAccountId);
            _logger.LogDebug(
                "Persisted {OfflineCount} offline accounts. SelectedAccountId: {SelectedAccountId}.",
                offlineAccounts.Count,
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
}
