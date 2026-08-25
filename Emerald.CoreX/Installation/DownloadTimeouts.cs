namespace Emerald.CoreX.Installation;

/// <summary>Per-request limits. File transfers have no total deadline while bytes continue flowing.</summary>
public sealed class DownloadTimeouts
{
    public TimeSpan ResponseHeadersTimeout { get; init; } = TimeSpan.FromSeconds(10);
    public TimeSpan InactivityTimeout { get; init; } = TimeSpan.FromSeconds(15);
    public int Attempts { get; init; } = 3;
}

public sealed class DownloadTimeoutException(string phase, string url)
    : TimeoutException($"Download {phase} timed out: {url}");
