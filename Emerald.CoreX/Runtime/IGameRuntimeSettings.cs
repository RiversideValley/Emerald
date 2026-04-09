namespace Emerald.CoreX.Runtime;

/// <summary>
/// Exposes runtime-specific settings needed while launching and tracking games.
/// </summary>
public interface IGameRuntimeSettings
{
    /// <summary>
    /// Gets a value indicating whether standard output log capture should be enabled.
    /// </summary>
    bool IsLogCaptureEnabled { get; }
}
