using System.Collections.ObjectModel;
using Emerald.CoreX.Helpers;
using Emerald.CoreX.Models;
using Emerald.CoreX.Notifications;
using Emerald.CoreX.Services.Auth;
using Emerald.Services;
using Microsoft.Extensions.Logging;

namespace Emerald.CoreX.Services;

public sealed partial class AccountService : IAccountService
{
    private readonly ILogger<AccountService> _logger;
    private readonly IBaseSettingsService _settingsService;
    private readonly IUiDispatcher _uiDispatcher;
    private readonly INotificationService? _notificationService;
    private readonly string _accountStorePath;
    private readonly IReadOnlyDictionary<string, IAccountProvider> _providers;
    private readonly IReadOnlyList<AccountProviderDescriptor> _providerDescriptors;

    // Protects mutations of _accounts and _selectedAccountId.
    private readonly SemaphoreSlim _gate = new(1, 1);

    // Serializes account reloads independently from other mutations.
    private readonly SemaphoreSlim _loadGate = new(1, 1);

    private readonly ObservableCollection<EAccount> _accounts = new();

    private readonly object _initLock = new();
    private Task? _initializationTask;

    private string? _selectedAccountId;

    public AccountService(
        ILogger<AccountService> logger,
        IBaseSettingsService settingsService,
        IUiDispatcher uiDispatcher,
        IEnumerable<IAccountProvider> providers,
        string? accountStorePath = null,
        INotificationService? notificationService = null)
    {
        _logger = logger;
        _settingsService = settingsService;
        _uiDispatcher = uiDispatcher;
        _notificationService = notificationService;
        _accountStorePath = string.IsNullOrWhiteSpace(accountStorePath)
            ? GetDefaultAccountStorePath()
            : accountStorePath;
        ArgumentNullException.ThrowIfNull(providers);
        var registeredProviders = providers.ToList();
        ValidateProviderRegistrations(registeredProviders);
        var duplicateProviderId = registeredProviders
            .GroupBy(provider => provider.Descriptor.ProviderId, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicateProviderId is not null)
            throw new ArgumentException($"Account provider ID '{duplicateProviderId}' is registered more than once.", nameof(providers));
        _providers = registeredProviders.ToDictionary(provider => provider.Descriptor.ProviderId, StringComparer.Ordinal);
        _providerDescriptors = registeredProviders.Select(provider => provider.Descriptor).ToArray();
        _selectedAccountId = _settingsService.Get<string?>(SettingsKeys.SelectedMinecraftAccount, null);
    }

    public ObservableCollection<EAccount> Accounts => _accounts;
    public IReadOnlyList<AccountProviderDescriptor> Providers => _providerDescriptors;

    public Task InitializeAsync()
    {
        lock (_initLock)
        {
            if (_initializationTask is null || _initializationTask.IsFaulted)
            {
                _initializationTask = InitializeCoreAsync();
            }

            return _initializationTask;
        }
    }

    private async Task InitializeCoreAsync()
    {
        _logger.LogInformation(
            "Initializing AccountService (accountStorePath={AccountStorePath}).",
            _accountStorePath);

        try
        {
            var context = new AccountProviderInitializationContext(_accountStorePath);
            foreach (var provider in _providers.Values)
                await provider.InitializeAsync(context).ConfigureAwait(false);
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
            task = InitializeAsync();

        await task.ConfigureAwait(false);
    }

    private static string GetDefaultAccountStorePath()
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Emerald",
            "accounts",
            "cml_accounts.json");

    private static void ValidateProviderRegistrations(IReadOnlyList<IAccountProvider> providers)
    {
        if (providers.Count == 0 || providers.Any(provider =>
                provider is null || string.IsNullOrWhiteSpace(provider.Descriptor?.ProviderId)))
        {
            throw new ArgumentException(
                "At least one account provider with a non-empty ProviderId is required.",
                nameof(providers));
        }

        foreach (var provider in providers)
        {
            var descriptor = provider.Descriptor;
            if (string.IsNullOrWhiteSpace(descriptor.DisplayName))
                throw new ArgumentException($"Account provider '{descriptor.ProviderId}' must have a display name.", nameof(providers));

            if (descriptor.SignInMethods.Any(method => string.IsNullOrWhiteSpace(method.MethodId)))
                throw new ArgumentException($"Account provider '{descriptor.ProviderId}' has a blank sign-in method ID.", nameof(providers));

            var duplicateMethodId = descriptor.SignInMethods
                .GroupBy(method => method.MethodId, StringComparer.Ordinal)
                .FirstOrDefault(group => group.Count() > 1)?.Key;
            if (duplicateMethodId is not null)
            {
                throw new ArgumentException(
                    $"Account provider '{descriptor.ProviderId}' exposes sign-in method '{duplicateMethodId}' more than once.",
                    nameof(providers));
            }

            if (descriptor.EffectiveRequirements.Any(requirement => string.IsNullOrWhiteSpace(requirement.ProviderId)))
                throw new ArgumentException($"Account provider '{descriptor.ProviderId}' has a blank requirement provider ID.", nameof(providers));
        }
    }

}
