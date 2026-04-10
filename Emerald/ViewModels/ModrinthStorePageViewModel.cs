using System.Collections.ObjectModel;
using System.Globalization;
using CmlLib.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Emerald.CoreX;
using Emerald.CoreX.Helpers;
using Emerald.CoreX.Notifications;
using Emerald.CoreX.Store;
using Emerald.CoreX.Store.Modrinth;
using Emerald.CoreX.Store.Modrinth.JSON;
using Emerald.Services;
using GameVersionType = Emerald.CoreX.Versions.Type;

namespace Emerald.ViewModels;

public sealed partial class ModrinthStorePageViewModel : ObservableObject
{
    private static readonly TimeSpan SearchDebounceDelay = TimeSpan.FromMilliseconds(350);

    private readonly Core _core;
    private readonly SettingsService _settingsService;
    private readonly INotificationService _notificationService;
    private readonly IGameStoreContentService _gameStoreContentService;
    private readonly ILogger<ModrinthStorePageViewModel> _logger;
    private readonly Dictionary<StoreContentType, IModrinthStore> _stores;
    private CancellationTokenSource? _searchDebounceCancellationTokenSource;
    private bool _isInitialized;
    private bool _suppressAutoSearch;

    public ObservableCollection<Game> Games { get; } = [];
    public ObservableCollection<StoreContentTypeOption> ContentTypes { get; } = [];
    public ObservableCollection<SearchSortOptionItem> SortOptions { get; } = [];
    public ObservableCollection<CategoryFilterOption> CategoryFilters { get; } = [];
    public ObservableCollection<SearchHit> SearchResults { get; } = [];
    public ObservableCollection<ItemVersion> CompatibleVersions { get; } = [];
    public ObservableCollection<InstalledStoreItem> InstalledItems { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedGame))]
    [NotifyPropertyChangedFor(nameof(CanSearch))]
    [NotifyPropertyChangedFor(nameof(CanInstall))]
    private Game? _selectedGame;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedContentType))]
    private StoreContentTypeOption? _selectedContentTypeOption;

    [ObservableProperty]
    private SearchSortOptionItem? _selectedSortOption;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedSearchResult))]
    [NotifyPropertyChangedFor(nameof(CanInstall))]
    private SearchHit? _selectedSearchResult;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInstall))]
    private StoreItem? _selectedItem;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInstall))]
    private ItemVersion? _selectedVersion;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCompatibilityNotice))]
    private string? _compatibilityNotice;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSearch))]
    private bool _isSearching;

    [ObservableProperty]
    private bool _isLoadingDetails;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInstall))]
    private bool _isInstalling;

    [ObservableProperty]
    private bool _isLoadingInstalledItems;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSearchResults))]
    private string _resultsStatusText = "Search to browse Modrinth projects.";

    public StoreContentType SelectedContentType => SelectedContentTypeOption?.ContentType ?? StoreContentType.Mod;
    public bool HasSelectedGame => SelectedGame != null;
    public bool HasSelectedSearchResult => SelectedSearchResult != null;
    public bool HasSearchResults => SearchResults.Count > 0;
    public bool HasCompatibleVersions => CompatibleVersions.Count > 0;
    public bool HasInstalledItems => InstalledItems.Count > 0;
    public bool HasCompatibilityNotice => !string.IsNullOrWhiteSpace(CompatibilityNotice);
    public bool CanSearch => SelectedGame != null && !IsSearching;
    public bool CanInstall => SelectedGame != null && SelectedItem != null && SelectedVersion != null && !IsInstalling;
    public string SelectedGameInstallTargetName => SelectedGame?.Version.DisplayName ?? "Select a game";
    public string SelectedGameBaseVersion => SelectedGame?.Version.BasedOn ?? string.Empty;
    public string SelectedGameLoaderDisplayName => SelectedGame == null
        ? string.Empty
        : FormatLoaderDisplayName(SelectedGame.Version.Type);

    public ModrinthStorePageViewModel(
        Core core,
        SettingsService settingsService,
        INotificationService notificationService,
        IGameStoreContentService gameStoreContentService,
        IEnumerable<IModrinthStore> stores,
        ILogger<ModrinthStorePageViewModel> logger)
    {
        _core = core;
        _settingsService = settingsService;
        _notificationService = notificationService;
        _gameStoreContentService = gameStoreContentService;
        _logger = logger;
        _stores = stores
            .GroupBy(store => store.ContentType)
            .ToDictionary(group => group.Key, group => group.First());

        ContentTypes.Add(new StoreContentTypeOption(StoreContentType.Mod, "Mods"));
        ContentTypes.Add(new StoreContentTypeOption(StoreContentType.ResourcePack, "Resource Packs"));
        ContentTypes.Add(new StoreContentTypeOption(StoreContentType.DataPack, "Data Packs"));
        ContentTypes.Add(new StoreContentTypeOption(StoreContentType.Shader, "Shaders"));
        ContentTypes.Add(new StoreContentTypeOption(StoreContentType.Plugin, "Plugins"));
        SelectedContentTypeOption = ContentTypes.FirstOrDefault();

        SortOptions.Add(new SearchSortOptionItem(SearchSortOptions.Relevance, "Relevance"));
        SortOptions.Add(new SearchSortOptionItem(SearchSortOptions.Downloads, "Downloads"));
        SortOptions.Add(new SearchSortOptionItem(SearchSortOptions.Follows, "Follows"));
        SortOptions.Add(new SearchSortOptionItem(SearchSortOptions.Updated, "Updated"));
        SortOptions.Add(new SearchSortOptionItem(SearchSortOptions.Newest, "Newest"));
        SelectedSortOption = SortOptions.FirstOrDefault();

        _core.Games.CollectionChanged += (_, _) => SyncGames();
        SearchResults.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasSearchResults));
        CompatibleVersions.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasCompatibleVersions));
        InstalledItems.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasInstalledItems));
        CategoryFilters.CollectionChanged += CategoryFilters_CollectionChanged;
    }

    [RelayCommand]
    private async Task InitializeAsync(object? navigationParameter)
    {
        if (!_core.Initialized && !_core.IsRefreshing)
        {
            var configuredPath = _settingsService.Settings.Minecraft.Path;
            var path = string.IsNullOrWhiteSpace(configuredPath)
                ? new MinecraftPath()
                : new MinecraftPath(configuredPath);
            await _core.InitializeAndRefresh(path);
        }

        SyncGames();
        SelectGameFromNavigation(navigationParameter as string);

        if (SelectedGame == null)
        {
            SelectedGame = Games.FirstOrDefault();
        }

        await LoadCategoriesAsync();
        await RefreshInstalledItemsAsync();
        _isInitialized = true;

        if (SearchResults.Count == 0 && CanSearch)
        {
            await SearchAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(CanSearch))]
    private async Task SearchAsync()
    {
        if (SelectedGame == null)
        {
            return;
        }

        try
        {
            IsSearching = true;
            ResultsStatusText = "Searching...";
            var store = ResolveStore(SelectedContentType);
            store.MCPath = SelectedGame.Path;
            var selectedCategories = CategoryFilters
                .Where(category => category.IsSelected)
                .Select(category => category.Name)
                .ToArray();

            var response = await store.SearchAsync(
                SearchQuery,
                limit: 30,
                sortOptions: SelectedSortOption?.Value ?? SearchSortOptions.Relevance,
                categories: selectedCategories.Length == 0 ? null : selectedCategories);

            SearchResults.Clear();
            foreach (var hit in response?.Hits ?? [])
            {
                SearchResults.Add(hit);
            }

            if (SearchResults.Count > 0)
            {
                SelectedSearchResult = null;
                SelectedItem = null;
                SelectedVersion = null;
                CompatibleVersions.Clear();
                CompatibilityNotice = null;
                ResultsStatusText = $"{SearchResults.Count} project(s) found.";
            }
            else
            {
                SelectedSearchResult = null;
                SelectedItem = null;
                SelectedVersion = null;
                CompatibleVersions.Clear();
                ResultsStatusText = "No projects found.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Store search failed.");
            ResultsStatusText = "Search failed.";
            _notificationService.Error("StoreSearchFailed", "Failed to search Modrinth.", ex: ex);
        }
        finally
        {
            IsSearching = false;
            SearchCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand]
    private async Task RefreshInstalledItemsAsync()
    {
        if (SelectedGame == null)
        {
            InstalledItems.Clear();
            return;
        }

        try
        {
            IsLoadingInstalledItems = true;
            var installed = await _gameStoreContentService.GetInstalledItemsAsync(SelectedGame, SelectedContentType);
            InstalledItems.Clear();
            foreach (var item in installed)
            {
                InstalledItems.Add(item);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load installed store items.");
            _notificationService.Error("StoreItemsLoadFailed", "Failed to load installed items.", ex: ex);
        }
        finally
        {
            IsLoadingInstalledItems = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanInstall))]
    private async Task InstallSelectedVersionAsync()
    {
        if (SelectedGame == null || SelectedItem == null || SelectedVersion == null)
        {
            return;
        }

        var title = $"Installing {SelectedItem.Title}";
        var notification = _notificationService.Create(
            title,
            $"Preparing {SelectedVersion.Name}",
            progress: 0,
            isIndeterminate: false,
            isCancellable: false);

        var progress = new Progress<double>(value =>
        {
            _notificationService.Update(
                notification.Id,
                message: $"Downloading {SelectedVersion.Name}",
                progress: value,
                isIndeterminate: false);
        });

        try
        {
            IsInstalling = true;
            await _gameStoreContentService.InstallAsync(
                SelectedGame,
                SelectedContentType,
                SelectedItem,
                SelectedVersion,
                progress,
                notification.CancellationToken ?? CancellationToken.None);

            _notificationService.Complete(notification.Id, true, $"Installed {SelectedItem.Title}");
            await RefreshInstalledItemsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to install selected store item.");
            _notificationService.Complete(notification.Id, false, "Installation failed.", ex);
        }
        finally
        {
            IsInstalling = false;
            InstallSelectedVersionCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand]
    private async Task RemoveTrackedAsync(InstalledStoreItem? item)
    {
        if (item == null || SelectedGame == null)
        {
            return;
        }

        try
        {
            var removed = await _gameStoreContentService.RemoveAsync(
                SelectedGame,
                SelectedContentType,
                item,
                forceUntracked: false);

            if (removed)
            {
                _notificationService.Info("StoreItemRemoved", $"Removed {item.DisplayName}");
            }
            else
            {
                _notificationService.Warning("StoreItemRemoveSkipped", $"Skipped remove for {item.DisplayName}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove tracked store item.");
            _notificationService.Error("StoreItemRemoveFailed", $"Failed to remove {item.DisplayName}.", ex: ex);
        }

        await RefreshInstalledItemsAsync();
    }

    [RelayCommand]
    private async Task ForceRemoveAsync(InstalledStoreItem? item)
    {
        if (item == null || SelectedGame == null)
        {
            return;
        }

        try
        {
            var removed = await _gameStoreContentService.RemoveAsync(
                SelectedGame,
                SelectedContentType,
                item,
                forceUntracked: true);

            if (removed)
            {
                _notificationService.Info("StoreItemForceRemoved", $"Removed {item.DisplayName}");
            }
            else
            {
                _notificationService.Warning("StoreItemForceRemoveSkipped", $"Skipped remove for {item.DisplayName}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to force remove untracked item.");
            _notificationService.Error("StoreItemForceRemoveFailed", $"Failed to remove {item.DisplayName}.", ex: ex);
        }

        await RefreshInstalledItemsAsync();
    }

    partial void OnSelectedGameChanged(Game? value)
    {
        SearchCommand.NotifyCanExecuteChanged();
        InstallSelectedVersionCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(SelectedGameInstallTargetName));
        OnPropertyChanged(nameof(SelectedGameBaseVersion));
        OnPropertyChanged(nameof(SelectedGameLoaderDisplayName));
        _ = RefreshInstalledItemsAsync();
        if (SelectedSearchResult != null)
        {
            _ = LoadSelectedProjectDetailsAsync();
        }

        QueueAutoSearch();
    }

    partial void OnSelectedContentTypeOptionChanged(StoreContentTypeOption? value)
    {
        _ = HandleContentTypeChangedAsync();
    }

    partial void OnSelectedSortOptionChanged(SearchSortOptionItem? value)
    {
        QueueAutoSearch();
    }

    partial void OnSearchQueryChanged(string value)
    {
        QueueAutoSearch();
    }

    partial void OnSelectedSearchResultChanged(SearchHit? value)
    {
        _ = LoadSelectedProjectDetailsAsync();
    }

    private async Task HandleContentTypeChangedAsync()
    {
        await LoadCategoriesAsync();
        await RefreshInstalledItemsAsync();
        QueueAutoSearch();
    }

    private async Task LoadCategoriesAsync()
    {
        try
        {
            var store = ResolveStore(SelectedContentType);
            await store.LoadCategoriesAsync();

            var selectedCategories = CategoryFilters
                .Where(category => category.IsSelected)
                .Select(category => category.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            _suppressAutoSearch = true;
            CategoryFilters.Clear();
            foreach (var category in store.Categories
                         .Select(category => category.name)
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
            {
                var option = new CategoryFilterOption(category);
                option.IsSelected = selectedCategories.Contains(category);
                CategoryFilters.Add(option);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load store categories for {StoreType}.", SelectedContentType);
            CategoryFilters.Clear();
        }
        finally
        {
            _suppressAutoSearch = false;
        }
    }

    private async Task LoadSelectedProjectDetailsAsync()
    {
        if (SelectedGame == null || SelectedSearchResult == null)
        {
            SelectedItem = null;
            SelectedVersion = null;
            CompatibleVersions.Clear();
            CompatibilityNotice = null;
            return;
        }

        try
        {
            IsLoadingDetails = true;
            var store = ResolveStore(SelectedContentType);
            store.MCPath = SelectedGame.Path;

            SelectedItem = await store.GetItemAsync(SelectedSearchResult.ProjectId);
            var versions = await _gameStoreContentService.GetCompatibleVersionsAsync(
                SelectedGame,
                SelectedContentType,
                SelectedSearchResult.ProjectId);

            CompatibilityNotice = versions.Notice;
            CompatibleVersions.Clear();
            foreach (var version in versions.Versions)
            {
                ApplySelectedGameCompatibility(version);
                CompatibleVersions.Add(version);
            }

            SelectedVersion = CompatibleVersions.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load selected store project details.");
            SelectedItem = null;
            SelectedVersion = null;
            CompatibleVersions.Clear();
            CompatibilityNotice = "Failed to load version compatibility details.";
            _notificationService.Error("StoreItemLoadFailed", "Failed to load item details.", ex: ex);
        }
        finally
        {
            IsLoadingDetails = false;
            InstallSelectedVersionCommand.NotifyCanExecuteChanged();
        }
    }

    private IModrinthStore ResolveStore(StoreContentType contentType)
    {
        if (_stores.TryGetValue(contentType, out var store))
        {
            return store;
        }

        throw new InvalidOperationException($"Store not registered for content type '{contentType}'.");
    }

    private void SyncGames()
    {
        var current = _core.Games.ToList();

        var removed = Games.Where(game => !current.Contains(game)).ToList();
        foreach (var game in removed)
        {
            Games.Remove(game);
        }

        foreach (var game in current)
        {
            if (!Games.Contains(game))
            {
                Games.Add(game);
            }
        }
    }

    private void SelectGameFromNavigation(string? gamePath)
    {
        if (string.IsNullOrWhiteSpace(gamePath))
        {
            return;
        }

        var selected = Games.FirstOrDefault(game =>
            string.Equals(
                NormalizePath(game.Path.BasePath),
                NormalizePath(gamePath),
                StringComparison.OrdinalIgnoreCase));

        if (selected != null)
        {
            SelectedGame = selected;
        }
    }

    private static string NormalizePath(string path)
        => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private void CategoryFilters_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (CategoryFilterOption item in e.OldItems)
            {
                item.PropertyChanged -= CategoryFilterOption_PropertyChanged;
            }
        }

        if (e.NewItems != null)
        {
            foreach (CategoryFilterOption item in e.NewItems)
            {
                item.PropertyChanged += CategoryFilterOption_PropertyChanged;
            }
        }
    }

    private void CategoryFilterOption_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CategoryFilterOption.IsSelected))
        {
            QueueAutoSearch();
        }
    }

    private void QueueAutoSearch()
    {
        if (!_isInitialized || _suppressAutoSearch || SelectedGame == null)
        {
            return;
        }

        _searchDebounceCancellationTokenSource?.Cancel();
        _searchDebounceCancellationTokenSource?.Dispose();
        _searchDebounceCancellationTokenSource = new CancellationTokenSource();
        _ = RunAutoSearchAsync(_searchDebounceCancellationTokenSource.Token);
    }

    private async Task RunAutoSearchAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(SearchDebounceDelay, cancellationToken);

            while (IsSearching)
            {
                await Task.Delay(100, cancellationToken);
            }

            if (cancellationToken.IsCancellationRequested || !CanSearch)
            {
                return;
            }

            await SearchAsync();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void ApplySelectedGameCompatibility(ItemVersion version)
    {
        var selectedGameVersion = SelectedGame?.Version.BasedOn ?? string.Empty;
        var selectedLoaders = ResolveSelectedLoaderAliases();

        var loaderChips = (version.Loaders ?? [])
            .Where(loader => !string.IsNullOrWhiteSpace(loader))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(loader => new StoreTagChip(
                FormatStoreLabel(loader),
                selectedLoaders.Contains(loader, StringComparer.OrdinalIgnoreCase)))
            .ToArray();

        var gameVersionChips = (version.GameVersions ?? [])
            .Where(gameVersion => !string.IsNullOrWhiteSpace(gameVersion))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .Select(gameVersion => new StoreTagChip(
                gameVersion,
                string.Equals(gameVersion, selectedGameVersion, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        version.UpdateCompatibilityChips(loaderChips, gameVersionChips);
    }

    private string[] ResolveSelectedLoaderAliases()
    {
        if (SelectedGame == null)
        {
            return [];
        }

        return SelectedGame.Version.Type switch
        {
            GameVersionType.Fabric => ["fabric"],
            GameVersionType.Forge => ["forge"],
            GameVersionType.Quilt => ["quilt"],
            GameVersionType.LiteLoader => ["liteloader"],
            GameVersionType.OptiFine => ["optifine"],
            _ => ["vanilla"]
        };
    }

    private static string FormatLoaderDisplayName(GameVersionType type)
    {
        return type switch
        {
            GameVersionType.OptiFine => "OptiFine",
            GameVersionType.LiteLoader => "LiteLoader",
            _ => FormatStoreLabel(type.ToString())
        };
    }

    private static string FormatStoreLabel(string value)
    {
        return value switch
        {
            "neoforge" => "NeoForge",
            "optifine" => "OptiFine",
            "liteloader" => "LiteLoader",
            "datapack" => "Data Pack",
            _ => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(value.Replace('-', ' ').Replace('_', ' ').ToLowerInvariant())
        };
    }
}

public sealed class StoreContentTypeOption
{
    public StoreContentTypeOption(StoreContentType contentType, string displayName)
    {
        ContentType = contentType;
        DisplayName = displayName;
    }

    public StoreContentType ContentType { get; }
    public string DisplayName { get; }
}

public sealed class SearchSortOptionItem
{
    public SearchSortOptionItem(SearchSortOptions value, string displayName)
    {
        Value = value;
        DisplayName = displayName;
    }

    public SearchSortOptions Value { get; }
    public string DisplayName { get; }
}

public sealed partial class CategoryFilterOption(string name) : ObservableObject
{
    public string Name { get; } = name;
    public string DisplayName => FormatDisplayName(Name);

    [ObservableProperty]
    private bool _isSelected;

    private static string FormatDisplayName(string name)
    {
        var words = name
            .Replace('_', ' ')
            .Replace('-', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(word.ToLowerInvariant()));

        return string.Join(" ", words);
    }
}
