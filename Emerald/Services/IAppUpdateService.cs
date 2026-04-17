using Emerald.Models;

namespace Emerald.Services;

public interface IAppUpdateService
{
    Task<AppUpdateCheckResult> CheckForUpdatesAsync(AppReleaseChannel preferredChannel, CancellationToken cancellationToken = default);
    Task<AppUpdateInstallResult> TryInstallUpdateAsync(AppUpdateCheckResult updateResult, CancellationToken cancellationToken = default);
}

public enum AppUpdateStatus
{
    UpToDate,
    UpdateAvailable,
    LocalBuildIsNewer,
    Unavailable
}

public enum AppUpdateInstallMethod
{
    Browser,
    AppInstaller,
    PackageManager
}

public sealed record AppUpdateCheckResult(
    AppUpdateStatus Status,
    string CurrentPublicVersion,
    Version CurrentPackageVersion,
    AppReleaseChannel CurrentChannel,
    Version? LatestPackageVersion = null,
    string? LatestPublicVersion = null,
    AppReleaseChannel? LatestChannel = null,
    string? LatestTag = null,
    string? ReleaseUrl = null,
    string? ReleaseNotes = null,
    AppUpdateInstallMethod InstallMethod = AppUpdateInstallMethod.Browser,
    string? PreferredInstallUri = null,
    bool IsInstallerCapable = false,
    string? ErrorMessage = null);

public sealed record AppUpdateInstallResult(
    bool Succeeded,
    string? Message = null,
    Exception? Error = null);
