using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CmlLib.Core.Auth;
using CmlLib.Core.Auth.Microsoft;
using CmlLib.Core.Auth.Microsoft.Sessions;
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
    private JELoginHandler? _loginHandler;
    private IPublicClientApplication? _msalApp;

    private const string ACCOUNTS_SETTINGS_KEY = "MinecraftAccounts";

    public ObservableCollection<EAccount> Accounts => _accounts;

    public AccountService(ILogger<AccountService> logger, IBaseSettingsService settingsService)
    {
        _logger = logger;
        _settingsService = settingsService;
        _accounts = new ObservableCollection<EAccount>();
    }

    public async Task InitializeAsync(string clientId)
    {
        try
        {
            _logger.LogInformation("Initializing AccountService with client ID: {ClientId}", clientId);

            // Initialize MSAL application
            _msalApp = await MsalClientHelper.BuildApplicationWithCache(clientId);

            // Initialize login handler with MSAL OAuth provider
            _loginHandler = new JELoginHandlerBuilder()
                .WithLogger(_logger)
                .WithOAuthProvider(new MsalCodeFlowProvider(_msalApp))
                .WithAccountManager(new InMemoryXboxGameAccountManager(JEGameAccount.FromSessionStorage))
                .Build();

            _logger.LogInformation("AccountService initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize AccountService");
            throw;
        }
    }

    public async Task LoadAllAccountsAsync()
    {
        try
        {
            _logger.LogInformation("Loading all accounts");

            if (_loginHandler == null)
            {
                _logger.LogWarning("LoginHandler not initialized. Call InitializeAsync first.");
                return;
            }

            // Clear current accounts
            _accounts.Clear();

            // Load stored offline accounts
            var storedAccounts = _settingsService.Get<List<EAccount>>(ACCOUNTS_SETTINGS_KEY, new List<EAccount>());
            _logger.LogInformation("Found {Count} stored accounts", storedAccounts.Count);

            // Get online accounts from MSAL
            var onlineAccounts = _loginHandler.AccountManager.GetAccounts().Select(x => x as JEGameAccount);
            _logger.LogInformation("Found {Count} online accounts", onlineAccounts.Count());

            _accounts.AddRange(storedAccounts.Where(acc => acc.Type == AccountType.Offline));


            // Add any new online accounts that aren't in storage
            foreach (var onlineAccount in onlineAccounts)
            {
                if (!_accounts.Any(acc => acc.UniqueId == onlineAccount.Identifier && acc.Type == AccountType.Microsoft))
                {
                    var newAccount = new EAccount(onlineAccount.Profile?.Username, AccountType.Microsoft, onlineAccount.Profile?.UUID, onlineAccount.Identifier);
                    newAccount.LastUsed = onlineAccount.LastAccess;

                    _accounts.Add(newAccount);

                    _logger.LogInformation("Added new Microsoft account: {Identifier}", onlineAccount.Identifier);
                }
            }

            // Save updated accounts to storage
             SaveAccounts();

            _logger.LogInformation("Successfully loaded {Count} total accounts", _accounts.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load accounts");
            throw;
        }
    }

    public void CreateOfflineAccount(string username)
    {
        try
        {
            _logger.LogInformation("Creating offline account for username: {Username}", username);

            if (string.IsNullOrWhiteSpace(username))
            {
                throw new ArgumentException("Username cannot be empty", nameof(username));
            }

            // Check if account already exists
            if (_accounts.Any(acc => acc.Name.Equals(username, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"Account with username '{username}' already exists");
            }

            var account = new EAccount(username, AccountType.Offline);
            _accounts.Add(account);

             SaveAccounts();

            _logger.LogInformation("Successfully created offline account: {Username}", username);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create offline account for username: {Username}", username);
            throw;
        }
    }

    public async Task SignInMicrosoftAccountAsync()
    {
        try
        {
            _logger.LogInformation("Starting Microsoft account sign-in");

            if (_loginHandler == null)
            {
                throw new InvalidOperationException("LoginHandler not initialized. Call InitializeAsync first.");
            }

            // Authenticate interactively to add a new account
            var session = await _loginHandler.AuthenticateInteractively();

            _logger.LogInformation("Successfully signed in Microsoft account: {Username}", session.Username);
            await LoadAllAccountsAsync();
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sign in Microsoft account");
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
                // Sign out from Microsoft account
                var accounts = _loginHandler.AccountManager.GetAccounts().Select(x => x as JEGameAccount);
                var selectedAccount = accounts.FirstOrDefault(acc => acc?.Profile?.UUID == account.UUID);
               
                if (selectedAccount != null)
                {
                    await _loginHandler.Signout(selectedAccount);
                    _logger.LogInformation("Signed out Microsoft account: {Name}", account.Name);
                }
            }

            // Remove from collection
            _accounts.Remove(account);

            // Save updated accounts
            SaveAccounts();

            _logger.LogInformation("Successfully removed account: {Name}", account.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove account: {Name}", account.Name);
            throw;
        }
    }

    public async Task<MSession> AuthenticateAccountAsync(EAccount account)
    {
        try
        {
            _logger.LogInformation("Authenticating account: {Name} ({Type})", account.Name, account.Type);

            MSession session;

            if (account.Type == AccountType.Offline)
            {
                session = MSession.CreateOfflineSession(account.Name);
                _logger.LogInformation("Created offline session for: {Name}", account.Name);
            }
            else if (account.Type == AccountType.Microsoft)
            {
                if (_loginHandler == null)
                {
                    throw new InvalidOperationException("LoginHandler not initialized. Call InitializeAsync first.");
                }

                var accounts = _loginHandler.AccountManager.GetAccounts().Select(x => x as JEGameAccount);
                var selectedAccount = accounts.FirstOrDefault(acc => acc?.Profile?.UUID == account.UUID);

                if (selectedAccount == null)
                {
                    throw new InvalidOperationException($"Microsoft account '{account.Name}' not found in login handler");
                }

                session = await _loginHandler.Authenticate(selectedAccount);
                _logger.LogInformation("Authenticated Microsoft account: {Name}", account.Name);
            }
            else
            {
                throw new ArgumentException($"Unknown account type: {account.Type}");
            }

            // Update last used time
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
            var mostRecent = _accounts.OrderByDescending(acc => acc.LastUsed).FirstOrDefault();

            if (mostRecent != null)
            {
                _logger.LogInformation("Most recently used account: {Name} (Last used: {LastUsed})",
                    mostRecent.Name, mostRecent.LastUsed);
            }
            else
            {
                _logger.LogInformation("No accounts available");
            }

            return mostRecent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get most recently used account");
            return null;
        }
    }

    private void SaveAccounts()
    {
        try
        {
            var accountsToSave = _accounts.ToList();
            _settingsService.Set(ACCOUNTS_SETTINGS_KEY, accountsToSave);
             _loginHandler?.AccountManager.SaveAccounts();
            _logger.LogDebug("Saved {Count} accounts to settings", accountsToSave.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save accounts to settings");
            throw;
        }
    }
}
