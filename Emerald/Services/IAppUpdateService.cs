namespace Emerald.Services;

public interface IAppUpdateService
{
    Task<AppUpdateCheckResult> CheckForUpdatesAsync(bool includePreReleases, CancellationToken cancellationToken = default);
}

public enum AppUpdateStatus
{
    UpToDate,
    UpdateAvailable,
    LocalBuildIsNewer,
    Unavailable
}

public sealed record AppUpdateCheckResult(
    AppUpdateStatus Status,
    Version CurrentVersion,
    Version? LatestVersion = null,
    string? LatestTag = null,
    string? ReleaseUrl = null,
    string? ReleaseNotes = null,
    string? ErrorMessage = null);
