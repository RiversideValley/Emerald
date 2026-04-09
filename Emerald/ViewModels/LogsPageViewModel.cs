using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Emerald.CoreX.Runtime;

namespace Emerald.ViewModels;

public partial class LogsPageViewModel : ObservableObject
{
    private readonly IGameRuntimeService _gameRuntimeService;
    private GameSession? _observedSession;
    private bool _isRefreshingProjection;

    public ObservableCollection<GameSession> Sessions => _gameRuntimeService.Sessions;

    public ObservableCollection<GameLogEntry> VisibleEntries { get; } = [];

    public ObservableCollection<string> AvailableLevelFilters { get; } =
    [
        "All",
        "Trace",
        "Debug",
        "Info",
        "Warn",
        "Error",
        "Fatal",
        "Unknown"
    ];

    public ObservableCollection<int> AvailablePageSizes { get; } = [100, 250, 500];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedSession))]
    [NotifyPropertyChangedFor(nameof(CanStopSelectedSession))]
    [NotifyPropertyChangedFor(nameof(CanForceStopSelectedSession))]
    [NotifyPropertyChangedFor(nameof(SelectedSessionTitle))]
    [NotifyPropertyChangedFor(nameof(SelectedSessionStatusText))]
    [NotifyPropertyChangedFor(nameof(SelectedCaptureModeText))]
    [NotifyPropertyChangedFor(nameof(LogCaptureNotice))]
    [NotifyPropertyChangedFor(nameof(HasLogCaptureNotice))]
    [NotifyPropertyChangedFor(nameof(HasSelectedSessionEntries))]
    [NotifyPropertyChangedFor(nameof(EmptyStateTitle))]
    [NotifyPropertyChangedFor(nameof(EmptyStateMessage))]
    private GameSession? _selectedSession;

    [ObservableProperty]
    private bool _autoScroll = true;

    [ObservableProperty]
    private bool _hasSessions;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _selectedLevelFilter = "All";

    [ObservableProperty]
    private int _pageSize = 100;

    [ObservableProperty]
    private int _currentPageNumber = 1;

    [ObservableProperty]
    private int _totalPages = 1;

    [ObservableProperty]
    private int _filteredEntryCount;

    public bool HasSelectedSession => SelectedSession != null;

    public bool HasSelectedSessionEntries => SelectedSession?.Entries.Count > 0;

    public bool HasVisibleEntries => VisibleEntries.Count > 0;

    public bool CanStopSelectedSession => SelectedSession?.CanStop ?? false;

    public bool CanForceStopSelectedSession => SelectedSession?.CanForceStop ?? false;

    public bool CanGoPreviousPage => CurrentPageNumber > 1;

    public bool CanGoNextPage => CurrentPageNumber < TotalPages;

    public string SelectedSessionTitle => SelectedSession?.DisplayName ?? "Logs";

    public string? SelectedSessionStatusText => SelectedSession?.StatusText;

    public string? SelectedCaptureModeText => SelectedSession?.CaptureModeText;

    public string? LogCaptureNotice => SelectedSession?.LogCaptureNotice;

    public bool HasLogCaptureNotice => !string.IsNullOrWhiteSpace(LogCaptureNotice);

    public string PageStatusText => FilteredEntryCount == 0
        ? "0 results"
        : $"{FilteredEntryCount:N0} results • Page {CurrentPageNumber:N0} of {TotalPages:N0}";

    public string EmptyStateTitle => HasSelectedSessionEntries ? "No matching logs" : "No log lines yet";

    public string EmptyStateMessage => HasSelectedSessionEntries
        ? "Try adjusting your search or log type filter."
        : "Logs will appear here as full Minecraft events from standard output.";

    public LogsPageViewModel(IGameRuntimeService gameRuntimeService)
    {
        _gameRuntimeService = gameRuntimeService;
        HasSessions = Sessions.Count > 0;
        Sessions.CollectionChanged += Sessions_CollectionChanged;
    }

    [RelayCommand]
    private Task InitializeAsync(object? navigationParameter)
    {
        SelectSession(navigationParameter as string);
        return Task.CompletedTask;
    }

    [RelayCommand(CanExecute = nameof(CanStopSelectedSession))]
    private async Task StopSelectedSessionAsync()
    {
        if (SelectedSession == null)
        {
            return;
        }

        await _gameRuntimeService.StopAsync(SelectedSession.Game, GameStopMode.Gentle);
    }

    [RelayCommand(CanExecute = nameof(CanForceStopSelectedSession))]
    private async Task ForceStopSelectedSessionAsync()
    {
        if (SelectedSession == null)
        {
            return;
        }

        await _gameRuntimeService.StopAsync(SelectedSession.Game, GameStopMode.Force);
    }

    [RelayCommand(CanExecute = nameof(CanGoPreviousPage))]
    private void GoToPreviousPage()
    {
        if (CanGoPreviousPage)
        {
            CurrentPageNumber--;
        }
    }

    [RelayCommand(CanExecute = nameof(CanGoNextPage))]
    private void GoToNextPage()
    {
        if (CanGoNextPage)
        {
            CurrentPageNumber++;
        }
    }

    public void SelectSession(string? gamePath)
    {
        GameSession? preferred = null;
        if (!string.IsNullOrWhiteSpace(gamePath))
        {
            preferred = _gameRuntimeService.FindLatestSession(gamePath);
        }

        if (preferred != null)
        {
            SelectedSession = preferred;
            return;
        }

        if (SelectedSession != null && Sessions.Contains(SelectedSession))
        {
            return;
        }

        SelectedSession = Sessions.FirstOrDefault();
    }

    public string? GetSelectedSessionClipboardText()
        => SelectedSession?.ToClipboardText();

    partial void OnSearchQueryChanged(string value)
        => RefreshVisibleEntries(GameLogProjectionRefreshReason.FilterChanged);

    partial void OnSelectedLevelFilterChanged(string value)
        => RefreshVisibleEntries(GameLogProjectionRefreshReason.FilterChanged);

    partial void OnPageSizeChanged(int value)
        => RefreshVisibleEntries(GameLogProjectionRefreshReason.FilterChanged);

    partial void OnAutoScrollChanged(bool value)
    {
        if (value)
        {
            RefreshVisibleEntries(GameLogProjectionRefreshReason.FilterChanged);
        }
    }

    partial void OnCurrentPageNumberChanged(int value)
    {
        if (!_isRefreshingProjection)
        {
            RefreshVisibleEntries(GameLogProjectionRefreshReason.PageChanged);
        }
    }

    partial void OnSelectedSessionChanged(GameSession? value)
    {
        if (_observedSession != null)
        {
            _observedSession.PropertyChanged -= SelectedSession_PropertyChanged;
            _observedSession.Entries.CollectionChanged -= SelectedEntries_CollectionChanged;
        }

        _observedSession = value;

        if (_observedSession != null)
        {
            _observedSession.PropertyChanged += SelectedSession_PropertyChanged;
            _observedSession.Entries.CollectionChanged += SelectedEntries_CollectionChanged;
        }

        OnPropertyChanged(nameof(CanStopSelectedSession));
        OnPropertyChanged(nameof(CanForceStopSelectedSession));
        OnPropertyChanged(nameof(SelectedSessionTitle));
        OnPropertyChanged(nameof(SelectedSessionStatusText));
        OnPropertyChanged(nameof(SelectedCaptureModeText));
        OnPropertyChanged(nameof(LogCaptureNotice));
        OnPropertyChanged(nameof(HasLogCaptureNotice));
        OnPropertyChanged(nameof(HasSelectedSessionEntries));
        RefreshVisibleEntries(GameLogProjectionRefreshReason.SessionChanged);
        StopSelectedSessionCommand.NotifyCanExecuteChanged();
        ForceStopSelectedSessionCommand.NotifyCanExecuteChanged();
    }

    private void Sessions_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        HasSessions = Sessions.Count > 0;

        if (SelectedSession == null || !Sessions.Contains(SelectedSession))
        {
            SelectedSession = Sessions.FirstOrDefault();
        }
        else
        {
            RefreshVisibleEntries(GameLogProjectionRefreshReason.EntriesChanged);
        }
    }

    private void SelectedSession_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(CanStopSelectedSession));
        OnPropertyChanged(nameof(CanForceStopSelectedSession));
        OnPropertyChanged(nameof(SelectedSessionStatusText));
        OnPropertyChanged(nameof(SelectedCaptureModeText));
        OnPropertyChanged(nameof(LogCaptureNotice));
        OnPropertyChanged(nameof(HasLogCaptureNotice));
        StopSelectedSessionCommand.NotifyCanExecuteChanged();
        ForceStopSelectedSessionCommand.NotifyCanExecuteChanged();
    }

    private void SelectedEntries_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasSelectedSessionEntries));

        var reason = e.Action == NotifyCollectionChangedAction.Add && e.NewItems?.Count > 0
            ? GameLogProjectionRefreshReason.LiveEntriesChanged
            : GameLogProjectionRefreshReason.EntriesChanged;

        RefreshVisibleEntries(reason);
    }

    private void RefreshVisibleEntries(GameLogProjectionRefreshReason reason)
    {
        var projection = GameLogProjectionBuilder.Build(
            SelectedSession?.Entries ?? [],
            SearchQuery,
            SelectedLevelFilter,
            PageSize,
            CurrentPageNumber,
            AutoScroll,
            reason,
            FilteredEntryCount);

        _isRefreshingProjection = true;
        try
        {
            TotalPages = projection.TotalPages;
            CurrentPageNumber = projection.CurrentPageNumber;
            FilteredEntryCount = projection.FilteredEntryCount;
        }
        finally
        {
            _isRefreshingProjection = false;
        }

        ReplaceVisibleEntries(projection.VisibleEntries);
        NotifyProjectionStateChanged();
    }

    private void ReplaceVisibleEntries(IEnumerable<GameLogEntry> entries)
    {
        VisibleEntries.Clear();
        foreach (var entry in entries)
        {
            VisibleEntries.Add(entry);
        }
    }

    private void NotifyProjectionStateChanged()
    {
        OnPropertyChanged(nameof(HasVisibleEntries));
        OnPropertyChanged(nameof(HasSelectedSessionEntries));
        OnPropertyChanged(nameof(CanGoPreviousPage));
        OnPropertyChanged(nameof(CanGoNextPage));
        OnPropertyChanged(nameof(PageStatusText));
        OnPropertyChanged(nameof(EmptyStateTitle));
        OnPropertyChanged(nameof(EmptyStateMessage));
        GoToPreviousPageCommand.NotifyCanExecuteChanged();
        GoToNextPageCommand.NotifyCanExecuteChanged();
    }
}
