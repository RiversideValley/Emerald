using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Emerald.CoreX.Runtime;

/// <summary>
/// Captures the live and historical runtime state for a launched game instance.
/// </summary>
public partial class GameSession(Game game, DateTimeOffset startedAt) : ObservableObject
{
    public Game Game { get; } = game;

    public ObservableCollection<GameLogEntry> Entries { get; } = new();

    public string GamePath => Game.Path.BasePath;

    public string DisplayName => Game.Version.DisplayName;

    public string VersionText
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Game.Version.ModVersion))
            {
                return $"{Game.Version.Type} | {Game.Version.BasedOn} | {Game.Version.ModVersion}";
            }

            return $"{Game.Version.Type} | {Game.Version.BasedOn}";
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsActive))]
    [NotifyPropertyChangedFor(nameof(CanStop))]
    [NotifyPropertyChangedFor(nameof(CanForceStop))]
    [NotifyPropertyChangedFor(nameof(RunStateText))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private GameRunState _state = GameRunState.Launching;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CaptureModeText))]
    [NotifyPropertyChangedFor(nameof(LogCaptureNotice))]
    private GameCaptureMode _captureMode = GameCaptureMode.LifecycleOnly;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private int? _processId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(ExitCodeText))]
    private int? _exitCode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private DateTimeOffset? _endedAt;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private bool _hasCrashReport;

    [ObservableProperty]
    private string? _crashReportPath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLogs))]
    private int _entryCount;

    [ObservableProperty]
    private string? _lastMessagePreview;

    public DateTimeOffset StartedAt { get; } = startedAt;

    public bool IsActive => State is GameRunState.Launching or GameRunState.Running or GameRunState.Stopping;

    public bool CanStop => IsActive;

    public bool CanForceStop => IsActive;

    public bool HasLogs => EntryCount > 0;

    public string RunStateText => State switch
    {
        GameRunState.Launching => "Launching",
        GameRunState.Running => "Running",
        GameRunState.Stopping => "Stopping",
        GameRunState.Exited => "Exited",
        GameRunState.Failed => "Failed",
        _ => "Idle"
    };

    public string CaptureModeText => CaptureMode switch
    {
        GameCaptureMode.StandardOutputOnly => "stdout only",
        GameCaptureMode.StandardOutputUnavailable => "stdout unavailable",
        _ => "lifecycle only"
    };

    public string? ExitCodeText => ExitCode is int code ? $"Exit {code}" : null;

    public string? LogCaptureNotice => CaptureMode switch
    {
        GameCaptureMode.StandardOutputOnly => "Using standard output log capture for this session.",
        GameCaptureMode.StandardOutputUnavailable => "Standard output capture is unavailable for this session. Only lifecycle events are shown.",
        GameCaptureMode.LifecycleOnly => "Log capture is disabled. Only lifecycle events are shown.",
        _ => null
    };

    public string StatusText
    {
        get
        {
            var parts = new List<string> { RunStateText };

            if (ProcessId is int pid)
            {
                parts.Add($"PID {pid}");
            }

            if (ExitCode is int code && !IsActive)
            {
                parts.Add($"exit {code}");
            }

            if (EndedAt is DateTimeOffset endedAt && !IsActive)
            {
                parts.Add(endedAt.ToLocalTime().ToString("g"));
            }

            if (HasCrashReport)
            {
                parts.Add("crash report");
            }

            return string.Join(" • ", parts);
        }
    }

    /// <summary>
    /// Formats the session header and all captured entries for clipboard export.
    /// </summary>
    public string ToClipboardText()
    {
        var builder = new StringBuilder();
        builder.AppendLine($"{DisplayName} ({VersionText})");
        builder.AppendLine(StatusText);
        builder.AppendLine();

        foreach (var entry in Entries)
        {
            builder.AppendLine(entry.ToClipboardText());
        }

        return builder.ToString();
    }
}
