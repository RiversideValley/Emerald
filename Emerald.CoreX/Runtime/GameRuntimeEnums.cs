namespace Emerald.CoreX.Runtime;

public enum GameRunState
{
    Idle,
    Launching,
    Running,
    Stopping,
    Exited,
    Failed
}

public enum GameLogLevel
{
    Trace,
    Debug,
    Info,
    Warn,
    Error,
    Fatal,
    Unknown
}

public enum GameStopMode
{
    Gentle,
    Force
}

public enum GameCaptureMode
{
    LifecycleOnly,
    StandardOutputOnly,
    FileOnly,
    Hybrid
}

public enum GameLogSource
{
    Lifecycle,
    StandardOutput,
    StandardError,
    FileTail,
    CrashReport
}
