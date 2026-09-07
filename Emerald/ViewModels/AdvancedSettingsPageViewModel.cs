using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Emerald.CoreX.Helpers;
using Emerald.CoreX.Notifications;
using Emerald.CoreX.Services.Auth.Authlib;
using Emerald.Services;
using Microsoft.Extensions.Logging;

namespace Emerald.ViewModels;

public sealed partial class AdvancedSettingsPageViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;
    private readonly IAuthlibInjectorService _authlibService;
    private readonly INotificationService _notifications;
    private readonly ILogger<AdvancedSettingsPageViewModel> _logger;
    private bool _initialized;

    public AdvancedSettingsPageViewModel(
        SettingsService settingsService,
        IAuthlibInjectorService authlibService,
        AuthlibInjectorOptions options,
        INotificationService notifications,
        ILogger<AdvancedSettingsPageViewModel> logger)
    {
        _settingsService = settingsService;
        _authlibService = authlibService;
        _notifications = notifications;
        _logger = logger;
        ModeOptions =
        [
            new(AuthlibInjectorVersionMode.Recommended,
                string.Format("AuthlibModeRecommendedFormat".Localize(), options.RecommendedVersion)),
            new(AuthlibInjectorVersionMode.Latest, "AuthlibModeLatest".Localize()),
            new(AuthlibInjectorVersionMode.Custom, "AuthlibModeCustom".Localize())
        ];
        _selectedMode = ModeOptions.First(option => option.Mode == Settings.VersionMode);
    }

    public IReadOnlyList<AuthlibModeOption> ModeOptions { get; }
    public ObservableCollection<AuthlibVersionOption> Versions { get; } = [];
    private Helpers.Settings.JSON.AuthlibInjectorSettings Settings
        => _settingsService.Settings.App.Advanced.AuthlibInjector;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCustomMode))]
    private AuthlibModeOption _selectedMode;

    [ObservableProperty]
    private AuthlibVersionOption? _selectedVersion;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCheckConfiguration))]
    private bool _isCatalogLoading;

    [ObservableProperty]
    private bool _hasCatalogError;

    [ObservableProperty]
    private string _catalogErrorMessage = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCheckConfiguration))]
    private bool _isChecking;

    [ObservableProperty]
    private string _configurationStatus = string.Empty;

    public bool IsCustomMode => SelectedMode.Mode == AuthlibInjectorVersionMode.Custom;
    public bool CanCheckConfiguration => !IsChecking && !IsCatalogLoading
        && (!IsCustomMode || SelectedVersion is not null);

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;
        await LoadCatalogAsync();
    }

    partial void OnSelectedModeChanged(AuthlibModeOption value)
    {
        Settings.VersionMode = value.Mode;
        ConfigurationStatus = string.Empty;
        OnPropertyChanged(nameof(CanCheckConfiguration));
        CheckConfigurationCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedVersionChanged(AuthlibVersionOption? value)
    {
        if (value is not null)
            Settings.CustomVersion = value.Version;
        ConfigurationStatus = string.Empty;
        OnPropertyChanged(nameof(CanCheckConfiguration));
        CheckConfigurationCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task LoadCatalogAsync()
    {
        IsCatalogLoading = true;
        HasCatalogError = false;
        CatalogErrorMessage = string.Empty;
        try
        {
            var versions = await _authlibService.GetAvailableVersionsAsync();
            Versions.Clear();
            foreach (var version in versions
                         .GroupBy(item => item.Version, StringComparer.Ordinal)
                         .Select(group => group.OrderByDescending(item => item.BuildNumber).First())
                         .OrderByDescending(item => item.BuildNumber))
            {
                Versions.Add(new AuthlibVersionOption(version.BuildNumber, version.Version,
                    string.Format("AuthlibVersionFormat".Localize(), version.Version, version.BuildNumber)));
            }

            if (!string.IsNullOrWhiteSpace(Settings.CustomVersion))
            {
                SelectedVersion = Versions.FirstOrDefault(option =>
                    string.Equals(option.Version, Settings.CustomVersion, StringComparison.Ordinal));
                if (SelectedVersion is null)
                {
                    SelectedVersion = new AuthlibVersionOption(0, Settings.CustomVersion,
                        string.Format("AuthlibSavedVersionUnavailableFormat".Localize(), Settings.CustomVersion));
                    Versions.Insert(0, SelectedVersion);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load the authlib-injector version catalog.");
            HasCatalogError = true;
            CatalogErrorMessage = "AuthlibCatalogError".Localize();
            if (!string.IsNullOrWhiteSpace(Settings.CustomVersion))
            {
                SelectedVersion = new AuthlibVersionOption(0, Settings.CustomVersion,
                    string.Format("AuthlibSavedVersionUnavailableFormat".Localize(), Settings.CustomVersion));
                Versions.Clear();
                Versions.Add(SelectedVersion);
            }
        }
        finally
        {
            IsCatalogLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCheckConfiguration))]
    private async Task CheckConfigurationAsync()
    {
        IsChecking = true;
        ConfigurationStatus = "AuthlibChecking".Localize();
        try
        {
            var result = await _authlibService.PrepareLaunchAsync();
            ConfigurationStatus = string.Format("AuthlibCheckSuccessFormat".Localize(),
                result.Version, result.BuildNumber, result.ApiRoot.GetLeftPart(UriPartial.Path));
            _notifications.Info("AuthlibCheckSuccessTitle".Localize(), ConfigurationStatus);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Authlib-injector configuration check failed.");
            ConfigurationStatus = string.Format("AuthlibCheckFailureFormat".Localize(), ex.Message);
            _notifications.Error("AuthlibCheckFailureTitle".Localize(), ConfigurationStatus, ex: ex);
        }
        finally
        {
            IsChecking = false;
            CheckConfigurationCommand.NotifyCanExecuteChanged();
        }
    }

    partial void OnIsCatalogLoadingChanged(bool value)
        => CheckConfigurationCommand.NotifyCanExecuteChanged();

    partial void OnIsCheckingChanged(bool value)
        => CheckConfigurationCommand.NotifyCanExecuteChanged();
}

public sealed record AuthlibModeOption(AuthlibInjectorVersionMode Mode, string Label);
public sealed record AuthlibVersionOption(int BuildNumber, string Version, string Label);
