namespace Emerald.CoreX.Store;

public sealed class StoreInstallRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public StoreContentType ContentType { get; set; }
    public string GamePath { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string ProjectTitle { get; set; } = string.Empty;
    public string VersionId { get; set; } = string.Empty;
    public string VersionName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string? Sha1 { get; set; }
    public string? Sha512 { get; set; }
    public DateTimeOffset InstalledAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
