using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CmlLib.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using Emerald.CoreX;
using Emerald.CoreX.Helpers;
using Emerald.CoreX.Installers;
using Emerald.CoreX.Notifications;
using Emerald.CoreX.Services;
using Emerald.CoreX.Versions;
using Emerald.Services;
using Microsoft.Extensions.Logging;

namespace Emerald.ViewModels;

public partial class GamesPageViewModel : ObservableObject
{
    private readonly Core _core;
    private readonly ILogger<GamesPageViewModel> _logger;
    private readonly INotificationService _notificationService;
    private readonly IAccountService _accountService;
    private readonly SettingsService _settingsService;
    private readonly ModLoaderRouter _modLoaderRouter;

    [ObservableProperty]
    private ObservableCollection<Game> _games;

    [ObservableProperty]
    private Game? _selectedGame;

    [ObservableProperty]
    private bool _isLoading;

    private bool IsRefreshing => _core.IsRefreshing;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private ObservableCollection<Game> _filteredGames;

    // For Add Game Dialog Wizard
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPrimaryButtonEnabled))]
    private int _addGameWizardStep = 0;

    [ObservableProperty]
    private ObservableCollection<CoreX.Versions.Version> _availableVersions;

    [ObservableProperty]
    private ObservableCollection<CoreX.Versions.Version> _filteredAvailableVersions;

    [ObservableProperty]
    private string _versionSearchQuery = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> _releaseTypes = new();

    [ObservableProperty]
    private string _selectedReleaseTypeFilter = "All";

    [ObservableProperty]
    private ObservableCollection<LoaderInfo> _availableModLoaders;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPrimaryButtonEnabled))]
    private CoreX.Versions.Version? _selectedVersion;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPrimaryButtonEnabled))]
    private LoaderInfo? _selectedModLoader;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPrimaryButtonEnabled))]
    private CoreX.Versions.Type _selectedModLoaderType = CoreX.Versions.Type.Vanilla;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPrimaryButtonEnabled))]
    private string _newGameName = string.Empty;

    // This single property now controls the primary button's state across all steps
    public bool IsPrimaryButtonEnabled
    {
        get
        {
            return AddGameWizardStep switch
            {
                0 => SelectedVersion != null,
                1 => !string.IsNullOrWhiteSpace(NewGameName) && (SelectedModLoaderType == CoreX.Versions.Type.Vanilla || SelectedModLoader != null),
                _ => false,
            };
        }
    }

    public GamesPageViewModel(Core core, ILogger<GamesPageViewModel> logger, INotificationService notificationService, IAccountService accountService, ModLoaderRouter modLoaderRouter, SettingsService settingsService)
    {
        _core = core;
        _logger = logger;
        _notificationService = notificationService;
        _accountService = accountService;
        _modLoaderRouter = modLoaderRouter;
        _settingsService = settingsService;
        Games = _core.Games;
        FilteredGames = new ObservableCollection<Game>(Games);
        AvailableVersions = new ObservableCollection<CoreX.Versions.Version>();
        FilteredAvailableVersions = new ObservableCollection<CoreX.Versions.Version>();
        AvailableModLoaders = new ObservableCollection<LoaderInfo>();

        _core.PropertyChanged += (_, _) => this.OnPropertyChanged();
        _core.VersionsRefreshed += (_, _) => UpdateAvailableVersions();
        Games.CollectionChanged += (_, _) => UpdateFilteredGames();
    }

    [RelayCommand]
    private void GoToNextStep() => AddGameWizardStep++;

    [RelayCommand]
    private void GoToPreviousStep() => AddGameWizardStep--;

    [RelayCommand]
    private void StartAddGame()
    {
        AddGameWizardStep = 0;
        NewGameName = string.Empty;
        SelectedVersion = null;
        SelectedModLoader = null;
        SelectedModLoaderType = CoreX.Versions.Type.Vanilla;
        VersionSearchQuery = string.Empty;
        SelectedReleaseTypeFilter = "All";
        AvailableModLoaders.Clear();
    }

    partial void OnSearchQueryChanged(string value) => UpdateFilteredGames();
    partial void OnVersionSearchQueryChanged(string value) => UpdateFilteredAvailableVersions();
    partial void OnSelectedReleaseTypeFilterChanged(string value) => UpdateFilteredAvailableVersions();

    private void UpdateFilteredGames()
    {
        var filtered = string.IsNullOrWhiteSpace(SearchQuery)
            ? Games
            : Games.Where(g =>
                g.Version.DisplayName.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                g.Version.BasedOn.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase));

        FilteredGames.Clear();
        foreach (var game in filtered)
        {
            FilteredGames.Add(game);
        }
    }

    private void UpdateFilteredAvailableVersions()
    {
        var filtered = AvailableVersions.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(VersionSearchQuery))
        {
            filtered = filtered.Where(v => v.BasedOn.Contains(VersionSearchQuery, StringComparison.OrdinalIgnoreCase));
        }

        if (SelectedReleaseTypeFilter != "All")
        {
            filtered = filtered.Where(v => v.ReleaseType.Equals(SelectedReleaseTypeFilter, StringComparison.OrdinalIgnoreCase));
        }

        FilteredAvailableVersions.Clear();
        foreach (var version in filtered.OrderByDescending(v => v?.ReleaseTime ?? DateTime.MinValue))
        {
            FilteredAvailableVersions.Add(version);
        }
    }

    private void UpdateAvailableVersions()
    {
        AvailableVersions.Clear();
        foreach (var version in _core.VanillaVersions)
        {
            AvailableVersions.Add(version);
        }

        // Populate release types for filtering
        ReleaseTypes.Clear();
        ReleaseTypes.Add("All");
        var distinctTypes = AvailableVersions.Select(v => v.ReleaseType).Distinct().OrderBy(t => t);
        foreach (var type in distinctTypes)
        {
            if (!string.IsNullOrWhiteSpace(type))
            {
                ReleaseTypes.Add(type);
            }
        }

        UpdateFilteredAvailableVersions();
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        try
        {
            IsLoading = true;
            _logger.LogInformation("Initializing GamesPage");

            if (!_core.Initialized && !_core.IsRefreshing)
            {
                var path = _settingsService.Settings.Minecraft.Path;
                var mcPath = path != null ? new MinecraftPath(path) : new();
                await _core.InitializeAndRefresh(mcPath);
            }

            UpdateFilteredGames();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize GamesPage");
            _notificationService.Error("InitializationError", "Failed to initialize games page", ex: ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshGamesAsync()
    {
        try
        {
            await _core.InitializeAndRefresh();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh games");
        }
    }

    [RelayCommand]
    private async Task LoadModLoadersAsync()
    {
        if (SelectedVersion == null || SelectedModLoaderType == CoreX.Versions.Type.Vanilla)
        {
            AvailableModLoaders.Clear();
            return;
        }

        try
        {
            IsLoading = true;
            _logger.LogInformation("Loading mod loaders for {Version} - Type: {Type}", SelectedVersion.BasedOn, SelectedModLoaderType);

            var installer = GetModLoaderInstaller(SelectedModLoaderType);
            if (installer != null)
            {
                var loaders = await installer.GetVersionsAsync(SelectedVersion.BasedOn);
                AvailableModLoaders.Clear();
                AvailableModLoaders.Add(new LoaderInfo { Tag = "Latest", Version = "Latest Available", Stable = true });
                foreach (var loader in loaders)
                {
                    AvailableModLoaders.Add(loader);
                }
                SelectedModLoader = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load mod loaders");
            _notificationService.Error("ModLoaderError", "Failed to load mod loaders", ex: ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task CreateGameAsync()
    {
        if (SelectedVersion == null || string.IsNullOrWhiteSpace(NewGameName))
        {
            _notificationService.Warning("InvalidInput", "Please select a version and enter a name");
            return;
        }

        try
        {
            IsLoading = true;
            _logger.LogInformation("Creating new game: {Name}", NewGameName);

            var modVer = SelectedModLoader?.Tag == "latest" ? null : SelectedModLoader?.Version;

            var version = new CoreX.Versions.Version
            {
                DisplayName = NewGameName,
                BasedOn = SelectedVersion.BasedOn,
                Type = SelectedModLoaderType,
                ReleaseType = SelectedVersion.ReleaseType,
                ModVersion = modVer
            };

            _core.AddGame(version);

            _notificationService.Info("GameCreated", $"Successfully created {NewGameName}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create game");
            _notificationService.Error("CreateGameError", "Failed to create game", ex: ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    // Unchanged methods below...
    [RelayCommand]
    private async Task InstallGameAsync(Game? game)
    {
        if (game == null) return;
        try
        {
            _logger.LogInformation("Installing game: {Name}", game.Version.DisplayName);
            await _core.InstallGame(game, showFileprog: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to install game");
            _notificationService.Error("InstallError", $"Failed to install {game.Version.DisplayName}", ex: ex);
        }
    }

    [RelayCommand]
    private async Task LaunchGameAsync(Game? game)
    {
        if (game == null) return;
        try
        {
            _logger.LogInformation("Launching game: {Name}", game.Version.DisplayName);
            var account = _accountService.GetMostRecentlyUsedAccount();
            if (account == null)
            {
                _notificationService.Warning("NoAccount", "Please sign in to an account first");
                return;
            }
            var session = await _accountService.AuthenticateAccountAsync(account);
            var process = await game.BuildProcess(game.Version.RealVersion, session);
            process.Start();
            _notificationService.Info("GameLaunched", $"Launched {game.Version.DisplayName}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to launch game");
            _notificationService.Error("LaunchError", $"Failed to launch {game.Version.DisplayName}", ex: ex);
        }
    }

    [RelayCommand]
    private void RemoveGame(Game? game)
    {
        if (game == null) return;
        try
        {
            _logger.LogInformation("Removing game: {Name}", game.Version.DisplayName);
            _core.RemoveGame(game, deleteFolder: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove game");
            _notificationService.Error("RemoveError", $"Failed to remove {game.Version.DisplayName}", ex: ex);
        }
    }

    [RelayCommand]
    private async Task RemoveGameWithFilesAsync(Game? game)
    {
        if (game == null) return;
        try
        {
            _logger.LogInformation("Removing game with files: {Name}", game.Version.DisplayName);
            await Task.Run(() => _core.RemoveGame(game, deleteFolder: true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove game with files");
            _notificationService.Error("RemoveError", $"Failed to remove {game.Version.DisplayName}", ex: ex);
        }
    }

    private IModLoaderInstaller? GetModLoaderInstaller(CoreX.Versions.Type type)
    {
        var Installers = Ioc.Default.GetServices<IModLoaderInstaller>();
        return Installers.FirstOrDefault(x => x.Type == type);
    }
}
