namespace Emerald.CoreX.Runtime;

/// <summary>
/// Represents the lifecycle state of a tracked game process.
/// </summary>
public enum GameRunState
{
    /// <summary>
    /// No active session is associated with the game.
    /// </summary>
    Idle,

    /// <summary>
    /// The launcher is preparing the process but the game is not yet running.
    /// </summary>
    Launching,

    /// <summary>
    /// The game process is currently running.
    /// </summary>
    Running,

    /// <summary>
    /// A stop request has been issued and the runtime is waiting for the process to exit.
    /// </summary>
    Stopping,

    /// <summary>
    /// The process exited without being classified as a failure.
    /// </summary>
    Exited,

    /// <summary>
    /// The session ended because launch or execution failed.
    /// </summary>
    Failed
}

/// <summary>
/// Represents the normalized severity level for a captured game log entry.
/// </summary>
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

/// <summary>
/// Represents the stop strategy requested for an active game session.
/// </summary>
public enum GameStopMode
{
    /// <summary>
    /// Ask the game window to close cleanly.
    /// </summary>
    Gentle,

    /// <summary>
    /// Terminate the process directly.
    /// </summary>
    Force
}

/// <summary>
/// Describes which capture channels were available for a session.
/// </summary>
public enum GameCaptureMode
{
    LifecycleOnly,
    StandardOutputOnly,
    StandardOutputUnavailable
}

/// <summary>
/// Identifies where a log entry originated from during runtime tracking.
/// </summary>
public enum GameLogSource
{
    Lifecycle,
    StandardOutput,
    StandardError,
    CrashReport
}
