using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Emerald.CoreX.Installation;
using Microsoft.Extensions.Logging;

namespace Emerald.CoreX.Services.Auth.Authlib;

public sealed class AuthlibInjectorService : IAuthlibInjectorService
{
    private const string ApiLocationHeader = "X-Authlib-Injector-API-Location";
    private readonly ILogger<AuthlibInjectorService> _logger;
    private readonly IAuthlibInjectorSettings _settings;
    private readonly AuthlibInjectorOptions _options;
    private readonly HttpClient _httpClient;
    private readonly DownloadTimeouts _timeouts;
    private readonly string _baseDirectory;
    private readonly SemaphoreSlim _preparationGate = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public AuthlibInjectorService(
        ILogger<AuthlibInjectorService> logger,
        IAuthlibInjectorSettings settings,
        AuthlibInjectorOptions options,
        string? baseDirectory = null,
        HttpClient? httpClient = null,
        DownloadTimeouts? timeouts = null)
    {
        _logger = logger;
        _settings = settings;
        _options = options;
        _httpClient = httpClient ?? new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        _timeouts = timeouts ?? new DownloadTimeouts();
        _baseDirectory = string.IsNullOrWhiteSpace(baseDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Emerald", "authlib-injector")
            : baseDirectory;

        RequireHttps(_options.ArtifactApiRoot, "artifact API root");
        RequireHttps(_options.PrimaryMetadataEndpoint, "primary metadata endpoint");
        RequireHttps(_options.FallbackMetadataEndpoint, "fallback metadata endpoint");
        if (string.IsNullOrWhiteSpace(_options.RecommendedVersion))
            throw new ArgumentException("A recommended authlib-injector version is required.", nameof(options));
    }

    public async Task<AuthlibInjectorLaunchConfiguration> PrepareLaunchAsync(CancellationToken cancellationToken = default)
    {
        await _preparationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(_baseDirectory);
            var selection = GetSelection();
            _logger.LogInformation("Preparing authlib-injector using {Mode} mode for {SelectedVersion}.",
                selection.Mode, selection.Version ?? "the latest release");

            var (artifact, cached) = await ResolveArtifactAsync(selection, cancellationToken).ConfigureAwait(false);
            var jarPath = await EnsureVerifiedJarAsync(artifact, cancellationToken).ConfigureAwait(false);
            var metadata = await ResolveMetadataAsync(cancellationToken).ConfigureAwait(false);
            var arguments = new[]
            {
                $"-javaagent:{jarPath}={metadata.ApiRoot.AbsoluteUri}",
                $"-Dauthlibinjector.yggdrasil.prefetched={Convert.ToBase64String(metadata.Bytes)}"
            };

            _logger.LogInformation(
                "Prepared authlib-injector {Version} build {Build}; metadata endpoint {Endpoint}; descriptor cache fallback: {Cached}.",
                artifact.Version, artifact.BuildNumber, SafeEndpoint(metadata.ApiRoot), cached);
            return new AuthlibInjectorLaunchConfiguration(
                artifact.Version, artifact.BuildNumber, jarPath, metadata.ApiRoot, arguments, cached);
        }
        finally
        {
            _preparationGate.Release();
        }
    }

