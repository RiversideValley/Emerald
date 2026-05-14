using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Emerald.CoreX;
using Emerald.CoreX.GameOptions;
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
    private bool _optionsFileExists = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptySearch))]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _selectedCategory = "All";

    [ObservableProperty]
    private string _gameDisplayName = string.Empty;

    public ObservableCollection<MinecraftOptionEntry> FilteredEntries { get; } = [];
    public ObservableCollection<string>               Categories      { get; } = [];

    public bool HasOptions       => !IsLoading && FilteredEntries.Count > 0 && OptionsFileExists;
    public bool ShowEmptySearch  => !IsLoading && FilteredEntries.Count == 0 && OptionsFileExists
                                    && !string.IsNullOrWhiteSpace(SearchQuery);

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
            var result = await _service.LoadAsync(game);
            OptionsFileExists = result.OptionsFileExists;
            _allEntries = result.Entries;

            RebuildCategories();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load options for {Name}.", game.Version.DisplayName);
            _notify.Error("OptionsLoadError",
                $"Failed to load options for {game.Version.DisplayName}.", ex: ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        if (_game is null || _allEntries.Count == 0) return;

        try
        {
            await _service.SaveAsync(_game, _allEntries);
            _notify.Info("OptionsSaved", $"Saved options for {_game.Version.DisplayName}.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save options for {Name}.", _game.Version.DisplayName);
            _notify.Error("OptionsSaveError",
                $"Failed to save options for {_game.Version.DisplayName}.", ex: ex);
        }
    }

    partial void OnSearchQueryChanged(string value)        => ApplyFilter();
    partial void OnSelectedCategoryChanged(string value)   => ApplyFilter();

    private void RebuildCategories()
    {
        Categories.Clear();
        Categories.Add("All");
        foreach (var cat in _allEntries
                     .Select(e => e.Category)
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

        if (SelectedCategory != "All")
            filtered = filtered.Where(e => e.Category == SelectedCategory);

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
    }
}
