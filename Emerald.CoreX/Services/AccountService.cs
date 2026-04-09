using System.Collections.ObjectModel;
using CmlLib.Core.Auth;
using CmlLib.Core.Auth.Microsoft;
using CmlLib.Core.Auth.Microsoft.Sessions;
using Emerald.CoreX.Helpers;
using Emerald.CoreX.Models;
using Emerald.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Uno.Extensions.Specialized;
using XboxAuthNet.Game.Accounts;
using XboxAuthNet.Game.Msal;
using XboxAuthNet.Game.Msal.OAuth;

namespace Emerald.CoreX.Services;

public class AccountService : IAccountService
{
    private readonly ILogger<AccountService> _logger;
    private readonly IBaseSettingsService _settingsService;
    private readonly ObservableCollection<EAccount> _accounts;
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private readonly object _initializationGate = new();

    private JELoginHandler? _loginHandler;
    private IPublicClientApplication? _msalApp;
    private Task? _initializationTask;
    private string? _selectedAccountId;

    public ObservableCollection<EAccount> Accounts => _accounts;

    public AccountService(ILogger<AccountService> logger, IBaseSettingsService settingsService)
    {
        _logger = logger;
        _settingsService = settingsService;
        _accounts = new ObservableCollection<EAccount>();
        _selectedAccountId = _settingsService.Get<string?>(SettingsKeys.SelectedMinecraftAccount, null);
    }

    public Task InitializeAsync(string clientId)
    {
        lock (_initializationGate)
        {
            _initializationTask ??= InitializeCoreAsync(clientId);
            return _initializationTask;
        }
    }

