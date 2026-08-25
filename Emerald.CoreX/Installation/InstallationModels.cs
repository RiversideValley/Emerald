namespace Emerald.CoreX.Installation;

public enum InstanceInstallationState
{
    Unknown,
    NotInstalled,
    Installing,
    Verifying,
    Ready,
    ReadyWithWarnings,
    NeedsRepair,
    Failed
}

public enum IntegrityCheckLevel { Quick, Full }
public enum IntegritySeverity { Warning, Critical }
public enum ManagedPathRoot { Instance, Assets, Libraries, Runtime, Versions }
public enum ManagedFileCategory { Metadata, Client, Library, Native, Logging, Asset, Java, ManagedContent, Other }

public sealed record ExpectedManagedFile(
    ManagedPathRoot Root,
    string RelativePath,
    long? Size,
    string? Sha1,
    string? Sha512,
    ManagedFileCategory Category,
    IntegritySeverity Severity,
    string? RepairUrl = null);

public sealed record IntegrityIssue(
    string Code,
    string Message,
    IntegritySeverity Severity,
    ExpectedManagedFile? File = null);

public sealed record InstanceIntegrityReport(
    IntegrityCheckLevel Level,
    InstanceInstallationState State,
    IReadOnlyList<IntegrityIssue> Issues,
    DateTimeOffset VerifiedAt,
    int CheckedFiles,
    int HashedFiles)
{
    public bool CanLaunch => State is InstanceInstallationState.Ready or InstanceInstallationState.ReadyWithWarnings;
}

public sealed record InstanceInstallResult(
    bool Success,
    InstanceInstallationState State,
    string? ResolvedVersion,
    InstanceIntegrityReport? Integrity,
    string? FailureReason = null);

public sealed record LaunchReadinessResult(bool CanLaunch, InstanceIntegrityReport Integrity, string? FailureReason = null);

public sealed record InstallationProgress(string Stage, string? CurrentItem, int Completed, int Total, long ProcessedBytes = 0, long TotalBytes = 0);

public sealed class InstanceInstallReceipt
{
    public int SchemaVersion { get; set; } = 1;
    public string? ResolvedVersion { get; set; }
    public string? Loader { get; set; }
    public DateTimeOffset? SuccessfulInstallAt { get; set; }
    public DateTimeOffset? SuccessfulRepairAt { get; set; }
    public DateTimeOffset? FullVerificationAt { get; set; }
    public string PathLayoutFingerprint { get; set; } = string.Empty;
    public string ManifestFingerprint { get; set; } = string.Empty;
    public IntegrityCheckLevel VerificationCoverage { get; set; } = IntegrityCheckLevel.Full;
    public List<ExpectedManagedFile> Files { get; set; } = [];
}
