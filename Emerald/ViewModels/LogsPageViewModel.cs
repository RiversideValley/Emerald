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
    private readonly ObservableCollection<GameLogEntry> _emptyEntries = new();
    private GameSession? _observedSession;

    public ObservableCollection<GameSession> Sessions => _gameRuntimeService.Sessions;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedSession))]
    [NotifyPropertyChangedFor(nameof(SelectedEntries))]
    [NotifyPropertyChangedFor(nameof(HasSelectedEntries))]
    [NotifyPropertyChangedFor(nameof(CanStopSelectedSession))]
    [NotifyPropertyChangedFor(nameof(CanForceStopSelectedSession))]
    [NotifyPropertyChangedFor(nameof(SelectedSessionTitle))]
    [NotifyPropertyChangedFor(nameof(SelectedSessionStatusText))]
    [NotifyPropertyChangedFor(nameof(SelectedCaptureModeText))]
    [NotifyPropertyChangedFor(nameof(LogCaptureNotice))]
    [NotifyPropertyChangedFor(nameof(HasLogCaptureNotice))]
    private GameSession? _selectedSession;

    [ObservableProperty]
    private bool _autoScroll = true;

    [ObservableProperty]
    private bool _hasSessions;

    public bool HasSelectedSession => SelectedSession != null;

    public ObservableCollection<GameLogEntry> SelectedEntries => SelectedSession?.Entries ?? _emptyEntries;

    public bool HasSelectedEntries => SelectedEntries.Count > 0;

    public bool CanStopSelectedSession => SelectedSession?.CanStop ?? false;

    public bool CanForceStopSelectedSession => SelectedSession?.CanForceStop ?? false;

    public string SelectedSessionTitle => SelectedSession?.DisplayName ?? "Logs";

    public string? SelectedSessionStatusText => SelectedSession?.StatusText;

    public string? SelectedCaptureModeText => SelectedSession?.CaptureModeText;

    public string? LogCaptureNotice => SelectedSession?.LogCaptureNotice;

    public bool HasLogCaptureNotice => !string.IsNullOrWhiteSpace(LogCaptureNotice);

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

        OnPropertyChanged(nameof(SelectedEntries));
        OnPropertyChanged(nameof(HasSelectedEntries));
        OnPropertyChanged(nameof(CanStopSelectedSession));
        OnPropertyChanged(nameof(CanForceStopSelectedSession));
        OnPropertyChanged(nameof(SelectedSessionTitle));
        OnPropertyChanged(nameof(SelectedSessionStatusText));
        OnPropertyChanged(nameof(SelectedCaptureModeText));
        OnPropertyChanged(nameof(LogCaptureNotice));
        OnPropertyChanged(nameof(HasLogCaptureNotice));
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
        OnPropertyChanged(nameof(HasSelectedEntries));
    }
}
