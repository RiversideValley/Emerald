using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Emerald.Models;
using Microsoft.Extensions.Logging;
using Windows.System;
#if WINDOWS
using Windows.ApplicationModel;
using Windows.Management.Deployment;
#endif

namespace Emerald.Services;

public partial class AppUpdateService(ILogger<AppUpdateService> logger) : IAppUpdateService
{
    private const string RepositoryOwner = "RiversideValley";
    private const string RepositoryName = "Emerald";
    private const string ReleaseAppInstallerFileName = "Emerald-Release.appinstaller";
    private const string NightlyArtifactsUrl = $"https://github.com/{RepositoryOwner}/{RepositoryName}/actions/workflows/ci.yml?query=branch%3Amain";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly Uri ReleasesEndpoint =
        new($"https://api.github.com/repos/{RepositoryOwner}/{RepositoryName}/releases?per_page=100");

    private static readonly string ReleaseAppInstallerUrl =
        $"https://github.com/{RepositoryOwner}/{RepositoryName}/releases/latest/download/{ReleaseAppInstallerFileName}";

    private readonly HttpClient _httpClient = CreateHttpClient();

    public async Task<AppUpdateCheckResult> CheckForUpdatesAsync(
        AppReleaseChannel preferredChannel,
        CancellationToken cancellationToken = default)
    {
        var currentPublicVersion = DirectResoucres.PublicVersion;
        var currentPackageVersion = ParsePackageVersion(DirectResoucres.PackageVersion);
        var currentChannel = DirectResoucres.ReleaseChannel;

        if (preferredChannel == AppReleaseChannel.Nightly)
        {
            return new AppUpdateCheckResult(
                Status: AppUpdateStatus.ManualDownloadRequired,
                CurrentPublicVersion: currentPublicVersion,
                CurrentPackageVersion: currentPackageVersion,
                CurrentChannel: currentChannel,
                LatestChannel: AppReleaseChannel.Nightly,
                InstallMethod: AppUpdateInstallMethod.Browser,
                PreferredInstallUri: NightlyArtifactsUrl,
                ErrorMessage: "Nightly builds are distributed as GitHub Actions artifacts and must be downloaded manually.");
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, ReleasesEndpoint);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var releases = await JsonSerializer.DeserializeAsync<List<GitHubReleaseDto>>(stream, JsonOptions, cancellationToken)
                ?? [];

            var candidate = releases
                .Where(release => !release.Draft)
                .Select(MapCandidate)
                .Where(release => release.Channel == preferredChannel)
                .OrderByDescending(release => release.PublishedAt ?? DateTimeOffset.MinValue)
                .ThenByDescending(release => release.PackageVersion)
                .FirstOrDefault();

            if (candidate is null)
            {
                return new AppUpdateCheckResult(
                    Status: AppUpdateStatus.Unavailable,
                    CurrentPublicVersion: currentPublicVersion,
                    CurrentPackageVersion: currentPackageVersion,
                    CurrentChannel: currentChannel,
                    ErrorMessage: $"No {preferredChannel.ToMetadataValue()} releases were found.");
            }

            var status = ResolveStatus(candidate, currentPackageVersion);
            var installMethod = ResolveInstallMethod(preferredChannel);
            var preferredInstallUri = ResolveInstallUri(preferredChannel, candidate.ReleaseUrl);

            return new AppUpdateCheckResult(
                Status: status,
                CurrentPublicVersion: currentPublicVersion,
                CurrentPackageVersion: currentPackageVersion,
                CurrentChannel: currentChannel,
                LatestPackageVersion: candidate.PackageVersion,
                LatestPublicVersion: candidate.PublicVersion,
                LatestChannel: candidate.Channel,
                LatestTag: candidate.TagName,
                ReleaseUrl: candidate.ReleaseUrl,
                ReleaseNotes: candidate.ReleaseNotes,
                InstallMethod: installMethod,
                PreferredInstallUri: preferredInstallUri,
                IsInstallerCapable: installMethod is AppUpdateInstallMethod.AppInstaller or AppUpdateInstallMethod.PackageManager);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to check for updates.");
            return new AppUpdateCheckResult(
                Status: AppUpdateStatus.Unavailable,
                CurrentPublicVersion: currentPublicVersion,
                CurrentPackageVersion: currentPackageVersion,
                CurrentChannel: currentChannel,
                ErrorMessage: ex.Message);
        }
    }

    public async Task<AppUpdateInstallResult> TryInstallUpdateAsync(
        AppUpdateCheckResult updateResult,
        CancellationToken cancellationToken = default)
    {
        if (updateResult.Status != AppUpdateStatus.UpdateAvailable)
        {
            return new AppUpdateInstallResult(false, "No installable update is currently available.");
        }

        if (string.IsNullOrWhiteSpace(updateResult.PreferredInstallUri)
            && string.IsNullOrWhiteSpace(updateResult.ReleaseUrl))
        {
            return new AppUpdateInstallResult(false, "The update did not provide an install URL.");
        }

        try
        {
            switch (updateResult.InstallMethod)
            {
                case AppUpdateInstallMethod.PackageManager:
#if WINDOWS
                    if (string.IsNullOrWhiteSpace(updateResult.PreferredInstallUri)
                        || !Uri.TryCreate(updateResult.PreferredInstallUri, UriKind.Absolute, out var appInstallerUri))
                    {
                        return new AppUpdateInstallResult(false, "The package manager install URI is invalid.");
                    }

                    var packageManager = new PackageManager();
                    var deploymentOperation = packageManager.AddPackageByAppInstallerFileAsync(
                        appInstallerUri,
                        AddPackageByAppInstallerOptions.None,
                        packageManager.GetDefaultPackageVolume());
                    var deploymentResult = await deploymentOperation;

                    if (!string.IsNullOrWhiteSpace(deploymentResult.ErrorText))
                    {
                        return new AppUpdateInstallResult(false, deploymentResult.ErrorText);
                    }

                    return new AppUpdateInstallResult(true, "Update installation has been started.");
#else
                    goto case AppUpdateInstallMethod.AppInstaller;
#endif
                case AppUpdateInstallMethod.AppInstaller:
                {
                    var installUri = BuildAppInstallerLaunchUri(updateResult.PreferredInstallUri ?? updateResult.ReleaseUrl!);
                    var launched = await Launcher.LaunchUriAsync(installUri);
                    return launched
                        ? new AppUpdateInstallResult(true, "App Installer launched successfully.")
                        : new AppUpdateInstallResult(false, "Failed to launch App Installer.");
                }
                default:
                {
                    var uri = updateResult.ReleaseUrl ?? updateResult.PreferredInstallUri!;
                    if (!Uri.TryCreate(uri, UriKind.Absolute, out var releaseUri))
                    {
                        return new AppUpdateInstallResult(false, "The release URL is invalid.");
                    }

                    var launched = await Launcher.LaunchUriAsync(releaseUri);
                    return launched
                        ? new AppUpdateInstallResult(true, "Browser launched successfully.")
                        : new AppUpdateInstallResult(false, "Failed to launch the browser.");
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to install update.");
            return new AppUpdateInstallResult(false, ex.Message, ex);
        }
    }

    private static AppUpdateStatus ResolveStatus(ReleaseCandidate candidate, Version currentPackageVersion)
    {
        if (candidate.PackageVersion > currentPackageVersion)
        {
            return AppUpdateStatus.UpdateAvailable;
        }

        if (candidate.PackageVersion < currentPackageVersion)
        {
            return AppUpdateStatus.LocalBuildIsNewer;
        }

        return AppUpdateStatus.UpToDate;
    }

    private static AppUpdateInstallMethod ResolveInstallMethod(AppReleaseChannel preferredChannel)
    {
        if (preferredChannel != AppReleaseChannel.Release)
        {
            return AppUpdateInstallMethod.Browser;
        }

#if WINDOWS
        return IsAppInstallerAssociatedWithCurrentPackage()
            ? AppUpdateInstallMethod.PackageManager
            : AppUpdateInstallMethod.AppInstaller;
#else
        return AppUpdateInstallMethod.Browser;
#endif
    }

    private static string? ResolveInstallUri(AppReleaseChannel preferredChannel, string? releaseUrl)
    {
        return preferredChannel == AppReleaseChannel.Release
            ? ReleaseAppInstallerUrl
            : releaseUrl;
    }

#if WINDOWS
    private static bool IsAppInstallerAssociatedWithCurrentPackage()
    {
        try
        {
            var package = Package.Current;
            var appInstallerInfo = package.GetAppInstallerInfo();
            return appInstallerInfo is not null;
        }
        catch
        {
            return false;
        }
    }
#endif

    private static Uri BuildAppInstallerLaunchUri(string sourceUri)
    {
        var encoded = Uri.EscapeDataString(sourceUri);
        return new Uri($"ms-appinstaller:?source={encoded}");
    }

    private static Version ParsePackageVersion(string rawVersion)
    {
        return Version.TryParse(rawVersion, out var parsed)
            ? parsed
            : new Version(0, 0, 0, 0);
    }

    private static ReleaseCandidate MapCandidate(GitHubReleaseDto dto)
    {
        var channel = ResolveChannel(dto);
        var publicVersion = ExtractPublicVersion(dto.TagName);
        var packageVersion = TryExtractPackageVersion(dto.TagName)
            ?? new Version(0, 0, 0, 0);

        return new ReleaseCandidate(
            TagName: dto.TagName ?? string.Empty,
            PublicVersion: publicVersion,
            PackageVersion: packageVersion,
            Channel: channel,
            ReleaseUrl: dto.HtmlUrl,
            ReleaseNotes: dto.Body,
            PublishedAt: dto.PublishedAt ?? dto.CreatedAt);
    }

    private static AppReleaseChannel ResolveChannel(GitHubReleaseDto dto)
    {
        var tag = dto.TagName ?? string.Empty;
        var tagLower = tag.ToLowerInvariant();

        if (tagLower.StartsWith("nightly-") || tagLower.Contains("nightly"))
        {
            return AppReleaseChannel.Nightly;
        }

        if (dto.PreRelease || tagLower.Contains("-pre.") || tagLower.Contains("-rc."))
        {
            return AppReleaseChannel.Prerelease;
        }

        return AppReleaseChannel.Release;
    }

    private static string ExtractPublicVersion(string? tagName)
    {
        if (string.IsNullOrWhiteSpace(tagName))
        {
            return "unknown";
        }

        var tag = tagName.Trim();
        if (tag.StartsWith('v') || tag.StartsWith('V'))
        {
            tag = tag[1..];
        }

        var pkgIndex = tag.IndexOf("+pkg.", StringComparison.OrdinalIgnoreCase);
        if (pkgIndex >= 0)
        {
            tag = tag[..pkgIndex];
        }

        return tag;
    }

    private static Version? TryExtractPackageVersion(string? tagName)
    {
        if (string.IsNullOrWhiteSpace(tagName))
        {
            return null;
        }

        var pkgMatch = PackageVersionRegex().Match(tagName);
        if (pkgMatch.Success)
        {
            var pkgVersion = $"{pkgMatch.Groups["major"].Value}.{pkgMatch.Groups["minor"].Value}.{pkgMatch.Groups["patch"].Value}.{pkgMatch.Groups["revision"].Value}";
            if (Version.TryParse(pkgVersion, out var parsed))
            {
                return parsed;
            }
        }

        var semanticMatch = SemanticVersionRegex().Match(tagName);
        if (!semanticMatch.Success)
        {
            return null;
        }

        var major = semanticMatch.Groups["major"].Value;
        var minor = semanticMatch.Groups["minor"].Value;
        var patch = semanticMatch.Groups["patch"].Value;
        var version = $"{major}.{minor}.{patch}.0";
        return Version.TryParse(version, out var fallbackParsed) ? fallbackParsed : null;
    }

    private static HttpClient CreateHttpClient()
    {
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Emerald", DirectResoucres.PackageVersion));
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return httpClient;
    }

    private sealed record ReleaseCandidate(
        string TagName,
        string PublicVersion,
        Version PackageVersion,
        AppReleaseChannel Channel,
        string? ReleaseUrl,
        string? ReleaseNotes,
        DateTimeOffset? PublishedAt);

    private sealed class GitHubReleaseDto
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; init; }

        [JsonPropertyName("draft")]
        public bool Draft { get; init; }

        [JsonPropertyName("prerelease")]
        public bool PreRelease { get; init; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; init; }

        [JsonPropertyName("body")]
        public string? Body { get; init; }

        [JsonPropertyName("created_at")]
        public DateTimeOffset? CreatedAt { get; init; }

        [JsonPropertyName("published_at")]
        public DateTimeOffset? PublishedAt { get; init; }
    }

    [GeneratedRegex(@"(?:\+|-)pkg\.(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)\.(?<revision>\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex PackageVersionRegex();

    [GeneratedRegex(@"(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)")]
    private static partial Regex SemanticVersionRegex();
}
