using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Emerald.CoreX;
using Emerald.CoreX.GameOptions;
using Emerald.CoreX.Helpers;
using Emerald.CoreX.Notifications;
using Microsoft.Extensions.Logging;

namespace Emerald.ViewModels;

public partial class GameOptionsViewModel : ObservableObject
{
    private readonly IMinecraftOptionsService  _service;
    private readonly INotificationService      _notify;
    private readonly ILogger<GameOptionsViewModel> _logger;

    private Game?                             _game;
    private IReadOnlyList<MinecraftOptionEntry> _allEntries = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOptions))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOptions))]
    private bool _optionsFileExists = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptySearch))]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _selectedCategory = "GameOptionsAll".Localize();

    [ObservableProperty]
    private string _gameDisplayName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    private bool _isSaving;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    private bool _hasSaveConflict;

    [ObservableProperty]
    private string _conflictMessage = string.Empty;

    public ObservableCollection<MinecraftOptionEntry> FilteredEntries { get; } = [];
    public ObservableCollection<string>               Categories      { get; } = [];

    public bool HasOptions       => !IsLoading && FilteredEntries.Count > 0 && OptionsFileExists;
    public bool ShowEmptySearch  => !IsLoading && FilteredEntries.Count == 0 && OptionsFileExists
                                    && !string.IsNullOrWhiteSpace(SearchQuery);
    public bool CanSave => HasOptions && !IsSaving && !HasSaveConflict && _allEntries.Any(entry => entry.IsDirty && entry.IsEditable);
    public bool LastSaveSucceeded { get; private set; }

    public GameOptionsViewModel(
        IMinecraftOptionsService service,
        INotificationService notify,
        ILogger<GameOptionsViewModel> logger)
    {
        _service = service;
        _notify  = notify;
        _logger  = logger;
    }

    [RelayCommand]
    public async Task LoadAsync(Game game)
    {
        _game = game;
        GameDisplayName = game.Version.DisplayName;
        IsLoading = true;

        try
        {
            foreach (var entry in _allEntries) entry.PropertyChanged -= Entry_PropertyChanged;
            HasSaveConflict = false;
            ConflictMessage = string.Empty;
            LastSaveSucceeded = false;
            var result = await _service.LoadAsync(game);
            OptionsFileExists = result.OptionsFileExists;
            _allEntries = result.Entries;
            foreach (var entry in _allEntries) entry.PropertyChanged += Entry_PropertyChanged;

            RebuildCategories();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load options for {Name}.", game.Version.DisplayName);
            _notify.Error("GameOptionsLoadError".Localize(),
                string.Format("GameOptionsLoadErrorMessage".Localize(), game.Version.DisplayName), ex: ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task SaveAsync()
    {
        LastSaveSucceeded = false;
        if (_game is null || !CanSave) return;

        try
        {
            IsSaving = true;
            var result = await _service.SaveAsync(_game, _allEntries);
            if (result.Status == MinecraftOptionsSaveStatus.Conflict)
            {
                HasSaveConflict = true;
                ConflictMessage = string.Format("GameOptionsConflictMessage".Localize(), string.Join(", ", result.ConflictingKeys));
                _notify.Warning("GameOptionsConflict".Localize(), ConflictMessage);
                return;
            }
            LastSaveSucceeded = result.Status is MinecraftOptionsSaveStatus.Saved or MinecraftOptionsSaveStatus.NoChanges;
            _notify.Info("GameOptionsSaved".Localize(), string.Format("GameOptionsSavedMessage".Localize(), _game.Version.DisplayName));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save options for {Name}.", _game.Version.DisplayName);
            _notify.Error("GameOptionsSaveError".Localize(),
                string.Format("GameOptionsSaveErrorMessage".Localize(), _game.Version.DisplayName), ex: ex);
        }
        finally
        {
            IsSaving = false;
            OnPropertyChanged(nameof(CanSave));
        }
    }

    [RelayCommand]
    public async Task ReloadAsync()
    {
        if (_game is not null) await LoadAsync(_game);
    }

    partial void OnSearchQueryChanged(string value)        => ApplyFilter();
    partial void OnSelectedCategoryChanged(string value)   => ApplyFilter();

    private void RebuildCategories()
    {
        Categories.Clear();
        Categories.Add("GameOptionsAll".Localize());
        foreach (var cat in _allEntries
            .Select(e => CategoryLabel(e.Category))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(c => c, StringComparer.OrdinalIgnoreCase))
        {
            Categories.Add(cat);
        }
    }

    private void ApplyFilter()
    {
        FilteredEntries.Clear();

        var filtered = _allEntries.AsEnumerable();

        if (SelectedCategory != "GameOptionsAll".Localize())
            filtered = filtered.Where(e => CategoryLabel(e.Category) == SelectedCategory);

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            var q = SearchQuery.Trim();
            filtered = filtered.Where(e =>
                e.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                e.Key.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var entry in filtered)
            FilteredEntries.Add(entry);

        OnPropertyChanged(nameof(HasOptions));
        OnPropertyChanged(nameof(ShowEmptySearch));
        OnPropertyChanged(nameof(CanSave));
    }

    private void Entry_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MinecraftOptionEntry.IsDirty)) OnPropertyChanged(nameof(CanSave));
    }

    private static string CategoryLabel(MinecraftOptionCategory category) =>
        $"GameOptionsCategory{category}".Localize();
}
