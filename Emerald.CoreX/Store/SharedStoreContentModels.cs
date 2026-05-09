using Emerald.CoreX.Store.Modrinth.JSON;
using Emerald.CoreX.Models;

namespace Emerald.CoreX.Store;

public enum StoreLinkMode
{
    SymbolicLink,
    HardLink,
    Copy
}

public enum StoreLinkKind
{
    None,
    SymbolicLink,
    HardLink,
    Copy
}

public enum StoreSharedContentHealth
{
    Ok,
    MissingInstanceFile,
    MissingSharedFile,
    BrokenLink,
    HashMismatch,
    Untracked
}

public enum StoreSharedContentMigrationAction
{
    ConvertTrackedFiles,
    ConvertAllCompatibleFiles,
    OnlyFutureInstalls,
    MaterializeFiles,
    LeaveExistingLinks,
    RemoveSharedInstalls
}

public sealed class StoreSharedContentSettings
{
    public StoreLinkMode WindowsLinkMode { get; set; } = StoreLinkMode.HardLink;

    public StoreLinkMode UnixLinkMode { get; set; } = StoreLinkMode.SymbolicLink;
}

public sealed class StoreLinkCreationResult
{
    public required StoreLinkKind LinkKind { get; init; }

    public string? FallbackReason { get; init; }
}

public sealed class StoreSharedInstallRequest
{
    public required Game Game { get; init; }

    public string? SharedBasePathOverride { get; init; }

    public GameSettings? EffectiveSettingsOverride { get; init; }

    public required StoreContentType ContentType { get; init; }

    public required string InstallFolderName { get; init; }

    public required ItemFile File { get; init; }

    public required string TargetPath { get; init; }

    public required Func<string, IProgress<double>?, CancellationToken, Task> DownloadToPathAsync { get; init; }

    public IProgress<double>? Progress { get; init; }

    public CancellationToken CancellationToken { get; init; }
}

public sealed class StoreSharedInstallResult
{
    public required string FilePath { get; init; }

    public string? Sha1 { get; init; }

    public string? Sha512 { get; init; }

    public string? SharedFilePath { get; init; }

    public StoreLinkKind LinkKind { get; init; }

    public long? FileSizeBytes { get; init; }
}

public sealed class StoreSharedContentManifestEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public StoreContentType ContentType { get; set; }

    public string BasePath { get; set; } = string.Empty;

    public string InstallFolderName { get; set; } = string.Empty;

    public string HashAlgorithm { get; set; } = "sha1";

    public string Hash { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string SharedFilePath { get; set; } = string.Empty;

    public long? FileSizeBytes { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public List<StoreSharedContentReference> References { get; set; } = [];
}

public sealed class StoreSharedContentReference
{
    public string InstallRecordId { get; set; } = string.Empty;

    public string GamePath { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string ProjectId { get; set; } = string.Empty;

    public string VersionId { get; set; } = string.Empty;

    public DateTimeOffset AddedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class StoreSharedContentMigrationPlan
{
    public required Game Game { get; init; }

    public required StoreContentType ContentType { get; init; }

    public required string InstallFolderName { get; init; }

    public bool EnableSharing { get; init; }

    public int TrackedConvertibleCount { get; set; }

    public int SharedInstallCount { get; set; }

    public int UntrackedFileCount { get; set; }

    public int HashMismatchCount { get; set; }

    public int BrokenOrMissingCount { get; set; }

    public bool HasWork =>
        TrackedConvertibleCount > 0
        || SharedInstallCount > 0
        || UntrackedFileCount > 0
        || HashMismatchCount > 0
        || BrokenOrMissingCount > 0;
}

public sealed class StoreSharedContentMigrationSummary
{
    public int TrackedConvertibleCount { get; set; }

    public int SharedInstallCount { get; set; }

    public int UntrackedFileCount { get; set; }

    public int HashMismatchCount { get; set; }

    public int BrokenOrMissingCount { get; set; }

    public int ChangedCount { get; set; }

    public bool HasWork =>
        TrackedConvertibleCount > 0
        || SharedInstallCount > 0
        || UntrackedFileCount > 0
        || HashMismatchCount > 0
        || BrokenOrMissingCount > 0;

    public void Add(StoreSharedContentMigrationPlan plan)
    {
        TrackedConvertibleCount += plan.TrackedConvertibleCount;
        SharedInstallCount += plan.SharedInstallCount;
        UntrackedFileCount += plan.UntrackedFileCount;
        HashMismatchCount += plan.HashMismatchCount;
        BrokenOrMissingCount += plan.BrokenOrMissingCount;
    }
}
