using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CmlLib.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using Emerald.CoreX;
using Emerald.CoreX.Helpers;
using Emerald.CoreX.Installers;
using Emerald.CoreX.Models;
using Emerald.CoreX.Notifications;
using Emerald.CoreX.Runtime;
using Emerald.CoreX.Versions;
using Emerald.Services;
using Microsoft.Extensions.Logging;

namespace Emerald.ViewModels;

/// <summary>
/// Manages the games page workflow for listing, creating, launching, and stopping game instances.
/// </summary>
public partial class GamesPageViewModel : ObservableObject
{
    private const string LatestLoaderTag = "latest";

    private readonly Core _core;
    private readonly ILogger<GamesPageViewModel> _logger;
    private readonly INotificationService _notificationService;
    private readonly SettingsService _settingsService;
    private readonly ModLoaderRouter _modLoaderRouter;
    private readonly IGameRuntimeService _gameRuntimeService;
    private int _modLoaderLoadRequestId;
    private bool _isUpdatingAddGameDefaults;

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

    // Add Game dialog state
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOnVersionSelectionStep))]
    [NotifyPropertyChangedFor(nameof(IsOnModLoaderStep))]
    [NotifyPropertyChangedFor(nameof(IsOnGameConfigurationStep))]
    [NotifyPropertyChangedFor(nameof(CanGoToPreviousAddGameStep))]
    [NotifyPropertyChangedFor(nameof(CanGoToNextAddGameStep))]
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
    [NotifyPropertyChangedFor(nameof(CanGoToNextAddGameStep))]
    [NotifyPropertyChangedFor(nameof(AddGameSelectedVersionSummary))]
    [NotifyPropertyChangedFor(nameof(AddGameSelectedVersionReleaseType))]
    private CoreX.Versions.Version? _selectedVersion;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoToNextAddGameStep))]
    [NotifyPropertyChangedFor(nameof(CanCreateGame))]
    private LoaderInfo? _selectedModLoader;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsModLoaderSelectionVisible))]
    [NotifyPropertyChangedFor(nameof(HasAvailableModLoaders))]
    [NotifyPropertyChangedFor(nameof(HasNoAvailableModLoaders))]
    [NotifyPropertyChangedFor(nameof(CanCreateGame))]
    private CoreX.Versions.Type _selectedModLoaderType = CoreX.Versions.Type.Vanilla;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCreateGame))]
    private string _newGameName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCreateGame))]
    [NotifyPropertyChangedFor(nameof(CurrentGameFolderPathPreview))]
    [NotifyPropertyChangedFor(nameof(HasCurrentGameFolderPathPreview))]
    private string _newGameFolderName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFolderNameReadOnly))]
    private bool _isCustomFolderNameEnabled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFolderValidationMessage))]
    [NotifyPropertyChangedFor(nameof(CanCreateGame))]
    private string? _gameFolderValidationMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFolderConflictWarning))]
    private string? _gameFolderConflictWarningMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAvailableModLoaders))]
    [NotifyPropertyChangedFor(nameof(HasNoAvailableModLoaders))]
    private bool _isLoadingModLoaders;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCreateGame))]
    private bool _isCreatingGame;

    [ObservableProperty]
    private ObservableCollection<AddGameModLoaderTypeOption> _modLoaderTypes;

    [ObservableProperty]
    private AddGameModLoaderTypeOption? _selectedModLoaderTypeOption;

    public bool IsOnVersionSelectionStep => AddGameWizardStep == 0;

    public bool IsOnModLoaderStep => AddGameWizardStep == 1;

    public bool IsOnGameConfigurationStep => AddGameWizardStep == 2;

    public bool CanGoToPreviousAddGameStep => AddGameWizardStep > 0;

    public bool CanGoToNextAddGameStep => AddGameWizardStep switch
    {
        0 => SelectedVersion != null,
        1 => SelectedModLoaderType == CoreX.Versions.Type.Vanilla || SelectedModLoader != null,
        _ => false
    };

    public bool IsModLoaderSelectionVisible => SelectedModLoaderType != CoreX.Versions.Type.Vanilla;

    public bool HasAvailableModLoaders => AvailableModLoaders.Count > 0;

    public bool HasNoAvailableModLoaders => IsModLoaderSelectionVisible && !IsLoadingModLoaders && !HasAvailableModLoaders;

    public bool IsFolderNameReadOnly => !IsCustomFolderNameEnabled;

    public bool HasFolderValidationMessage => !string.IsNullOrWhiteSpace(GameFolderValidationMessage);

    public bool HasFolderConflictWarning => !string.IsNullOrWhiteSpace(GameFolderConflictWarningMessage);

    public string AddGameSelectedVersionSummary => SelectedVersion?.BasedOn ?? "ChooseAVersion".Localize();

    public string AddGameSelectedVersionReleaseType => SelectedVersion?.ReleaseType ?? string.Empty;

    public string CurrentGameFolderPathPreview
        => _core.BasePath == null || string.IsNullOrWhiteSpace(NewGameFolderName)
            ? string.Empty
            : Path.Combine(_core.BasePath.BasePath, Core.GamesFolderName, NewGameFolderName.Trim());

    public bool HasCurrentGameFolderPathPreview => !string.IsNullOrWhiteSpace(CurrentGameFolderPathPreview);

    public bool CanCreateGame
        => SelectedVersion != null
           && !string.IsNullOrWhiteSpace(NewGameName)
           && !string.IsNullOrWhiteSpace(NewGameFolderName)
           && !HasFolderValidationMessage
           && !IsCreatingGame
           && (SelectedModLoaderType == CoreX.Versions.Type.Vanilla || SelectedModLoader != null);

    public GamesPageViewModel(
        Core core,
        ILogger<GamesPageViewModel> logger,
        INotificationService notificationService,
        ModLoaderRouter modLoaderRouter,
        SettingsService settingsService,
        IGameRuntimeService gameRuntimeService)
    {
        _core = core;
        _logger = logger;
        _notificationService = notificationService;
        _modLoaderRouter = modLoaderRouter;
        _settingsService = settingsService;
        _gameRuntimeService = gameRuntimeService;
        Games = _core.Games;
        FilteredGames = new ObservableCollection<Game>(Games);
        AvailableVersions = new ObservableCollection<CoreX.Versions.Version>();
        FilteredAvailableVersions = new ObservableCollection<CoreX.Versions.Version>();
        AvailableModLoaders = new ObservableCollection<LoaderInfo>();
        ModLoaderTypes = new ObservableCollection<AddGameModLoaderTypeOption>(
        [
            new()
            {
                Type = CoreX.Versions.Type.Vanilla,
                Title = "Vanilla".Localize(),
                Description = "VanillaLoaderDescription".Localize()
            },
            new()
            {
                Type = CoreX.Versions.Type.Fabric,
                Title = "Fabric",
                Description = "FabricLoaderDescription".Localize()
            },
            new()
            {
                Type = CoreX.Versions.Type.Forge,
                Title = "Forge",
                Description = "ForgeLoaderDescription".Localize()
            },
            new()
            {
                Type = CoreX.Versions.Type.Quilt,
                Title = "Quilt",
                Description = "QuiltLoaderDescription".Localize()
            },
            new()
            {
                Type = CoreX.Versions.Type.OptiFine,
                Title = "OptiFine",
                Description = "OptiFineLoaderDescription".Localize()
            },
            new()
            {
                Type = CoreX.Versions.Type.LiteLoader,
                Title = "LiteLoader",
                Description = "LiteLoaderDescription".Localize()
            }
        ]);
        SelectedModLoaderTypeOption = ModLoaderTypes.FirstOrDefault(option => option.Type == CoreX.Versions.Type.Vanilla);

        _core.PropertyChanged += (_, _) => this.OnPropertyChanged();
        _core.VersionsRefreshed += (_, _) => UpdateAvailableVersions();
        Games.CollectionChanged += (_, _) =>
        {
            UpdateFilteredGames();
            RefreshFolderState();
        };
        AvailableModLoaders.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasAvailableModLoaders));
            OnPropertyChanged(nameof(HasNoAvailableModLoaders));
        };
    }

    [RelayCommand]
    private void GoToNextStep()
    {
        if (!CanGoToNextAddGameStep)
        {
            return;
        }

        AddGameWizardStep++;
    }

    [RelayCommand]
    private void GoToPreviousStep()
    {
        if (AddGameWizardStep == 0)
        {
            return;
        }

        AddGameWizardStep--;
    }

    [RelayCommand]
    private void StartAddGame()
    {
        _logger.LogDebug("Resetting add-game wizard state.");
        _modLoaderLoadRequestId++;
        AddGameWizardStep = 0;
        IsCreatingGame = false;
        IsLoadingModLoaders = false;
        IsCustomFolderNameEnabled = false;
        NewGameName = string.Empty;
        NewGameFolderName = string.Empty;
        GameFolderValidationMessage = null;
        GameFolderConflictWarningMessage = null;
        SelectedVersion = null;
        SelectedModLoader = null;
        SelectedModLoaderType = CoreX.Versions.Type.Vanilla;
        SelectedModLoaderTypeOption = ModLoaderTypes.FirstOrDefault(option => option.Type == SelectedModLoaderType);
        VersionSearchQuery = string.Empty;
        SelectedReleaseTypeFilter = "All";
        AvailableModLoaders.Clear();
    }

    partial void OnSearchQueryChanged(string value) => UpdateFilteredGames();
    partial void OnVersionSearchQueryChanged(string value) => UpdateFilteredAvailableVersions();
    partial void OnSelectedReleaseTypeFilterChanged(string value) => UpdateFilteredAvailableVersions();
    partial void OnAddGameWizardStepChanged(int value)
    {
        OnPropertyChanged(nameof(IsOnVersionSelectionStep));
        OnPropertyChanged(nameof(IsOnModLoaderStep));
        OnPropertyChanged(nameof(IsOnGameConfigurationStep));
        OnPropertyChanged(nameof(CanGoToPreviousAddGameStep));
        OnPropertyChanged(nameof(CanGoToNextAddGameStep));
    }

    partial void OnSelectedVersionChanged(CoreX.Versions.Version? value)
    {
        OnPropertyChanged(nameof(AddGameSelectedVersionSummary));
        OnPropertyChanged(nameof(AddGameSelectedVersionReleaseType));
        OnPropertyChanged(nameof(CanGoToNextAddGameStep));

        if (value != null)
        {
            _isUpdatingAddGameDefaults = true;
            try
            {
                NewGameName = value.BasedOn;
                if (!IsCustomFolderNameEnabled)
                {
                    NewGameFolderName = value.BasedOn;
                }
            }
            finally
            {
                _isUpdatingAddGameDefaults = false;
            }
        }

        RefreshFolderState();

        if (IsModLoaderSelectionVisible)
        {
            _ = LoadModLoadersAsync();
        }
    }

    partial void OnNewGameNameChanged(string value)
    {
        if (!IsCustomFolderNameEnabled && !_isUpdatingAddGameDefaults)
        {
            _isUpdatingAddGameDefaults = true;
            try
            {
                NewGameFolderName = value;
            }
            finally
            {
                _isUpdatingAddGameDefaults = false;
            }
        }

        OnPropertyChanged(nameof(CanCreateGame));
        RefreshFolderState();
    }

    partial void OnNewGameFolderNameChanged(string value)
    {
        RefreshFolderState();
        OnPropertyChanged(nameof(CurrentGameFolderPathPreview));
        OnPropertyChanged(nameof(HasCurrentGameFolderPathPreview));
        OnPropertyChanged(nameof(CanCreateGame));
    }

    partial void OnIsCustomFolderNameEnabledChanged(bool value)
    {
        if (!value)
        {
            _isUpdatingAddGameDefaults = true;
            try
            {
                NewGameFolderName = NewGameName;
            }
            finally
            {
                _isUpdatingAddGameDefaults = false;
            }
        }

        RefreshFolderState();
    }

    partial void OnSelectedModLoaderTypeOptionChanged(AddGameModLoaderTypeOption? value)
    {
        if (value != null && SelectedModLoaderType != value.Type)
        {
            SelectedModLoaderType = value.Type;
        }
    }

    partial void OnSelectedModLoaderTypeChanged(CoreX.Versions.Type value)
    {
        var matchingOption = ModLoaderTypes.FirstOrDefault(option => option.Type == value);
        if (matchingOption != null && !ReferenceEquals(SelectedModLoaderTypeOption, matchingOption))
        {
            SelectedModLoaderTypeOption = matchingOption;
        }

        SelectedModLoader = null;
        OnPropertyChanged(nameof(IsModLoaderSelectionVisible));
        OnPropertyChanged(nameof(HasAvailableModLoaders));
        OnPropertyChanged(nameof(HasNoAvailableModLoaders));
        OnPropertyChanged(nameof(CanGoToNextAddGameStep));
        OnPropertyChanged(nameof(CanCreateGame));

        _ = LoadModLoadersAsync();
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

        _logger.LogDebug(
            "Updated filtered games. SearchQueryEmpty: {SearchQueryEmpty}. VisibleGames: {VisibleGames}. TotalGames: {TotalGames}.",
            string.IsNullOrWhiteSpace(SearchQuery),
            FilteredGames.Count,
            Games.Count);
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

        _logger.LogDebug(
            "Updated filtered versions. SearchQueryEmpty: {SearchQueryEmpty}. ReleaseTypeFilter: {ReleaseTypeFilter}. VisibleVersions: {VisibleVersions}.",
            string.IsNullOrWhiteSpace(VersionSearchQuery),
            SelectedReleaseTypeFilter,
            FilteredAvailableVersions.Count);
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
                ReleaseTypes.Add(FormatReleaseTypeLabel(type));
            }
        }

        UpdateFilteredAvailableVersions();
        _logger.LogDebug("Updated available versions list. VersionCount: {VersionCount}.", AvailableVersions.Count);
    }

    private static string FormatReleaseTypeLabel(string releaseType)
    {
        if (string.IsNullOrWhiteSpace(releaseType))
        {
            return string.Empty;
        }

        return char.ToUpperInvariant(releaseType[0]) + releaseType[1..];
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
            UpdateAvailableVersions();
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
            _logger.LogInformation("Refreshing games list.");
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
            _logger.LogDebug(
                "Skipping mod loader load. HasSelectedVersion: {HasSelectedVersion}. SelectedType: {SelectedType}.",
                SelectedVersion != null,
                SelectedModLoaderType);
            _modLoaderLoadRequestId++;
            IsLoadingModLoaders = false;
            AvailableModLoaders.Clear();
            SelectedModLoader = null;
            return;
        }

        var selectedVersion = SelectedVersion;
        var selectedType = SelectedModLoaderType;
        var requestId = ++_modLoaderLoadRequestId;

        try
        {
            IsLoadingModLoaders = true;
            _logger.LogInformation("Loading mod loaders for {Version} - Type: {Type}", selectedVersion.BasedOn, selectedType);

            var installer = GetModLoaderInstaller(selectedType);
            if (installer != null)
            {
                var loaders = await installer.GetVersionsAsync(selectedVersion.BasedOn);
                if (requestId != _modLoaderLoadRequestId || SelectedVersion != selectedVersion || SelectedModLoaderType != selectedType)
                {
                    return;
                }

                AvailableModLoaders.Clear();
                if (loaders.Count > 0)
                {
                    AvailableModLoaders.Add(new LoaderInfo
                    {
                        Tag = LatestLoaderTag,
                        Version = "LatestLoaderLabel".Localize(),
                        Stable = true
                    });

                    foreach (var loader in loaders)
                    {
                        AvailableModLoaders.Add(loader);
                    }

                    SelectedModLoader = AvailableModLoaders.FirstOrDefault();
                }
                else
                {
                    SelectedModLoader = null;
                }

                _logger.LogInformation(
                    "Loaded {LoaderCount} mod loader option(s) for {Version} using {LoaderType}.",
                    AvailableModLoaders.Count,
                    selectedVersion.BasedOn,
                    selectedType);
            }
            else
            {
                AvailableModLoaders.Clear();
                SelectedModLoader = null;
                _logger.LogWarning("No mod loader installer was found for {LoaderType}.", selectedType);
            }
        }
        catch (Exception ex)
        {
            if (requestId != _modLoaderLoadRequestId)
            {
                return;
            }

            AvailableModLoaders.Clear();
            SelectedModLoader = null;
            _logger.LogError(ex, "Failed to load mod loaders");
            _notificationService.Error("ModLoaderError", "Failed to load mod loaders", ex: ex);
        }
        finally
        {
            if (requestId == _modLoaderLoadRequestId)
            {
                IsLoadingModLoaders = false;
            }
        }
    }

    public async Task<bool> SubmitAddGameAsync()
    {
        if (SelectedVersion == null || string.IsNullOrWhiteSpace(NewGameName))
        {
            _logger.LogWarning(
                "Cannot create game because the selected version or new game name is missing. HasVersion: {HasVersion}. HasName: {HasName}.",
                SelectedVersion != null,
                !string.IsNullOrWhiteSpace(NewGameName));
            _notificationService.Warning("InvalidInput", "Please select a version and enter a name");
            return false;
        }

        if (HasFolderValidationMessage)
        {
            _notificationService.Warning("InvalidGameFolderName", GameFolderValidationMessage ?? "InvalidGameFolderNameMessage".Localize());
            return false;
        }

        try
        {
            IsCreatingGame = true;
            _logger.LogInformation("Creating new game: {Name}", NewGameName);

            var modVer = SelectedModLoader?.Tag == LatestLoaderTag ? null : SelectedModLoader?.Version;

            var version = new CoreX.Versions.Version
            {
                DisplayName = NewGameName.Trim(),
                BasedOn = SelectedVersion.BasedOn,
                Type = SelectedModLoaderType,
                ReleaseType = SelectedVersion.ReleaseType,
                ModVersion = modVer
            };

            _core.AddGame(version, NewGameFolderName.Trim());

            _notificationService.Info("GameCreated", $"Successfully created {NewGameName.Trim()}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create game");
            _notificationService.Error("CreateGameError", "Failed to create game", ex: ex);
            return false;
        }
        finally
        {
            IsCreatingGame = false;
        }
    }

    [RelayCommand]
    private async Task CreateGameAsync()
        => await SubmitAddGameAsync();

    // Unchanged methods below...
    [RelayCommand]
    private async Task InstallGameAsync(Game? game)
    {
        if (game == null)
        {
            _logger.LogDebug("Ignoring install request because no game was provided.");
            return;
        }
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

    public async Task LaunchGameAsync(Game? game, EAccount? account = null)
    {
        if (game == null)
        {
            _logger.LogDebug("Ignoring launch request because no game was provided.");
            return;
        }
        try
        {
            _logger.LogInformation("Launching game: {Name}", game.Version.DisplayName);
            await _gameRuntimeService.LaunchAsync(game, account);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to launch game");
            _notificationService.Error("LaunchError", $"Failed to launch {game.Version.DisplayName}", ex: ex);
        }
    }

    [RelayCommand]
    private async Task StopGameAsync(Game? game)
    {
        if (game == null)
        {
            _logger.LogDebug("Ignoring stop request because no game was provided.");
            return;
        }

        try
        {
            _logger.LogInformation("Stopping game: {Name}", game.Version.DisplayName);
            await _gameRuntimeService.StopAsync(game, GameStopMode.Gentle);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop game");
            _notificationService.Error("StopError", $"Failed to stop {game.Version.DisplayName}", ex: ex);
        }
    }

    [RelayCommand]
    private async Task ForceStopGameAsync(Game? game)
    {
        if (game == null)
        {
            _logger.LogDebug("Ignoring force-stop request because no game was provided.");
            return;
        }

        try
        {
            _logger.LogInformation("Force stopping game: {Name}", game.Version.DisplayName);
            await _gameRuntimeService.StopAsync(game, GameStopMode.Force);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to force stop game");
            _notificationService.Error("StopError", $"Failed to stop {game.Version.DisplayName}", ex: ex);
        }
    }

    [RelayCommand]
    private void RemoveGame(Game? game)
    {
        if (game == null)
        {
            _logger.LogDebug("Ignoring remove request because no game was provided.");
            return;
        }
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
        if (game == null)
        {
            _logger.LogDebug("Ignoring remove-with-files request because no game was provided.");
            return;
        }
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
        var installers = Ioc.Default.GetServices<IModLoaderInstaller>();
        var installer = installers.FirstOrDefault(x => x.Type == type);
        _logger.LogDebug("Resolved mod loader installer for {LoaderType}. FoundInstaller: {FoundInstaller}.", type, installer != null);
        return installer;
    }

    private void RefreshFolderState()
    {
        var validationMessage = ValidateFolderName(NewGameFolderName);
        GameFolderValidationMessage = validationMessage;

        if (validationMessage != null)
        {
            GameFolderConflictWarningMessage = null;
            return;
        }

        if (_core.BasePath == null || string.IsNullOrWhiteSpace(NewGameFolderName))
        {
            GameFolderConflictWarningMessage = null;
            return;
        }

        var normalizedTargetPath = NormalizePath(CurrentGameFolderPathPreview);
        var conflictingGame = Games.FirstOrDefault(game =>
            string.Equals(NormalizePath(game.Path.BasePath), normalizedTargetPath, StringComparison.OrdinalIgnoreCase));

        if (conflictingGame != null)
        {
            GameFolderConflictWarningMessage = string.Format(
                "GameFolderUsedByExistingGameMessage".Localize(),
                conflictingGame.Version.DisplayName);
            return;
        }

        GameFolderConflictWarningMessage = Directory.Exists(CurrentGameFolderPathPreview)
            ? "GameFolderExistingDirectoryWarning".Localize()
            : null;
    }

    private static string? ValidateFolderName(string? folderName)
    {
        if (string.IsNullOrWhiteSpace(folderName))
        {
            return "GameFolderNameRequired".Localize();
        }

        var trimmedFolderName = folderName.Trim();
        if (trimmedFolderName == "." || trimmedFolderName == "..")
        {
            return "GameFolderNameInvalidSegment".Localize();
        }

        if (trimmedFolderName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return "GameFolderNameInvalidCharacters".Localize();
        }

        if (trimmedFolderName.Contains(Path.DirectorySeparatorChar) || trimmedFolderName.Contains(Path.AltDirectorySeparatorChar))
        {
            return "GameFolderNameSingleSegmentOnly".Localize();
        }

        return null;
    }

    private static string NormalizePath(string path)
    {
        try
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            return path;
        }
    }
}