    public async Task LoadAllAccountsAsync()
    {
        await EnsureInitializedAsync();
        await _loadGate.WaitAsync();

        try
        {
            _logger.LogInformation("Loading all accounts.");

            if (_loginHandler == null)
            {
                throw new InvalidOperationException("LoginHandler was not initialized.");
            }

            _accounts.Clear();

            var storedAccounts = _settingsService.Get(SettingsKeys.MinecraftAccounts, new List<EAccount>());
            _logger.LogInformation("Found {Count} stored accounts.", storedAccounts.Count);

            var onlineAccounts = _loginHandler.AccountManager
                .GetAccounts()
                .OfType<JEGameAccount>()
                .ToArray();

            _logger.LogInformation("Found {Count} online accounts.", onlineAccounts.Length);

            _accounts.AddRange(storedAccounts.Where(acc => acc.Type == AccountType.Offline));

            foreach (var onlineAccount in onlineAccounts)
            {
                if (_accounts.Any(acc => acc.UniqueId == onlineAccount.Identifier && acc.Type == AccountType.Microsoft))
                {
                    continue;
                }

                var newAccount = new EAccount(
                    onlineAccount.Profile?.Username ?? "Microsoft Account",
                    AccountType.Microsoft,
                    onlineAccount.Profile?.UUID ?? string.Empty,
                    onlineAccount.Identifier);

                newAccount.LastUsed = onlineAccount.LastAccess;
                _accounts.Add(newAccount);
            }

            EnsureUniqueIds();
            RestoreSelectedAccount();
            SaveAccounts();

            _logger.LogInformation("Loaded {Count} total accounts.", _accounts.Count);
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
        try
        {
            _logger.LogInformation("Creating offline account for username: {Username}", username);

            if (string.IsNullOrWhiteSpace(username))
            {
                throw new ArgumentException("Username cannot be empty.", nameof(username));
            }

            if (_accounts.Any(acc => acc.Name.Equals(username, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"Account with username '{username}' already exists.");
            }

            var account = new EAccount(username, AccountType.Offline);
            _accounts.Add(account);

            if (GetSelectedAccount() == null)
            {
                ApplySelectedAccount(account.UniqueId, persistSelection: false);
            }

            SaveAccounts();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create offline account for username: {Username}", username);
            throw;
        }
    }

    public async Task SignInMicrosoftAccountAsync()
    {
        await EnsureInitializedAsync();

        try
        {
            _logger.LogInformation("Starting Microsoft account sign-in.");

            if (_loginHandler == null)
            {
                throw new InvalidOperationException("LoginHandler not initialized. Call InitializeAsync first.");
            }

            var session = await _loginHandler.AuthenticateInteractively();
            _logger.LogInformation("Successfully signed in Microsoft account: {Username}", session.Username);

            await LoadAllAccountsAsync();

            if (GetSelectedAccount() == null)
            {
                var matchingAccount = _accounts.FirstOrDefault(acc =>
                    acc.Type == AccountType.Microsoft &&
                    string.Equals(acc.Name, session.Username, StringComparison.OrdinalIgnoreCase));

                if (matchingAccount != null)
                {
                    SetSelectedAccount(matchingAccount);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sign in Microsoft account.");
            throw;
        }
    }

    public async Task RemoveAccountAsync(EAccount account)
    {
        try
        {
            _logger.LogInformation("Removing account: {Name} ({Type})", account.Name, account.Type);

            if (account.Type == AccountType.Microsoft && _loginHandler != null)
            {
                var selectedAccount = _loginHandler.AccountManager
                    .GetAccounts()
                    .OfType<JEGameAccount>()
                    .FirstOrDefault(acc => acc.Profile?.UUID == account.UUID);

                if (selectedAccount != null)
                {
                    await _loginHandler.Signout(selectedAccount);
                    _logger.LogInformation("Signed out Microsoft account: {Name}", account.Name);
                }
            }

            var wasSelected = string.Equals(account.UniqueId, _selectedAccountId, StringComparison.Ordinal);
            _accounts.Remove(account);

            if (wasSelected)
            {
                ApplySelectedAccount(null, persistSelection: false);
            }

            SaveAccounts();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove account: {Name}", account.Name);
            throw;
        }
    }

    public async Task<MSession> AuthenticateAccountAsync(EAccount account)
    {
        await EnsureInitializedAsync();

        try
        {
            _logger.LogInformation("Authenticating account: {Name} ({Type})", account.Name, account.Type);

            MSession session;

            if (account.Type == AccountType.Offline)
            {
                session = MSession.CreateOfflineSession(account.Name);
            }
            else if (account.Type == AccountType.Microsoft)
            {
                if (_loginHandler == null)
                {
                    throw new InvalidOperationException("LoginHandler not initialized. Call InitializeAsync first.");
                }

                var selectedAccount = _loginHandler.AccountManager
                    .GetAccounts()
                    .OfType<JEGameAccount>()
                    .FirstOrDefault(acc => acc.Profile?.UUID == account.UUID);

                if (selectedAccount == null)
                {
                    throw new InvalidOperationException($"Microsoft account '{account.Name}' was not found in the login handler.");
                }

                session = await _loginHandler.Authenticate(selectedAccount);
            }
            else
            {
                throw new ArgumentException($"Unknown account type: {account.Type}");
            }

            account.LastUsed = DateTime.UtcNow;
            SaveAccounts();
            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to authenticate account: {Name}", account.Name);
            throw;
        }
    }

    public EAccount? GetMostRecentlyUsedAccount()
    {
        try
        {
            return _accounts
                .OrderByDescending(acc => acc.LastUsed)
                .FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get the most recently used account.");
            return null;
        }
    }

    public EAccount? GetSelectedAccount()
        => _accounts.FirstOrDefault(acc => string.Equals(acc.UniqueId, _selectedAccountId, StringComparison.Ordinal));

    public void SetSelectedAccount(EAccount? account)
    {
        if (account == null)
        {
            ApplySelectedAccount(null);
            return;
        }

        var hadLegacyIds = EnsureUniqueIds();

        if (string.IsNullOrWhiteSpace(account.UniqueId))
        {
            account.UniqueId = Guid.NewGuid().ToString();
            hadLegacyIds = true;
        }

        var matchingAccount = _accounts.FirstOrDefault(acc => ReferenceEquals(acc, account))
            ?? _accounts.FirstOrDefault(acc => string.Equals(acc.UniqueId, account.UniqueId, StringComparison.Ordinal))
            ?? _accounts.FirstOrDefault(acc =>
                acc.Type == account.Type &&
                string.Equals(acc.UUID, account.UUID, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(acc.Name, account.Name, StringComparison.OrdinalIgnoreCase));

        ApplySelectedAccount(matchingAccount?.UniqueId);

        if (hadLegacyIds)
        {
            SaveAccounts();
        }
    }

    private async Task InitializeCoreAsync(string clientId)
    {
        try
        {
            _logger.LogInformation("Initializing AccountService with client ID: {ClientId}", clientId);

            _msalApp = await MsalClientHelper.BuildApplicationWithCache(clientId);

            _loginHandler = new JELoginHandlerBuilder()
                .WithLogger(_logger)
                .WithOAuthProvider(new MsalCodeFlowProvider(_msalApp))
                .WithAccountManager(new InMemoryXboxGameAccountManager(JEGameAccount.FromSessionStorage))
                .Build();

            _logger.LogInformation("AccountService initialized successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize AccountService.");
            throw;
        }
    }

    private async Task EnsureInitializedAsync()
    {
        Task? initializationTask;

        lock (_initializationGate)
        {
            initializationTask = _initializationTask;
        }

        if (initializationTask == null)
        {
            throw new InvalidOperationException("AccountService.InitializeAsync must be called before using account operations.");
        }

        await initializationTask;
    }

    private void RestoreSelectedAccount()
    {
        _selectedAccountId = _settingsService.Get<string?>(SettingsKeys.SelectedMinecraftAccount, null);

        if (!string.IsNullOrWhiteSpace(_selectedAccountId) && GetSelectedAccount() == null)
        {
            _logger.LogInformation("Stored selected account no longer exists. Clearing the selection.");
            ApplySelectedAccount(null);
            return;
        }

        ApplySelectedAccount(_selectedAccountId, persistSelection: false);
    }

    private bool EnsureUniqueIds()
    {
        var hadLegacyIds = false;

        foreach (var account in _accounts)
        {
            if (!string.IsNullOrWhiteSpace(account.UniqueId))
            {
                continue;
            }

            account.UniqueId = Guid.NewGuid().ToString();
            hadLegacyIds = true;
            _logger.LogInformation("Generated missing unique account id for {AccountName} ({AccountType}).", account.Name, account.Type);
        }

        return hadLegacyIds;
    }

    private void ApplySelectedAccount(string? uniqueId, bool persistSelection = true)
    {
        _selectedAccountId = string.IsNullOrWhiteSpace(uniqueId) ? null : uniqueId;

        foreach (var account in _accounts)
        {
            account.IsSelected = string.Equals(account.UniqueId, _selectedAccountId, StringComparison.Ordinal);
        }

        if (persistSelection)
        {
            _settingsService.Set(SettingsKeys.SelectedMinecraftAccount, _selectedAccountId);
        }
    }

    private void SaveAccounts()
    {
        try
        {
            _settingsService.Set(SettingsKeys.MinecraftAccounts, _accounts.ToList());
            _settingsService.Set(SettingsKeys.SelectedMinecraftAccount, _selectedAccountId);
            _loginHandler?.AccountManager.SaveAccounts();
            _logger.LogDebug("Saved {Count} accounts to settings.", _accounts.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save accounts to settings.");
            throw;
        }
    }
}
