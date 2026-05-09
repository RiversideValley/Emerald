namespace Emerald.CoreX.Store;

public sealed class InstalledStoreItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public StoreContentType ContentType { get; set; }
    public string GamePath { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public bool IsTracked { get; set; }
    public bool IsDirectory { get; set; }
    public long? FileSizeBytes { get; set; }
    public DateTimeOffset? InstalledAtUtc { get; set; }
    public string? ProjectId { get; set; }
    public string? VersionId { get; set; }
    public string? ProjectTitle { get; set; }
    public string? VersionName { get; set; }
    public string? Sha1 { get; set; }
    public string? Sha512 { get; set; }
    public string? GodFolderHash { get; set; }
    public string? SharedFilePath { get; set; }
    public StoreLinkKind LinkKind { get; set; }
    public StoreSharedContentHealth Health { get; set; } = StoreSharedContentHealth.Ok;
    public bool ExistsOnDisk { get; set; } = true;
    public bool IsShared => LinkKind != StoreLinkKind.None || !string.IsNullOrWhiteSpace(GodFolderHash);
    public bool NeedsRepair => Health != StoreSharedContentHealth.Ok && Health != StoreSharedContentHealth.Untracked;

    public string StatusText
    {
        get
        {
            if (!IsTracked)
            {
                return "Untracked";
            }

            if (NeedsRepair)
            {
                return Health switch
                {
                    StoreSharedContentHealth.MissingInstanceFile => "Missing",
                    StoreSharedContentHealth.MissingSharedFile => "Cache missing",
                    StoreSharedContentHealth.BrokenLink => "Broken link",
                    StoreSharedContentHealth.HashMismatch => "Modified",
                    _ => "Needs repair"
                };
            }

            return IsShared ? $"Shared ({FormatLinkKind(LinkKind)})" : "Tracked";
        }
    }
    public string ContentTypeDisplayName => StoreDisplayFormatter.FormatContentType(ContentType);
    public string InstalledRelativeText => InstalledAtUtc.HasValue
        ? $"Installed {StoreDisplayFormatter.FormatRelativeTime(InstalledAtUtc.Value.UtcDateTime)}"
        : IsTracked
            ? "Tracked install"
            : "Found on disk";
    public string FileSizeText => StoreDisplayFormatter.FormatFileSize(FileSizeBytes);
    public string SecondaryText => !string.IsNullOrWhiteSpace(VersionName) ? VersionName : FileName;

    private static string FormatLinkKind(StoreLinkKind linkKind)
        => linkKind switch
        {
            StoreLinkKind.SymbolicLink => "symlink",
            StoreLinkKind.HardLink => "hard link",
            StoreLinkKind.Copy => "copy",
            _ => "file"
        };
}
