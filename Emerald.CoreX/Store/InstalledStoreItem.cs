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

    public string StatusText => IsTracked ? "Tracked" : "Untracked";
}
