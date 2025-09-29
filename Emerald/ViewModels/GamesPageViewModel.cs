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

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private ObservableCollection<Game> _filteredGames;



    // For Add Game Dialog


    [ObservableProperty]
    private ObservableCollection<CoreX.Versions.Version> _availableVersions;

    [ObservableProperty]
    private ObservableCollection<LoaderInfo> _availableModLoaders;


    [NotifyPropertyChangedFor(nameof(IsCreateButtonEnabled))]
    [ObservableProperty]
    private CoreX.Versions.Version? _selectedVersion;

    [NotifyPropertyChangedFor(nameof(IsCreateButtonEnabled))]
    [ObservableProperty]
    private LoaderInfo? _selectedModLoader;

    [NotifyPropertyChangedFor(nameof(IsCreateButtonEnabled))]
    [ObservableProperty]
    private CoreX.Versions.Type _selectedModLoaderType = CoreX.Versions.Type.Vanilla;

    [NotifyPropertyChangedFor(nameof(IsCreateButtonEnabled))]
    [ObservableProperty]
    private string _newGameName = string.Empty;



    public bool IsCreateButtonEnabled =>
        !string.IsNullOrWhiteSpace(NewGameName) &&
        SelectedVersion != null &&
        (SelectedModLoaderType == CoreX.Versions.Type.Vanilla || SelectedModLoader != null);

    public GamesPageViewModel(Core core, ILogger<GamesPageViewModel> logger, INotificationService notificationService, IAccountService accountService, ModLoaderRouter modLoaderRouter, SettingsService settingsService)
    {
        _core = core;
        _logger = logger;
        _notificationService = notificationService;
        _accountService = accountService;
        _modLoaderRouter =modLoaderRouter;
        _settingsService = settingsService;
        Games = _core.Games;
        FilteredGames = new ObservableCollection<Game>(Games);
        AvailableVersions = new ObservableCollection<CoreX.Versions.Version>();
        AvailableModLoaders = new ObservableCollection<LoaderInfo>();

        // Subscribe to collection changes
        Games.CollectionChanged += (s, e) => UpdateFilteredGames();
    }

    partial void OnSearchQueryChanged(string value)
    {
        UpdateFilteredGames();
    }

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

    [RelayCommand]
    private async Task InitializeAsync()
    {
        try
        {
            IsLoading = true;
            _logger.LogInformation("Initializing GamesPage");

            if (!_core.Initialized)
            {
                var path = _settingsService.Settings.Minecraft.Path;

                var mcPath = path != null ? new MinecraftPath(path) : new();
                await _core.InitializeAndRefresh(mcPath);
            }

            AvailableVersions.Clear();
            foreach (var version in _core.VanillaVersions)
            {
                AvailableVersions.Add(version);
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
            IsRefreshing = true;
            _logger.LogInformation("Refreshing games list");

            await _core.InitializeAndRefresh();
            UpdateFilteredGames();

            _notificationService.Info("RefreshComplete", "Games list has been refreshed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh games");
            _notificationService.Error("RefreshError", "Failed to refresh games", ex: ex);
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private void StartAddGame()
    {
        NewGameName = string.Empty;
        SelectedVersion = null;
        SelectedModLoader = null;
        SelectedModLoaderType = CoreX.Versions.Type.Vanilla;
        AvailableModLoaders.Clear();
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
            _logger.LogInformation("Loading mod loaders for {Version} - Type: {Type}",
                SelectedVersion.BasedOn, SelectedModLoaderType);

            var installer = GetModLoaderInstaller(SelectedModLoaderType);
            if (installer != null)
            {
                var loaders = await installer.GetVersionsAsync(SelectedVersion.BasedOn);

                AvailableModLoaders.Clear();

                // Add "Latest" option
                AvailableModLoaders.Add(new LoaderInfo
                {
                    Tag = "Latest",
                    Version = "Latest Available",
                    Stable = true
                     
                });

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
                Metadata = SelectedVersion.Metadata,
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
            var process = await game.BuildProcess(game.Version.BasedOn, session);

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

            // Show confirmation dialog here if needed
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

        return Installers.First(x => x.Type == type);
    }
}