    public async Task<IReadOnlyList<AuthlibInjectorVersion>> GetAvailableVersionsAsync(CancellationToken cancellationToken = default)
    {
        await _preparationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(_baseDirectory);
            ArtifactIndexDto index;
            try
            {
                index = await GetJsonWithRetriesAsync<ArtifactIndexDto>(new Uri(_options.ArtifactApiRoot, "artifacts.json"), cancellationToken)
                    .ConfigureAwait(false);
                ValidateIndex(index);
                await WriteJsonAtomicallyAsync(CatalogPath, index, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Could not refresh the authlib-injector version catalog; trying the cached catalog.");
                index = await ReadJsonFileAsync<ArtifactIndexDto>(CatalogPath, cancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidOperationException("The authlib-injector version catalog is unavailable and no cached catalog exists.", ex);
                ValidateIndex(index);
            }

            return index.Artifacts
                .Select(entry => new AuthlibInjectorVersion(entry.BuildNumber, entry.Version))
                .OrderByDescending(item => item.BuildNumber)
                .ToArray();
        }
        finally
        {
            _preparationGate.Release();
        }
    }

    private Selection GetSelection() => _settings.VersionMode switch
    {
        AuthlibInjectorVersionMode.Recommended => new(_settings.VersionMode, _options.RecommendedVersion),
        AuthlibInjectorVersionMode.Latest => new(_settings.VersionMode, null),
        AuthlibInjectorVersionMode.Custom when !string.IsNullOrWhiteSpace(_settings.CustomVersion)
            => new(_settings.VersionMode, _settings.CustomVersion.Trim()),
        AuthlibInjectorVersionMode.Custom => throw new InvalidOperationException("Choose an authlib-injector version in Advanced settings before launching with Ely.by."),
        _ => throw new InvalidOperationException($"Unsupported authlib-injector version mode '{_settings.VersionMode}'.")
    };

    private async Task<(ArtifactDescriptor Descriptor, bool Cached)> ResolveArtifactAsync(
        Selection selection,
        CancellationToken cancellationToken)
    {
        try
        {
            ArtifactDescriptor descriptor;
            if (selection.Mode == AuthlibInjectorVersionMode.Latest)
            {
                descriptor = ValidateDescriptor(await GetJsonWithRetriesAsync<ArtifactDescriptor>(
                    new Uri(_options.ArtifactApiRoot, "artifact/latest.json"), cancellationToken).ConfigureAwait(false));
            }
            else
            {
                var index = await GetJsonWithRetriesAsync<ArtifactIndexDto>(new Uri(_options.ArtifactApiRoot, "artifacts.json"), cancellationToken)
                    .ConfigureAwait(false);
                ValidateIndex(index);
                await WriteJsonAtomicallyAsync(CatalogPath, index, cancellationToken).ConfigureAwait(false);
                var catalogEntry = index.Artifacts.FirstOrDefault(entry =>
                    string.Equals(entry.Version, selection.Version, StringComparison.Ordinal));
                if (catalogEntry is null)
                    throw new AuthlibSelectionException($"Authlib-injector version '{selection.Version}' is not present in the official catalog.");

                var build = catalogEntry.BuildNumber;
                descriptor = ValidateDescriptor(await GetJsonWithRetriesAsync<ArtifactDescriptor>(
                    new Uri(_options.ArtifactApiRoot, $"artifact/{build}.json"), cancellationToken).ConfigureAwait(false));
                if (descriptor.BuildNumber != build || !string.Equals(descriptor.Version, selection.Version, StringComparison.Ordinal))
                    throw new InvalidDataException("The official authlib-injector artifact descriptor did not match the selected catalog entry.");
            }

            await WriteDescriptorAsync(descriptor, selection.Mode, cancellationToken).ConfigureAwait(false);
            return (descriptor, false);
        }
        catch (AuthlibSelectionException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            var cached = await FindCachedDescriptorAsync(selection, cancellationToken).ConfigureAwait(false);
            if (cached is null)
                throw new InvalidOperationException("The official authlib-injector artifact API is unavailable and no cached descriptor matches the selected version.", ex);

            _logger.LogWarning(ex,
                "The authlib-injector artifact API freshness check failed; using cached descriptor for {Version} build {Build}.",
                cached.Version, cached.BuildNumber);
            return (cached, true);
        }
    }

    private async Task<string> EnsureVerifiedJarAsync(ArtifactDescriptor descriptor, CancellationToken cancellationToken)
    {
        var jarPath = Path.Combine(_baseDirectory, $"authlib-injector-{descriptor.BuildNumber}.jar");
        if (File.Exists(jarPath))
        {
            if (await HasExpectedHashAsync(jarPath, descriptor.Sha256, cancellationToken).ConfigureAwait(false))
            {
                _logger.LogInformation("Verified cached authlib-injector build {Build} with SHA-256.", descriptor.BuildNumber);
                return jarPath;
            }

            _logger.LogWarning("Cached authlib-injector build {Build} failed SHA-256 validation; downloading a replacement.", descriptor.BuildNumber);
        }

        Exception? lastError = null;
        for (var attempt = 1; attempt <= Math.Max(1, _timeouts.Attempts); attempt++)
        {
            var tempPath = jarPath + $".{Guid.NewGuid():N}.tmp";
            try
            {
                _logger.LogInformation("Downloading authlib-injector {Version} build {Build}, attempt {Attempt}/{Attempts}.",
                    descriptor.Version, descriptor.BuildNumber, attempt, Math.Max(1, _timeouts.Attempts));
                await DownloadFileAsync(descriptor.DownloadUrl, tempPath, cancellationToken).ConfigureAwait(false);
                if (!await HasExpectedHashAsync(tempPath, descriptor.Sha256, cancellationToken).ConfigureAwait(false))
                    throw new InvalidDataException($"Downloaded authlib-injector build {descriptor.BuildNumber} failed SHA-256 validation.");

                File.Move(tempPath, jarPath, true);
                _logger.LogInformation("Verified authlib-injector build {Build} with SHA-256 and stored it in the cache.", descriptor.BuildNumber);
                return jarPath;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                lastError = ex;
                TryDelete(tempPath);
                if (attempt < Math.Max(1, _timeouts.Attempts))
                    _logger.LogWarning(ex, "Authlib-injector download attempt {Attempt} failed; retrying.", attempt);
            }
        }

        throw new InvalidOperationException($"Could not download and verify authlib-injector {descriptor.Version} build {descriptor.BuildNumber}.", lastError);
    }

    private async Task<MetadataResult> ResolveMetadataAsync(CancellationToken cancellationToken)
    {
        var failures = new List<string>();
        foreach (var endpoint in new[] { _options.PrimaryMetadataEndpoint, _options.FallbackMetadataEndpoint })
        {
            try
            {
                var result = await FetchMetadataAsync(endpoint, cancellationToken).ConfigureAwait(false);
                if (endpoint == _options.FallbackMetadataEndpoint)
                    _logger.LogInformation("Using Ely.by metadata fallback endpoint {Endpoint}.", SafeEndpoint(result.ApiRoot));
                return result;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                var reason = SanitizeFailure(ex);
                failures.Add($"{SafeEndpoint(endpoint)}: {reason}");
                _logger.LogWarning("Ely.by metadata endpoint {Endpoint} failed: {Reason}.", SafeEndpoint(endpoint), reason);
            }
        }

        throw new InvalidOperationException(
            "Ely.by launch metadata could not be obtained, so Minecraft was not started. " + string.Join("; ", failures));
    }

    private async Task<MetadataResult> FetchMetadataAsync(Uri initialEndpoint, CancellationToken cancellationToken)
    {
        var endpoint = initialEndpoint;
        for (var redirects = 0; redirects < 4; redirects++)
        {
            RequireHttps(endpoint, "Ely.by metadata endpoint");
            using var response = await GetResponseAsync(endpoint, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"HTTP {(int)response.StatusCode} ({response.StatusCode})", null, response.StatusCode);

            var responseUri = response.RequestMessage?.RequestUri ?? endpoint;
            if (response.Headers.TryGetValues(ApiLocationHeader, out var values))
            {
                var location = values.FirstOrDefault();
                if (string.IsNullOrWhiteSpace(location) || !Uri.TryCreate(responseUri, location, out var discovered))
                    throw new InvalidDataException("invalid API-location header");
                RequireHttps(discovered, "discovered Ely.by API location");
                endpoint = discovered;
                continue;
            }

            var bytes = await ReadLimitedAsync(response.Content, _options.MaximumMetadataBytes, cancellationToken).ConfigureAwait(false);
            ValidateMetadata(bytes);
            return new MetadataResult(responseUri, bytes);
        }

        throw new InvalidDataException("too many API-location redirects");
    }

    private async Task<T> GetJsonAsync<T>(Uri endpoint, CancellationToken cancellationToken)
    {
        RequireHttps(endpoint, "authlib-injector artifact endpoint");
        using var response = await GetResponseAsync(endpoint, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"HTTP {(int)response.StatusCode} ({response.StatusCode})", null, response.StatusCode);
        var bytes = await ReadLimitedAsync(response.Content, 1024 * 1024, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<T>(bytes, _jsonOptions)
            ?? throw new InvalidDataException("The authlib-injector API returned an empty JSON document.");
    }

    private async Task<T> GetJsonWithRetriesAsync<T>(Uri endpoint, CancellationToken cancellationToken)
    {
        Exception? lastError = null;
        var attempts = Math.Max(1, _timeouts.Attempts);
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                return await GetJsonAsync<T>(endpoint, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (
                (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
                && IsRetryableArtifactFailure(ex))
            {
                lastError = ex;
                if (attempt < attempts)
                    _logger.LogWarning(ex, "Authlib-injector artifact request attempt {Attempt}/{Attempts} failed; retrying.", attempt, attempts);
            }
        }

        throw lastError ?? new InvalidOperationException("The authlib-injector artifact request failed.");
    }

    private async Task<HttpResponseMessage> GetResponseAsync(Uri endpoint, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timeouts.ResponseHeadersTimeout);
        try
        {
            var response = await _httpClient.GetAsync(endpoint, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            var finalEndpoint = response.RequestMessage?.RequestUri;
            if (finalEndpoint is null || finalEndpoint.Scheme != Uri.UriSchemeHttps)
            {
                response.Dispose();
                throw new InvalidDataException("An HTTP redirect resolved to a non-HTTPS endpoint.");
            }
            return response;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new DownloadTimeoutException("response headers", SafeEndpoint(endpoint));
        }
    }

    private async Task DownloadFileAsync(Uri endpoint, string destination, CancellationToken cancellationToken)
    {
        RequireHttps(endpoint, "authlib-injector download URL");
        using var response = await GetResponseAsync(endpoint, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var target = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
        var buffer = new byte[81920];
        while (true)
        {
            using var inactivity = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            inactivity.CancelAfter(_timeouts.InactivityTimeout);
            int read;
            try
            {
                read = await source.ReadAsync(buffer, inactivity.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new DownloadTimeoutException("inactivity", SafeEndpoint(endpoint));
            }
            if (read == 0) break;
            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }
        await target.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<byte[]> ReadLimitedAsync(HttpContent content, int maximumBytes, CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength > maximumBytes)
            throw new InvalidDataException($"response exceeded the {maximumBytes}-byte limit");

        await using var source = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var output = new MemoryStream();
        var buffer = new byte[16 * 1024];
        while (true)
        {
            using var inactivity = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            inactivity.CancelAfter(_timeouts.InactivityTimeout);
            int read;
            try
            {
                read = await source.ReadAsync(buffer, inactivity.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new DownloadTimeoutException("inactivity", "metadata response");
            }
            if (read == 0) break;
            if (output.Length + read > maximumBytes)
                throw new InvalidDataException($"response exceeded the {maximumBytes}-byte limit");
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }

    private static void ValidateMetadata(byte[] bytes)
    {
        try
        {
            using var json = JsonDocument.Parse(bytes);
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("meta", out var meta) || meta.ValueKind != JsonValueKind.Object
                || !meta.TryGetProperty("serverName", out var serverName) || serverName.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(serverName.GetString())
                || !meta.TryGetProperty("implementationName", out var implementation) || implementation.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(implementation.GetString())
                || !root.TryGetProperty("skinDomains", out var skinDomains) || skinDomains.ValueKind != JsonValueKind.Array
                || !root.TryGetProperty("signaturePublickey", out var publicKey) || publicKey.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(publicKey.GetString()))
                throw new InvalidDataException("incomplete Ely.by metadata JSON");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("invalid Ely.by metadata JSON", ex);
        }
    }

    private static void ValidateIndex(ArtifactIndexDto index)
    {
        if (index.Artifacts.Count == 0
            || index.Artifacts.Any(entry => string.IsNullOrWhiteSpace(entry.Version) || entry.BuildNumber <= 0))
            throw new InvalidDataException("The authlib-injector version catalog is incomplete.");
    }

    private static ArtifactDescriptor ValidateDescriptor(ArtifactDescriptor descriptor)
    {
        if (descriptor.BuildNumber <= 0 || string.IsNullOrWhiteSpace(descriptor.Version))
            throw new InvalidDataException("The authlib-injector artifact descriptor is incomplete.");
        RequireHttps(descriptor.DownloadUrl, "authlib-injector download URL");
        if (descriptor.Sha256.Length != 64 || descriptor.Sha256.Any(character => !Uri.IsHexDigit(character)))
            throw new InvalidDataException("The authlib-injector artifact descriptor has an invalid SHA-256 checksum.");
        return descriptor;
    }

    private async Task WriteDescriptorAsync(ArtifactDescriptor descriptor, AuthlibInjectorVersionMode mode, CancellationToken cancellationToken)
    {
        await WriteJsonAtomicallyAsync(DescriptorPath(descriptor.BuildNumber), descriptor, cancellationToken).ConfigureAwait(false);
        if (mode == AuthlibInjectorVersionMode.Latest)
            await WriteJsonAtomicallyAsync(LatestSelectionPath, new LatestSelectionDto { BuildNumber = descriptor.BuildNumber }, cancellationToken)
                .ConfigureAwait(false);
    }

    private async Task<ArtifactDescriptor?> FindCachedDescriptorAsync(Selection selection, CancellationToken cancellationToken)
    {
        if (selection.Mode == AuthlibInjectorVersionMode.Latest)
        {
            var latest = await ReadJsonFileAsync<LatestSelectionDto>(LatestSelectionPath, cancellationToken).ConfigureAwait(false);
            return latest is { BuildNumber: > 0 }
                ? ValidateCached(await ReadJsonFileAsync<ArtifactDescriptor>(DescriptorPath(latest.BuildNumber), cancellationToken).ConfigureAwait(false))
                : null;
        }

        foreach (var path in Directory.EnumerateFiles(_baseDirectory, "artifact-*.json"))
        {
            var descriptor = ValidateCached(await ReadJsonFileAsync<ArtifactDescriptor>(path, cancellationToken).ConfigureAwait(false));
            if (descriptor is not null && string.Equals(descriptor.Version, selection.Version, StringComparison.Ordinal))
                return descriptor;
        }
        return null;
    }

    private static ArtifactDescriptor? ValidateCached(ArtifactDescriptor? descriptor)
    {
        if (descriptor is null) return null;
        try { return ValidateDescriptor(descriptor); }
        catch { return null; }
    }

    private async Task WriteJsonAtomicallyAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        var tempPath = path + $".{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(tempPath, JsonSerializer.Serialize(value, _jsonOptions), cancellationToken).ConfigureAwait(false);
            File.Move(tempPath, path, true);
        }
        finally
        {
            TryDelete(tempPath);
        }
    }

    private async Task<T?> ReadJsonFileAsync<T>(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) return default;
        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<T>(json, _jsonOptions);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Ignoring invalid cached authlib-injector data at {Path}.", path);
            return default;
        }
    }

    private static async Task<bool> HasExpectedHashAsync(string path, string expected, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return string.Equals(Convert.ToHexString(hash), expected, StringComparison.OrdinalIgnoreCase);
    }

    private static void RequireHttps(Uri uri, string description)
    {
        if (!uri.IsAbsoluteUri || uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidDataException($"The {description} must use HTTPS.");
    }

    private static string SanitizeFailure(Exception exception) => exception switch
    {
        HttpRequestException { StatusCode: { } status } => $"HTTP {(int)status}",
        DownloadTimeoutException => "timed out",
        InvalidDataException invalid => invalid.Message,
        _ => exception.GetType().Name
    };

    private static bool IsRetryableArtifactFailure(Exception exception) => exception switch
    {
        DownloadTimeoutException => true,
        HttpRequestException { StatusCode: null } => true,
        HttpRequestException { StatusCode: HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests } => true,
        HttpRequestException { StatusCode: { } status } when (int)status >= 500 => true,
        _ => false
    };

    private static string SafeEndpoint(Uri endpoint) => endpoint.GetLeftPart(UriPartial.Path);

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { }
    }

    private string CatalogPath => Path.Combine(_baseDirectory, "catalog.json");
    private string LatestSelectionPath => Path.Combine(_baseDirectory, "latest-selection.json");
    private string DescriptorPath(int build) => Path.Combine(_baseDirectory, $"artifact-{build}.json");

    private sealed record Selection(AuthlibInjectorVersionMode Mode, string? Version);
    private sealed record MetadataResult(Uri ApiRoot, byte[] Bytes);
    private sealed class AuthlibSelectionException(string message) : InvalidOperationException(message);

    private sealed class ArtifactIndexDto
    {
        [JsonPropertyName("latest_build_number")]
        public int LatestBuildNumber { get; init; }

        [JsonPropertyName("artifacts")]
        public List<ArtifactIndexEntryDto> Artifacts { get; init; } = [];
    }

    private sealed class ArtifactIndexEntryDto
    {
        [JsonPropertyName("version")]
        public string Version { get; init; } = string.Empty;

        [JsonPropertyName("build_number")]
        public int BuildNumber { get; init; }
    }

    private sealed class LatestSelectionDto
    {
        [JsonPropertyName("build_number")]
        public int BuildNumber { get; init; }
    }

    private sealed class ArtifactDescriptor
    {
        [JsonPropertyName("build_number")]
        public int BuildNumber { get; init; }

        [JsonPropertyName("version")]
        public string Version { get; init; } = string.Empty;

        [JsonPropertyName("release_time")]
        public DateTimeOffset? ReleaseTime { get; init; }

        [JsonPropertyName("download_url")]
        public Uri DownloadUrl { get; init; } = null!;

        [JsonPropertyName("checksums")]
        public ArtifactChecksums Checksums { get; init; } = new();

        [JsonIgnore]
        public string Sha256 => Checksums.Sha256;
    }

    private sealed class ArtifactChecksums
    {
        [JsonPropertyName("sha256")]
        public string Sha256 { get; init; } = string.Empty;
    }
}
