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
using Emerald.CoreX.Installation;
using Emerald.CoreX.Installers;
using Emerald.CoreX.Models;
using Emerald.CoreX.Notifications;
using Emerald.CoreX.Modpacks;
using Emerald.CoreX.Runtime;
using Emerald.CoreX.Store;
using Emerald.CoreX.Store.Modrinth;
using Emerald.CoreX.Store.Modrinth.JSON;
using Emerald.CoreX.Versions;
using Emerald.Services;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;

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
    private readonly IModpackInstanceCreationService _modpackCreationService;
    private readonly IModrinthStore _modPackStore;
    private readonly DispatcherQueue _dispatcherQueue;
    private int _modLoaderLoadRequestId;
    private int _modpackDetailsLoadRequestId;
    private int _modpackProbeRequestId;
    private int _gamesProjectionQueued;
    private int _versionsProjectionQueued;
    private bool _isUpdatingAddGameDefaults;
    private bool _isInitializingModpacks;

    [ObservableProperty]
    private ObservableCollection<Game> _games;

    [ObservableProperty]
    private Game? _selectedGame;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _gamesLoadingMessage = "Loading games...";

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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNormalAddGameMode))]
    [NotifyPropertyChangedFor(nameof(IsModpackAddGameMode))]
    private AddGameMode _selectedAddGameMode = AddGameMode.Normal;

    public ObservableCollection<SearchSortOptionItem> ModpackSortOptions { get; } = [];
    public ObservableCollection<CategoryFilterOption> ModpackCategoryFilters { get; } = [];
    public ObservableCollection<SearchHit> ModpackSearchResults { get; } = [];
    public ObservableCollection<ItemVersion> ModpackVersions { get; } = [];

    [ObservableProperty]
    private SearchSortOptionItem? _selectedModpackSortOption;

    [ObservableProperty]
    private string _modpackSearchQuery = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedModpack))]
    private SearchHit? _selectedModpackSearchResult;

    [ObservableProperty]
    private StoreItem? _selectedModpackItem;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDownloadModpack))]
    private ItemVersion? _selectedModpackVersion;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSearchModpacks))]
    private bool _isSearchingModpacks;

    [ObservableProperty]
    private bool _isLoadingModpackDetails;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDownloadModpack))]
    private bool _isLoadingModpackManifest;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDownloadModpack))]
    private bool _isDownloadingModpack;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasModpackSearchResults))]
    private string _modpackResultsStatusText = "Search Modrinth modpacks.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasModpackProbe))]
    [NotifyPropertyChangedFor(nameof(ModpackMinecraftVersion))]
    [NotifyPropertyChangedFor(nameof(ModpackLoaderDisplayName))]
    [NotifyPropertyChangedFor(nameof(ModpackLoaderVersion))]
    [NotifyPropertyChangedFor(nameof(CanDownloadModpack))]
    private ModpackProbeResult? _modpackProbe;

    public bool IsOnVersionSelectionStep => AddGameWizardStep == 0;

    public bool IsOnModLoaderStep => AddGameWizardStep == 1;

    public bool IsOnGameConfigurationStep => AddGameWizardStep == 2;

    public bool CanGoToPreviousAddGameStep => AddGameWizardStep > 0;

    public bool ShowNormalBackButton => IsNormalAddGameMode && CanGoToPreviousAddGameStep;

    public bool ShowNormalNextButton => IsNormalAddGameMode && !IsOnGameConfigurationStep;

    public bool ShowNormalCreateButton => IsNormalAddGameMode && IsOnGameConfigurationStep;

    public bool IsOnModpackBrowseStep => IsModpackAddGameMode && AddGameWizardStep == 0;

    public bool IsOnModpackVersionStep => IsModpackAddGameMode && AddGameWizardStep == 1;

    public bool IsOnModpackConfigurationStep => IsModpackAddGameMode && AddGameWizardStep == 2;

    public bool ShowModpackBackButton => IsModpackAddGameMode && CanGoToPreviousAddGameStep;

    public bool ShowModpackNextButton => IsModpackAddGameMode && !IsOnModpackConfigurationStep;

    public bool ShowModpackDownloadButton => IsModpackAddGameMode && IsOnModpackConfigurationStep;

    public bool CanGoToNextAddGameStep => IsModpackAddGameMode
        ? AddGameWizardStep switch
        {
            0 => SelectedModpackItem != null && HasModpackVersions && !IsLoadingModpackDetails,
            1 => SelectedModpackVersion != null && ModpackProbe != null && !IsLoadingModpackManifest,
            _ => false
        }
        : AddGameWizardStep switch
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

    public bool IsNormalAddGameMode => SelectedAddGameMode == AddGameMode.Normal;

    public bool IsModpackAddGameMode => SelectedAddGameMode == AddGameMode.Modpacks;

    public bool CanSearchModpacks => !IsSearchingModpacks;

    public bool HasSelectedModpack => SelectedModpackSearchResult != null;

    public bool HasModpackSearchResults => ModpackSearchResults.Count > 0;

    public bool HasModpackVersions => ModpackVersions.Count > 0;

    public bool ShowNoGamesMessage => !IsLoading && FilteredGames.Count == 0;

    public bool IsOfflineMode => _core.IsOfflineMode;

    public bool HasModpackProbe => ModpackProbe != null;

    public string ModpackMinecraftVersion => ModpackProbe?.MinecraftVersion ?? string.Empty;

    public string ModpackLoaderDisplayName => ModpackProbe?.Loader.DisplayName ?? string.Empty;

    public string ModpackLoaderVersion => ModpackProbe?.Loader.Version ?? "Included with Minecraft";

    public string SelectedModpackTitle
        => SelectedModpackItem?.Title ?? SelectedModpackSearchResult?.Title ?? "No modpack selected";

    public string SelectedModpackAuthor
        => SelectedModpackSearchResult?.Author ?? string.Empty;

    public string SelectedModpackSummary
        => SelectedModpackItem?.Description ?? SelectedModpackSearchResult?.Description ?? string.Empty;

    public string SelectedModpackVersionTitle
        => SelectedModpackVersion?.Name ?? "No version selected";

    public string SelectedModpackVersionNumber
        => SelectedModpackVersion?.VersionNumber ?? string.Empty;

    public bool CanDownloadModpack
        => ModpackProbe != null
           && SelectedModpackItem != null
           && SelectedModpackVersion != null
           && IsOnModpackConfigurationStep
           && !string.IsNullOrWhiteSpace(NewGameName)
           && !string.IsNullOrWhiteSpace(NewGameFolderName)
           && !HasFolderValidationMessage
           && !HasFolderConflictWarning
           && !IsDownloadingModpack
           && !IsLoadingModpackManifest;

    public GamesPageViewModel(
        Core core,
        ILogger<GamesPageViewModel> logger,
        INotificationService notificationService,
        ModLoaderRouter modLoaderRouter,
        SettingsService settingsService,
        IGameRuntimeService gameRuntimeService,
        IModpackInstanceCreationService modpackCreationService,
        IEnumerable<IModrinthStore> stores)
    {
        _core = core;
        _logger = logger;
        _notificationService = notificationService;
        _modLoaderRouter = modLoaderRouter;
        _settingsService = settingsService;
        _gameRuntimeService = gameRuntimeService;
        _modpackCreationService = modpackCreationService;
        _modPackStore = stores.First(store => store.ContentType == StoreContentType.ModPack);
        _dispatcherQueue = App.Current.MainWindow.DispatcherQueue;

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
                Type = CoreX.Versions.Type.NeoForge,
                Title = "NeoForge",
                Description = "NeoForgeLoaderDescription".Localize()
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

        ModpackSortOptions.Add(new SearchSortOptionItem(SearchSortOptions.Relevance, "Relevance"));
        ModpackSortOptions.Add(new SearchSortOptionItem(SearchSortOptions.Downloads, "Downloads"));
        ModpackSortOptions.Add(new SearchSortOptionItem(SearchSortOptions.Follows, "Follows"));
        ModpackSortOptions.Add(new SearchSortOptionItem(SearchSortOptions.Updated, "Updated"));
        ModpackSortOptions.Add(new SearchSortOptionItem(SearchSortOptions.Newest, "Newest"));
        SelectedModpackSortOption = ModpackSortOptions.FirstOrDefault();

        _core.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Core.IsOfflineMode))
            {
                _dispatcherQueue.TryEnqueue(() => OnPropertyChanged(nameof(IsOfflineMode)));
            }
        };
        _core.VersionsRefreshed += (_, _) => QueueVersionsProjectionUpdate();
        Games.CollectionChanged += (_, _) => QueueGamesProjectionUpdate();
        AvailableModLoaders.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasAvailableModLoaders));
            OnPropertyChanged(nameof(HasNoAvailableModLoaders));
        };
        ModpackSearchResults.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasModpackSearchResults));
        ModpackVersions.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasModpackVersions));
            NotifyAddGameWizardStateChanged();
        };
        ModpackCategoryFilters.CollectionChanged += ModpackCategoryFilters_CollectionChanged;
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
        _modpackDetailsLoadRequestId++;
        _modpackProbeRequestId++;
        SelectedAddGameMode = AddGameMode.Normal;
        AddGameWizardStep = 0;
        IsCreatingGame = false;
        IsLoadingModLoaders = false;
        IsSearchingModpacks = false;
        IsLoadingModpackDetails = false;
        IsLoadingModpackManifest = false;
        ModpackResultsStatusText = ModpackSearchResults.Count > 0
            ? $"{ModpackSearchResults.Count} modpack(s) found."
            : "Search Modrinth modpacks.";
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
        ResetModpackState(clearResults: false);
        NotifyAddGameWizardStateChanged();
    }

    partial void OnSearchQueryChanged(string value) => UpdateFilteredGames();
    partial void OnIsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowNoGamesMessage));
    }
    partial void OnVersionSearchQueryChanged(string value) => UpdateFilteredAvailableVersions();
    partial void OnSelectedReleaseTypeFilterChanged(string value) => UpdateFilteredAvailableVersions();
    partial void OnSelectedAddGameModeChanged(AddGameMode value)
    {
        if (AddGameWizardStep != 0)
        {
            AddGameWizardStep = 0;
        }

        NotifyAddGameWizardStateChanged();
        RefreshFolderState();
        if (value == AddGameMode.Modpacks)
        {
            _ = InitializeModpackBrowseAsync();
        }
    }

    partial void OnSelectedModpackSortOptionChanged(SearchSortOptionItem? value)
    {
        if (IsModpackAddGameMode)
        {
            _ = SearchModpacksAsync();
        }
    }

    partial void OnModpackSearchQueryChanged(string value)
    {
        if (IsModpackAddGameMode)
        {
            _ = SearchModpacksAsync();
        }
    }

    partial void OnSelectedModpackSearchResultChanged(SearchHit? value)
    {
        OnPropertyChanged(nameof(HasSelectedModpack));
        NotifySelectedModpackStateChanged();
        NotifyAddGameWizardStateChanged();
        _ = LoadSelectedModpackDetailsAsync();
    }

    partial void OnSelectedModpackVersionChanged(ItemVersion? value)
    {
        NotifySelectedModpackStateChanged();
        NotifyAddGameWizardStateChanged();
        _ = ProbeSelectedModpackVersionAsync();
    }

    partial void OnIsSearchingModpacksChanged(bool value)
    {
        SearchModpacksCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsLoadingModpackDetailsChanged(bool value)
    {
        NotifyAddGameWizardStateChanged();
    }

    partial void OnIsLoadingModpackManifestChanged(bool value)
    {
        NotifyAddGameWizardStateChanged();
        NotifyModpackDownloadStateChanged();
    }

    partial void OnIsDownloadingModpackChanged(bool value)
    {
        OnPropertyChanged(nameof(CanDownloadModpack));
        DownloadModpackCommand.NotifyCanExecuteChanged();
    }

    partial void OnModpackProbeChanged(ModpackProbeResult? value)
    {
        NotifySelectedModpackStateChanged();
        NotifyAddGameWizardStateChanged();
        NotifyModpackDownloadStateChanged();
    }

    partial void OnAddGameWizardStepChanged(int value)
    {
        NotifyAddGameWizardStateChanged();
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
        OnPropertyChanged(nameof(CanDownloadModpack));
        DownloadModpackCommand.NotifyCanExecuteChanged();
        RefreshFolderState();
    }

    partial void OnNewGameFolderNameChanged(string value)
    {
        RefreshFolderState();
        OnPropertyChanged(nameof(CurrentGameFolderPathPreview));
        OnPropertyChanged(nameof(HasCurrentGameFolderPathPreview));
        OnPropertyChanged(nameof(CanCreateGame));
        OnPropertyChanged(nameof(CanDownloadModpack));
        DownloadModpackCommand.NotifyCanExecuteChanged();
        NotifyAddGameWizardStateChanged();
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
        OnPropertyChanged(nameof(CanDownloadModpack));
        DownloadModpackCommand.NotifyCanExecuteChanged();
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

        OnPropertyChanged(nameof(ShowNoGamesMessage));

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

    private void QueueGamesProjectionUpdate()
    {
        if (Interlocked.Exchange(ref _gamesProjectionQueued, 1) != 0) return;
        _dispatcherQueue.TryEnqueue(() =>
        {
            Interlocked.Exchange(ref _gamesProjectionQueued, 0);
            UpdateFilteredGames();
            RefreshFolderState();
        });
    }

    private void QueueVersionsProjectionUpdate()
    {
        if (Interlocked.Exchange(ref _versionsProjectionQueued, 1) != 0) return;
        _dispatcherQueue.TryEnqueue(() =>
        {
            Interlocked.Exchange(ref _versionsProjectionQueued, 0);
            UpdateAvailableVersions();
        });
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
            if (!IsDownloadingModpack)
            {
                GamesLoadingMessage = "Loading games...";
                IsLoading = true;
            }

            _logger.LogInformation("Initializing GamesPage");

            _dispatcherQueue.TryEnqueue(() =>
            {
                UpdateAvailableVersions();
                UpdateFilteredGames();
            });
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Failed to initialize GamesPage");
            _dispatcherQueue.TryEnqueue(() =>
                _notificationService.Error("InitializationError", "Failed to initialize games page", ex: ex));
        }
        finally
        {
            if (!IsDownloadingModpack)
            {
                _dispatcherQueue.TryEnqueue(() => IsLoading = false);
            }
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
            _dispatcherQueue.TryEnqueue(() => IsLoadingModLoaders = true);
            _logger.LogInformation("Loading mod loaders for {Version} - Type: {Type}", selectedVersion.BasedOn, selectedType);

            var installer = GetModLoaderInstaller(selectedType);
            if (installer != null)
            {
                var loaders = await installer.GetVersionsAsync(selectedVersion.BasedOn);
                if (requestId != _modLoaderLoadRequestId || SelectedVersion != selectedVersion || SelectedModLoaderType != selectedType)
                {
                    return;
                }

                _dispatcherQueue.TryEnqueue(() =>
                {
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
                });

                _logger.LogInformation(
                    "Loaded {LoaderCount} mod loader option(s) for {Version} using {LoaderType}.",
                    loaders.Count,
                    selectedVersion.BasedOn,
                    selectedType);
            }
            else
            {
                _dispatcherQueue.TryEnqueue(() =>
                {
                    AvailableModLoaders.Clear();
                    SelectedModLoader = null;
                });
                _logger.LogWarning("No mod loader installer was found for {LoaderType}.", selectedType);
            }
        }
        catch (Exception ex)
        {
            if (requestId != _modLoaderLoadRequestId)
            {
                return;
            }

            _dispatcherQueue.TryEnqueue(() =>
            {
                AvailableModLoaders.Clear();
                SelectedModLoader = null;
                _notificationService.Error("ModLoaderError", "Failed to load mod loaders", ex: ex);
            });
            _logger.LogError(ex, "Failed to load mod loaders");
        }
        finally
        {
            if (requestId == _modLoaderLoadRequestId)
            {
                _dispatcherQueue.TryEnqueue(() => IsLoadingModLoaders = false);
            }
        }
    }

    [RelayCommand]
    private async Task InitializeModpackBrowseAsync()
    {
        if (_isInitializingModpacks)
        {
            return;
        }

        try
        {
            _isInitializingModpacks = true;
            await _modPackStore.LoadCategoriesAsync();

            var selectedCategories = ModpackCategoryFilters
                .Where(category => category.IsSelected)
                .Select(category => category.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            _dispatcherQueue.TryEnqueue(() =>
            {
                ModpackCategoryFilters.Clear();
                foreach (var category in _modPackStore.Categories
                             .Select(category => category.name)
                             .Where(name => !string.IsNullOrWhiteSpace(name))
                             .Distinct(StringComparer.OrdinalIgnoreCase)
                             .OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
                {
                    var option = new CategoryFilterOption(category);
                    option.IsSelected = selectedCategories.Contains(category);
                    ModpackCategoryFilters.Add(option);
                }
            });

            if (ModpackSearchResults.Count == 0)
            {
                await SearchModpacksAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize modpack browse state.");
            _dispatcherQueue.TryEnqueue(() =>
                _notificationService.Error("ModpackBrowseInitFailed", "Failed to load Modrinth modpacks.", ex: ex));
        }
        finally
        {
            _isInitializingModpacks = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSearchModpacks))]
    private async Task SearchModpacksAsync()
    {
        try
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                IsSearchingModpacks = true;
                ModpackResultsStatusText = "Searching modpacks...";
            });

            var selectedCategories = ModpackCategoryFilters
                .Where(category => category.IsSelected)
                .Select(category => category.Name)
                .ToArray();

            var response = await _modPackStore.SearchAsync(
                ModpackSearchQuery,
                limit: 30,
                sortOptions: SelectedModpackSortOption?.Value ?? SearchSortOptions.Relevance,
                categories: selectedCategories.Length == 0 ? null : selectedCategories);

            _dispatcherQueue.TryEnqueue(() =>
            {
                ModpackSearchResults.Clear();
                foreach (var hit in response?.Hits ?? [])
                {
                    ModpackSearchResults.Add(hit);
                }

                ResetModpackSelection();
                ModpackResultsStatusText = ModpackSearchResults.Count > 0
                    ? $"{ModpackSearchResults.Count} modpack(s) found."
                    : "No modpacks found.";
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search Modrinth modpacks.");
            _dispatcherQueue.TryEnqueue(() =>
            {
                ModpackResultsStatusText = "Modpack search failed.";
                _notificationService.Error("ModpackSearchFailed", "Failed to search Modrinth modpacks.", ex: ex);
            });
        }
        finally
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                IsSearchingModpacks = false;
                SearchModpacksCommand.NotifyCanExecuteChanged();
                NotifyAddGameWizardStateChanged();
            });
        }
    }

    private async Task LoadSelectedModpackDetailsAsync()
    {
        var requestId = ++_modpackDetailsLoadRequestId;
        _modpackProbeRequestId++;

        _dispatcherQueue.TryEnqueue(() =>
        {
            CleanupModpackProbe();
            SelectedModpackItem = null;
            SelectedModpackVersion = null;
            ModpackVersions.Clear();
        });

        if (SelectedModpackSearchResult == null)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                IsLoadingModpackDetails = false;
                NotifyAddGameWizardStateChanged();
            });
            return;
        }

        try
        {
            _dispatcherQueue.TryEnqueue(() => IsLoadingModpackDetails = true);

            var selectedResult = SelectedModpackSearchResult;
            var item = await _modPackStore.GetItemAsync(selectedResult.ProjectId);
            var versions = await _modPackStore.GetVersionsAsync(selectedResult.ProjectId) ?? [];

            if (requestId != _modpackDetailsLoadRequestId || SelectedModpackSearchResult != selectedResult)
            {
                return;
            }

            _dispatcherQueue.TryEnqueue(() =>
            {
                SelectedModpackItem = item;
                foreach (var version in versions)
                {
                    ApplyModpackCompatibility(version);
                    ModpackVersions.Add(version);
                }
            });
        }
        catch (Exception ex)
        {
            if (requestId != _modpackDetailsLoadRequestId)
            {
                return;
            }

            _logger.LogError(ex, "Failed to load selected modpack details.");
            _dispatcherQueue.TryEnqueue(() =>
                _notificationService.Error("ModpackDetailsFailed", "Failed to load modpack details.", ex: ex));
        }
        finally
        {
            if (requestId == _modpackDetailsLoadRequestId)
            {
                _dispatcherQueue.TryEnqueue(() =>
                {
                    IsLoadingModpackDetails = false;
                    NotifySelectedModpackStateChanged();
                    NotifyAddGameWizardStateChanged();
                });
            }
        }
    }

    private async Task ProbeSelectedModpackVersionAsync()
    {
        var requestId = ++_modpackProbeRequestId;

        _dispatcherQueue.TryEnqueue(CleanupModpackProbe);

        if (SelectedModpackItem == null || SelectedModpackVersion == null)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                IsLoadingModpackManifest = false;
                NotifyAddGameWizardStateChanged();
            });
            return;
        }

        try
        {
            _dispatcherQueue.TryEnqueue(() => IsLoadingModpackManifest = true);

            var selectedItem = SelectedModpackItem;
            var selectedVersion = SelectedModpackVersion;
            var probe = await _modpackCreationService.ProbeAsync(selectedVersion);

            if (requestId != _modpackProbeRequestId
                || SelectedModpackItem != selectedItem
                || SelectedModpackVersion != selectedVersion)
            {
                TryDeleteModpackProbe(probe.MrPackPath);
                return;
            }

            _dispatcherQueue.TryEnqueue(() =>
            {
                ModpackProbe = probe;

                _isUpdatingAddGameDefaults = true;
                try
                {
                    var defaultName = string.IsNullOrWhiteSpace(probe.Manifest.Name)
                        ? SelectedModpackItem.Title
                        : probe.Manifest.Name;

                    NewGameName = defaultName;
                    if (!IsCustomFolderNameEnabled)
                    {
                        NewGameFolderName = SanitizeFolderName(defaultName);
                    }
                }
                finally
                {
                    _isUpdatingAddGameDefaults = false;
                }

                RefreshFolderState();
            });
        }
        catch (Exception ex)
        {
            if (requestId != _modpackProbeRequestId)
            {
                return;
            }

            _logger.LogError(ex, "Failed to inspect selected modpack version.");
            _dispatcherQueue.TryEnqueue(() =>
                _notificationService.Error("ModpackProbeFailed", "Failed to inspect the selected modpack version.", ex: ex));
        }
        finally
        {
            if (requestId == _modpackProbeRequestId)
            {
                _dispatcherQueue.TryEnqueue(() =>
                {
                    IsLoadingModpackManifest = false;
                    NotifyAddGameWizardStateChanged();
                    NotifyModpackDownloadStateChanged();
                });
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanDownloadModpack))]
    public async Task<bool> DownloadModpackAsync()
    {
        if (!CanDownloadModpack || SelectedModpackItem == null || SelectedModpackVersion == null || ModpackProbe == null)
        {
            return false;
        }

        try
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                IsDownloadingModpack = true;
                IsLoading = true;
                GamesLoadingMessage = "Downloading modpack...";
            });

            var request = new ModpackInstanceCreationRequest
            {
                InstanceName = NewGameName.Trim(),
                FolderName = NewGameFolderName.Trim(),
                Project = SelectedModpackItem,
                Version = SelectedModpackVersion,
                MrPackPath = ModpackProbe.MrPackPath
            };

            await _modpackCreationService.CreateAsync(request);

            _dispatcherQueue.TryEnqueue(() =>
            {
                _notificationService.Info("ModpackCreated", $"Successfully created {NewGameName.Trim()}");
                CleanupModpackProbe();
            });

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create modpack instance.");
            _dispatcherQueue.TryEnqueue(() =>
            {
                _notificationService.Error("ModpackCreateFailed", "Failed to create modpack instance.", ex: ex);
                CleanupModpackProbe();
            });
            return false;
        }
        finally
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                IsDownloadingModpack = false;
                IsLoading = false;
                GamesLoadingMessage = "Loading games...";
                OnPropertyChanged(nameof(CanDownloadModpack));
                DownloadModpackCommand.NotifyCanExecuteChanged();
            });
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
            _dispatcherQueue.TryEnqueue(() =>
                _notificationService.Error("InstallError", $"Failed to install {game.Version.DisplayName}", ex: ex));
        }
    }

    [RelayCommand]
    private async Task VerifyGameAsync(Game? game)
    {
        if (game == null) return;
        try
        {
            var report = await _core.VerifyGameAsync(game, IntegrityCheckLevel.Full);
            if (report.State == InstanceInstallationState.Ready)
            {
                _notificationService.Info(
                    "VerificationComplete",
                    string.Format("VerificationCompleteMessage".Localize(), game.Version.DisplayName, report.CheckedFiles));
            }
            else if (report.State == InstanceInstallationState.ReadyWithWarnings)
            {
                _notificationService.Warning(
                    "VerificationWarnings",
                    string.Format("VerificationWarningsMessage".Localize(), game.Version.DisplayName, report.Issues.Count));
            }
            else
            {
                _notificationService.Error(
                    "VerificationFailed",
                    string.Format("VerificationNeedsRepairMessage".Localize(), game.Version.DisplayName, report.Issues.Count));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify game {Game}", game.Version.DisplayName);
            _notificationService.Error("VerificationFailed", $"Could not verify {game.Version.DisplayName}.", ex: ex);
        }
    }

    [RelayCommand]
    private async Task RepairGameAsync(Game? game)
    {
        if (game == null) return;
        try
        {
            var result = await _core.RepairGameAsync(game);
            if (result.Success)
                _notificationService.Info("RepairComplete", $"Repaired {game.Version.DisplayName}.");
            else
                _notificationService.Warning("RepairFailed", result.FailureReason ?? $"Could not repair {game.Version.DisplayName}.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to repair game {Game}", game.Version.DisplayName);
            _notificationService.Error("RepairFailed", $"Could not repair {game.Version.DisplayName}.", ex: ex);
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
            _dispatcherQueue.TryEnqueue(() =>
                _notificationService.Error("LaunchError", $"Failed to launch {game.Version.DisplayName}", ex: ex));
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
            _dispatcherQueue.TryEnqueue(() =>
                _notificationService.Error("StopError", $"Failed to stop {game.Version.DisplayName}", ex: ex));
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
            _dispatcherQueue.TryEnqueue(() =>
                _notificationService.Error("StopError", $"Failed to stop {game.Version.DisplayName}", ex: ex));
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
            _dispatcherQueue.TryEnqueue(() =>
                _notificationService.Error("RemoveError", $"Failed to remove {game.Version.DisplayName}", ex: ex));
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
            _dispatcherQueue.TryEnqueue(() =>
                _notificationService.Error("RemoveError", $"Failed to remove {game.Version.DisplayName}", ex: ex));
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
            NotifyModpackDownloadStateChanged();
            return;
        }

        if (_core.BasePath == null || string.IsNullOrWhiteSpace(NewGameFolderName))
        {
            GameFolderConflictWarningMessage = null;
            NotifyModpackDownloadStateChanged();
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
            NotifyModpackDownloadStateChanged();
            return;
        }

        GameFolderConflictWarningMessage = Directory.Exists(CurrentGameFolderPathPreview)
            ? "GameFolderExistingDirectoryWarning".Localize()
            : null;

        NotifyModpackDownloadStateChanged();
    }

    private void NotifyModpackDownloadStateChanged()
    {
        OnPropertyChanged(nameof(CanDownloadModpack));
        DownloadModpackCommand.NotifyCanExecuteChanged();
    }

    private void NotifyAddGameWizardStateChanged()
    {
        OnPropertyChanged(nameof(IsOnVersionSelectionStep));
        OnPropertyChanged(nameof(IsOnModLoaderStep));
        OnPropertyChanged(nameof(IsOnGameConfigurationStep));
        OnPropertyChanged(nameof(IsOnModpackBrowseStep));
        OnPropertyChanged(nameof(IsOnModpackVersionStep));
        OnPropertyChanged(nameof(IsOnModpackConfigurationStep));
        OnPropertyChanged(nameof(CanGoToPreviousAddGameStep));
        OnPropertyChanged(nameof(CanGoToNextAddGameStep));
        OnPropertyChanged(nameof(ShowNormalBackButton));
        OnPropertyChanged(nameof(ShowNormalNextButton));
        OnPropertyChanged(nameof(ShowNormalCreateButton));
        OnPropertyChanged(nameof(ShowModpackBackButton));
        OnPropertyChanged(nameof(ShowModpackNextButton));
        OnPropertyChanged(nameof(ShowModpackDownloadButton));
        OnPropertyChanged(nameof(CanDownloadModpack));
        GoToNextStepCommand.NotifyCanExecuteChanged();
        GoToPreviousStepCommand.NotifyCanExecuteChanged();
        DownloadModpackCommand.NotifyCanExecuteChanged();
    }

    private void NotifySelectedModpackStateChanged()
    {
        OnPropertyChanged(nameof(HasSelectedModpack));
        OnPropertyChanged(nameof(HasModpackProbe));
        OnPropertyChanged(nameof(ModpackMinecraftVersion));
        OnPropertyChanged(nameof(ModpackLoaderDisplayName));
        OnPropertyChanged(nameof(ModpackLoaderVersion));
        OnPropertyChanged(nameof(SelectedModpackTitle));
        OnPropertyChanged(nameof(SelectedModpackAuthor));
        OnPropertyChanged(nameof(SelectedModpackSummary));
        OnPropertyChanged(nameof(SelectedModpackVersionTitle));
        OnPropertyChanged(nameof(SelectedModpackVersionNumber));
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

    private void ResetModpackState(bool clearResults)
    {
        _modpackDetailsLoadRequestId++;
        _modpackProbeRequestId++;
        CleanupModpackProbe();
        SelectedModpackSearchResult = null;
        SelectedModpackItem = null;
        SelectedModpackVersion = null;
        ModpackVersions.Clear();
        IsLoadingModpackDetails = false;
        IsLoadingModpackManifest = false;
        if (clearResults)
        {
            ModpackSearchResults.Clear();
        }

        NotifySelectedModpackStateChanged();
        NotifyAddGameWizardStateChanged();
    }

    private void ResetModpackSelection()
    {
        _modpackDetailsLoadRequestId++;
        _modpackProbeRequestId++;
        CleanupModpackProbe();
        SelectedModpackSearchResult = null;
        SelectedModpackItem = null;
        SelectedModpackVersion = null;
        ModpackVersions.Clear();
        IsLoadingModpackDetails = false;
        IsLoadingModpackManifest = false;
        NotifySelectedModpackStateChanged();
        NotifyAddGameWizardStateChanged();
    }

    private void CleanupModpackProbe()
    {
        var path = ModpackProbe?.MrPackPath;
        ModpackProbe = null;
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        TryDeleteModpackProbe(path);
    }

    private void TryDeleteModpackProbe(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to clean up temporary modpack file {Path}.", path);
        }
    }

    private void ApplyModpackCompatibility(ItemVersion version)
    {
        var loaderChips = (version.Loaders ?? [])
            .Where(loader => !string.IsNullOrWhiteSpace(loader))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(loader => new StoreTagChip(FormatStoreLabel(loader), false))
            .ToArray();

        var gameVersionChips = (version.GameVersions ?? [])
            .Where(gameVersion => !string.IsNullOrWhiteSpace(gameVersion))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .Select(gameVersion => new StoreTagChip(gameVersion, false))
            .ToArray();

        version.UpdateCompatibilityChips(loaderChips, gameVersionChips);
    }

    private void ModpackCategoryFilters_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (CategoryFilterOption item in e.OldItems)
            {
                item.PropertyChanged -= ModpackCategoryFilter_PropertyChanged;
            }
        }

        if (e.NewItems != null)
        {
            foreach (CategoryFilterOption item in e.NewItems)
            {
                item.PropertyChanged += ModpackCategoryFilter_PropertyChanged;
            }
        }
    }

    private void ModpackCategoryFilter_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CategoryFilterOption.IsSelected) && IsModpackAddGameMode)
        {
            _ = SearchModpacksAsync();
        }
    }

    private static string SanitizeFolderName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Trim()
            .Select(character => invalid.Contains(character) || character is '/' or '\\' ? '_' : character)
            .ToArray();

        var sanitized = new string(chars);
        return string.IsNullOrWhiteSpace(sanitized)
            ? "Modpack"
            : sanitized;
    }

    private static string FormatStoreLabel(string value)
    {
        return value switch
        {
            "neoforge" => "NeoForge",
            "optifine" => "OptiFine",
            "liteloader" => "LiteLoader",
            "datapack" => "Data Pack",
            _ => System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(value.Replace('-', ' ').Replace('_', ' ').ToLowerInvariant())
        };
    }
}

public enum AddGameMode
{
    Normal,
    Modpacks
}
