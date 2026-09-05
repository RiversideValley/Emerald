namespace Emerald.CoreX.CrashHandling;

public interface IPlatformDiagnosticsProvider
{
    PlatformDiagnosticsResult FindRecent(DateTimeOffset occurredUtc);
}
public sealed record PlatformDiagnosticsResult(string Status, string? Path = null)
{
    public bool IsAvailable => !string.IsNullOrWhiteSpace(Path);

    public static PlatformDiagnosticsResult Unavailable(string reason)
        => new(reason);
}
