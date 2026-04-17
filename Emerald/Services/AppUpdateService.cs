using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Emerald.Services;

public partial class AppUpdateService(ILogger<AppUpdateService> logger) : IAppUpdateService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly Uri ReleasesEndpoint = new("https://api.github.com/repos/RiversideValley/Emerald/releases");

    private readonly HttpClient _httpClient = CreateHttpClient();

    public async Task<AppUpdateCheckResult> CheckForUpdatesAsync(bool includePreReleases, CancellationToken cancellationToken = default)
    {
        var currentVersion = GetCurrentVersion();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, ReleasesEndpoint);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var releases = await JsonSerializer.DeserializeAsync<List<GitHubReleaseDto>>(stream, JsonOptions, cancellationToken)
                ?? [];

            var release = releases
                .Where(candidate => !candidate.Draft)
                .Where(candidate => includePreReleases || !candidate.PreRelease)
                .OrderByDescending(candidate => candidate.PublishedAt ?? candidate.CreatedAt)
                .FirstOrDefault(candidate => TryParseVersion(candidate.TagName, out _));

            if (release is null || !TryParseVersion(release.TagName, out var latestVersion) || latestVersion is null)
            {
                return new AppUpdateCheckResult(
                    Status: AppUpdateStatus.Unavailable,
                    CurrentVersion: currentVersion,
                    ErrorMessage: "No parseable release versions were found.");
            }

            var status = latestVersion > currentVersion
                ? AppUpdateStatus.UpdateAvailable
                : latestVersion < currentVersion
                    ? AppUpdateStatus.LocalBuildIsNewer
                    : AppUpdateStatus.UpToDate;

            return new AppUpdateCheckResult(
                Status: status,
                CurrentVersion: currentVersion,
                LatestVersion: latestVersion,
                LatestTag: release.TagName,
                ReleaseUrl: release.HtmlUrl,
                ReleaseNotes: release.Body);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to check for updates.");
            return new AppUpdateCheckResult(
                Status: AppUpdateStatus.Unavailable,
                CurrentVersion: currentVersion,
                ErrorMessage: ex.Message);
        }
    }

    private static Version GetCurrentVersion()
    {
        return Version.TryParse(DirectResoucres.AppVersion, out var parsedVersion)
            ? parsedVersion
            : new Version(0, 0, 0, 0);
    }

    private static HttpClient CreateHttpClient()
    {
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Emerald", DirectResoucres.AppVersion));
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return httpClient;
    }

    private static bool TryParseVersion(string? source, out Version? version)
    {
        version = null;

        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        var match = VersionRegex().Match(source);
        if (!match.Success)
        {
            return false;
        }

        return Version.TryParse(match.Value, out version);
    }

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

    [GeneratedRegex(@"\d+(?:\.\d+){1,3}")]
    private static partial Regex VersionRegex();
}
