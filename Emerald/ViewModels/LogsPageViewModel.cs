using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Emerald.CoreX.Runtime;

namespace Emerald.ViewModels;

/// <summary>
/// Presents tracked runtime sessions and the current filtered log projection for the logs page.
/// </summary>
public partial class LogsPageViewModel : ObservableObject
{
    private readonly IGameRuntimeService _gameRuntimeService;
    private readonly ILogger<LogsPageViewModel> _logger;
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

    /// <summary>
    /// Initializes the logs page viewmodel and starts observing session changes.
    /// </summary>
    public LogsPageViewModel(IGameRuntimeService gameRuntimeService, ILogger<LogsPageViewModel> logger)
    {
        _gameRuntimeService = gameRuntimeService;
        _logger = logger;
        HasSessions = Sessions.Count > 0;
        Sessions.CollectionChanged += Sessions_CollectionChanged;
        _logger.LogInformation("Logs page viewmodel initialized. ExistingSessions: {SessionCount}.", Sessions.Count);
    }

    [RelayCommand]
    private Task InitializeAsync(object? navigationParameter)
    {
        _logger.LogInformation(
            "Initializing logs page. HasNavigationPath: {HasNavigationPath}.",
            navigationParameter is string gamePath && !string.IsNullOrWhiteSpace(gamePath));
        SelectSession(navigationParameter as string);
        return Task.CompletedTask;
    }

    [RelayCommand(CanExecute = nameof(CanStopSelectedSession))]
    private async Task StopSelectedSessionAsync()
    {
        if (SelectedSession == null)
        {
            _logger.LogDebug("Ignoring stop request because no session is selected.");
            return;
        }

        _logger.LogInformation("Stopping selected session {SessionName}.", SelectedSession.DisplayName);
        await _gameRuntimeService.StopAsync(SelectedSession.Game, GameStopMode.Gentle);
    }

    [RelayCommand(CanExecute = nameof(CanForceStopSelectedSession))]
    private async Task ForceStopSelectedSessionAsync()
    {
        if (SelectedSession == null)
        {
            _logger.LogDebug("Ignoring force-stop request because no session is selected.");
            return;
        }

        _logger.LogInformation("Force stopping selected session {SessionName}.", SelectedSession.DisplayName);
        await _gameRuntimeService.StopAsync(SelectedSession.Game, GameStopMode.Force);
    }

    [RelayCommand(CanExecute = nameof(CanGoPreviousPage))]
    private void GoToPreviousPage()
    {
        if (CanGoPreviousPage)
        {
            _logger.LogDebug("Moving to previous logs page from {CurrentPage}.", CurrentPageNumber);
            CurrentPageNumber--;
        }
    }

    [RelayCommand(CanExecute = nameof(CanGoNextPage))]
    private void GoToNextPage()
    {
        if (CanGoNextPage)
        {
            _logger.LogDebug("Moving to next logs page from {CurrentPage}.", CurrentPageNumber);
            CurrentPageNumber++;
        }
    }

    /// <summary>
    /// Selects the newest matching session for the supplied game path, or falls back to the first available session.
    /// </summary>
    public void SelectSession(string? gamePath)
    {
        GameSession? preferred = null;
        if (!string.IsNullOrWhiteSpace(gamePath))
        {
            preferred = _gameRuntimeService.FindLatestSession(gamePath);
        }

        if (preferred != null)
        {
            _logger.LogDebug("Selected preferred session {SessionName} for path {GamePath}.", preferred.DisplayName, gamePath);
            SelectedSession = preferred;
            return;
        }

        if (SelectedSession != null && Sessions.Contains(SelectedSession))
        {
            _logger.LogDebug("Keeping current session selection {SessionName}.", SelectedSession.DisplayName);
            return;
        }

        SelectedSession = Sessions.FirstOrDefault();
        _logger.LogDebug(
            "Fell back to session selection {SessionName}.",
            SelectedSession?.DisplayName ?? "<none>");
    }

    /// <summary>
    /// Builds the clipboard text for the currently selected session.
    /// </summary>
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
        _logger.LogInformation(
            "Selected log session changed to {SessionName}.",
            value?.DisplayName ?? "<none>");
        RefreshVisibleEntries(GameLogProjectionRefreshReason.SessionChanged);
        StopSelectedSessionCommand.NotifyCanExecuteChanged();
        ForceStopSelectedSessionCommand.NotifyCanExecuteChanged();
    }

    private void Sessions_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        HasSessions = Sessions.Count > 0;
        _logger.LogDebug(
            "Observed logs session collection change. Action: {Action}. SessionCount: {SessionCount}.",
            e.Action,
            Sessions.Count);

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

        _logger.LogDebug(
            "Observed entry collection change for {SessionName}. Action: {Action}. RefreshReason: {Reason}.",
            SelectedSession?.DisplayName ?? "<none>",
            e.Action,
            reason);

        RefreshVisibleEntries(reason);
    }

    /// <summary>
    /// Rebuilds the visible log projection for the selected session.
    /// </summary>
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
        _logger.LogDebug(
            "Refreshed visible log entries. Reason: {Reason}. SelectedSession: {SessionName}. VisibleEntries: {VisibleEntryCount}. FilteredEntries: {FilteredEntryCount}. CurrentPage: {CurrentPage}. TotalPages: {TotalPages}.",
            reason,
            SelectedSession?.DisplayName ?? "<none>",
            VisibleEntries.Count,
            FilteredEntryCount,
            CurrentPageNumber,
            TotalPages);
        NotifyProjectionStateChanged();
    }

    /// <summary>
    /// Replaces the UI-bound visible entry collection with the latest projection result.
    /// </summary>
    private void ReplaceVisibleEntries(IEnumerable<GameLogEntry> entries)
    {
        VisibleEntries.Clear();
        foreach (var entry in entries)
        {
            VisibleEntries.Add(entry);
        }
    }

    /// <summary>
    /// Raises UI state notifications that depend on the current projection result.
    /// </summary>
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
