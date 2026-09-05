using System.Text.Json.Serialization;

namespace Emerald.CoreX.CrashHandling;

public enum CrashRecordKind
{
    ManagedCrash,
    UnexpectedShutdown
}

public sealed class CrashRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string RunId { get; set; } = string.Empty;
    public DateTimeOffset OccurredUtc { get; set; } = DateTimeOffset.UtcNow;
    public CrashRecordKind Kind { get; set; }
    public string Source { get; set; } = string.Empty;

    public string AppVersion { get; set; } = string.Empty;
    public string PackageVersion { get; set; } = string.Empty;
    public string BuildChannel { get; set; } = string.Empty;
    public string ReleaseTag { get; set; } = string.Empty;
    public string CommitSha { get; set; } = string.Empty;
    public string BuildTimestampUtc { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string OperatingSystem { get; set; } = string.Empty;
    public string Architecture { get; set; } = string.Empty;
    public string Runtime { get; set; } = string.Empty;

    public CrashExceptionInfo? Exception { get; set; }
    public string ApplicationLogTail { get; set; } = string.Empty;
    public string NativeDiagnosticsStatus { get; set; } = "Unavailable";
    public string? NativeDiagnosticsPath { get; set; }
    public DateTimeOffset? AcknowledgedUtc { get; set; }
    public string? ReportPath { get; set; }

    [JsonIgnore]
    public bool IsAcknowledged => AcknowledgedUtc.HasValue;

    public static CrashRecord CreateUnexpectedShutdown(
        LifecycleRunState previousRun,
        CrashEnvironment environment,
        string applicationLogTail,
        string nativeDiagnosticsStatus = "Unavailable",
        string? nativeDiagnosticsPath = null)
        => new()
        {
            RunId = previousRun.RunId,
            OccurredUtc = previousRun.LastHeartbeatUtc == default
                ? previousRun.StartedUtc
                : previousRun.LastHeartbeatUtc,
            Kind = CrashRecordKind.UnexpectedShutdown,
            Source = "Lifecycle marker",
            AppVersion = FirstValue(previousRun.AppVersion, environment.AppVersion),
            PackageVersion = FirstValue(previousRun.PackageVersion, environment.PackageVersion),
            BuildChannel = FirstValue(previousRun.BuildChannel, environment.BuildChannel),
            ReleaseTag = FirstValue(previousRun.ReleaseTag, environment.ReleaseTag),
            CommitSha = FirstValue(previousRun.CommitSha, environment.CommitSha),
            BuildTimestampUtc = FirstValue(previousRun.BuildTimestampUtc, environment.BuildTimestampUtc),
            Platform = FirstValue(previousRun.Platform, environment.Platform),
            OperatingSystem = FirstValue(previousRun.OperatingSystem, environment.OperatingSystem),
            Architecture = FirstValue(previousRun.Architecture, environment.Architecture),
            Runtime = FirstValue(previousRun.Runtime, environment.Runtime),
            ApplicationLogTail = applicationLogTail,
            NativeDiagnosticsStatus = nativeDiagnosticsStatus,
            NativeDiagnosticsPath = nativeDiagnosticsPath
        };

    private static string FirstValue(string value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value;
}

public sealed class CrashExceptionInfo
{
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int? HResult { get; set; }
    public string StackTrace { get; set; } = string.Empty;
    public List<CrashExceptionInfo> InnerExceptions { get; set; } = [];

    public static CrashExceptionInfo FromException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return FromException(exception, new HashSet<Exception>(ReferenceEqualityComparer.Instance), 0);
    }

    private static CrashExceptionInfo FromException(Exception exception, HashSet<Exception> visited, int depth)
    {
        var result = new CrashExceptionInfo
        {
            Type = exception.GetType().FullName ?? exception.GetType().Name,
            Message = CrashTextSanitizer.Sanitize(exception.Message, 16_384),
            HResult = exception.HResult,
            StackTrace = CrashTextSanitizer.Sanitize(exception.StackTrace, 32_768)
        };

        if (depth >= 32 || !visited.Add(exception))
        {
            return result;
        }

        if (exception is AggregateException aggregate)
        {
            foreach (var inner in aggregate.InnerExceptions)
            {
                result.InnerExceptions.Add(FromException(inner, visited, depth + 1));
            }
        }
        else if (exception.InnerException is not null)
        {
            result.InnerExceptions.Add(FromException(exception.InnerException, visited, depth + 1));
        }

        return result;
    }
}

public sealed record CrashEnvironment(
    string AppVersion,
    string PackageVersion,
    string BuildChannel,
    string ReleaseTag,
    string CommitSha,
    string BuildTimestampUtc,
    string Platform,
    string OperatingSystem,
    string Architecture,
    string Runtime);
