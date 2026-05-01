using System.Collections.ObjectModel;
using Emerald.CoreX.Helpers;
using Emerald.CoreX.Models;
using Emerald.CoreX.Notifications;
using Emerald.CoreX.Services.Auth;
using Emerald.CoreX.Services.Auth.Authlib;
using Emerald.CoreX.Services.Auth.ElyBy;
using Emerald.CoreX.Services.Auth.Microsoft;
using Emerald.CoreX.Services.Auth.Offline;
using Emerald.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Emerald.CoreX.Services;

public sealed partial class AccountService : IAccountService
{
    public bool RequireMicrosoftAccountForOfflineAccounts => false;
    public bool RequireMicrosoftAccountForElyByAccounts => false;

    private readonly ILogger<AccountService> _logger;
    private readonly IBaseSettingsService _settingsService;
    private readonly IUiDispatcher _uiDispatcher;
    private readonly IMicrosoftAccountClient _microsoftAccountClient;
    private readonly IElyByAuthClient _elyByAuthClient;
    private readonly IElyByAccountStore _elyByAccountStore;
    private readonly INotificationService? _notificationService;
    private readonly string _accountStorePath;
    private readonly IReadOnlyDictionary<AccountType, IAccountAuthenticationProvider> _authenticationProviders;

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
        INotificationService? notificationService = null,
        IElyByAuthClient? elyByAuthClient = null,
        IElyByAccountStore? elyByAccountStore = null,
        IAuthlibInjectorService? authlibInjectorService = null,
        IEnumerable<IAccountAuthenticationProvider>? authenticationProviders = null)
    {
        _logger = logger;
        _settingsService = settingsService;
        _uiDispatcher = uiDispatcher;
        _microsoftAccountClient = microsoftAccountClient ?? new CmlLibMicrosoftAccountClient(logger);
        _elyByAuthClient = elyByAuthClient ?? new ElyByAuthClient(NullLogger<ElyByAuthClient>.Instance);
        _elyByAccountStore = elyByAccountStore ?? new ElyByAccountStore(settingsService);
        _notificationService = notificationService;
        _accountStorePath = string.IsNullOrWhiteSpace(accountStorePath)
            ? GetDefaultAccountStorePath()
            : accountStorePath;
        _authenticationProviders = (authenticationProviders ?? CreateDefaultAuthenticationProviders(authlibInjectorService))
            .ToDictionary(provider => provider.AccountType);
        _selectedAccountId = _settingsService.Get<string?>(SettingsKeys.SelectedMinecraftAccount, null);
    }

    public ObservableCollection<EAccount> Accounts => _accounts;

    private IReadOnlyList<IAccountAuthenticationProvider> CreateDefaultAuthenticationProviders(
        IAuthlibInjectorService? authlibInjectorService)
    {
        var authlib = authlibInjectorService
            ?? new AuthlibInjectorService(NullLogger<AuthlibInjectorService>.Instance);

        return
        [
            new OfflineAccountAuthenticationProvider(),
            new MicrosoftAccountAuthenticationProvider(_microsoftAccountClient),
            new ElyByAccountAuthenticationProvider(_elyByAccountStore, _elyByAuthClient, authlib)
        ];
    }

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
        int ElyByCount,
        IReadOnlyList<string> LoggedOutMicrosoftAccountNames)
    {
        public int TotalCount => Accounts.Count;
        public int LoggedOutMicrosoftCount => LoggedOutMicrosoftAccountNames.Count;
    }
}
