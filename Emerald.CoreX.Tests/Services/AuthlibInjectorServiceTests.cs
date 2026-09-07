using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Emerald.CoreX.Installation;
using Emerald.CoreX.Services.Auth.Authlib;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Emerald.CoreX.Tests.Services;

public sealed class AuthlibInjectorServiceTests : IDisposable
{
    private static readonly byte[] JarBytes = Encoding.UTF8.GetBytes("verified authlib injector test jar");
    private static readonly byte[] MetadataBytes = Encoding.UTF8.GetBytes(
        " {\"meta\":{\"serverName\":\"Ely.by\",\"implementationName\":\"ely.by\"},\"skinDomains\":[\".ely.by\"],\"signaturePublickey\":\"test-key\"} ");
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"emerald-authlib-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task Recommended_PrimaryFailureUsesFallbackAndPreservesExactMetadataBytes()
    {
        var handler = CreateHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/artifacts.json" => Json(IndexJson()),
            "/artifact/56.json" => Json(DescriptorJson()),
            "/artifact/56/authlib-injector-1.2.8.jar" => Bytes(JarBytes),
            "/" when request.RequestUri.Host == "ely.by" => new(HttpStatusCode.InternalServerError),
            "/api/authlib-injector" => Bytes(MetadataBytes, "application/json"),
            _ => new(HttpStatusCode.NotFound)
        });

        var result = await CreateService(handler).PrepareLaunchAsync();

        Assert.Equal("1.2.8", result.Version);
        Assert.Equal(56, result.BuildNumber);
        Assert.Equal("https://account.ely.by/api/authlib-injector", result.ApiRoot.AbsoluteUri);
        Assert.Equal(2, result.JvmArguments.Count);
        Assert.Equal($"-Dauthlibinjector.yggdrasil.prefetched={Convert.ToBase64String(MetadataBytes)}", result.JvmArguments[1]);
        Assert.Contains("-javaagent:", result.JvmArguments[0]);
        Assert.EndsWith("=https://account.ely.by/api/authlib-injector", result.JvmArguments[0]);
    }

    [Fact]
    public async Task PrimaryAliHeaderResolvesRelativeHttpsLocation()
    {
        var handler = CreateHandler(request =>
        {
            if (IsArtifactRequest(request)) return ArtifactResponse(request);
            if (request.RequestUri!.Host == "ely.by" && request.RequestUri.AbsolutePath == "/")
            {
                var response = Bytes([], "application/json");
                response.Headers.Add("X-Authlib-Injector-API-Location", "/authlib");
                return response;
            }
            return request.RequestUri.AbsolutePath == "/authlib"
                ? Bytes(MetadataBytes, "application/json")
                : new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var result = await CreateService(handler).PrepareLaunchAsync();

        Assert.Equal("https://ely.by/authlib", result.ApiRoot.AbsoluteUri);
        Assert.EndsWith("=https://ely.by/authlib", result.JvmArguments[0]);
    }

    [Fact]
    public async Task PrimaryTimeoutFallsBackToAccountEndpoint()
    {
        var handler = new AsyncTestHandler(async (request, cancellationToken) =>
        {
            if (request.RequestUri!.Host == "ely.by")
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            if (IsArtifactRequest(request)) return ArtifactResponse(request);
            return Bytes(MetadataBytes, "application/json");
        });

        var result = await CreateService(handler, responseHeadersTimeout: TimeSpan.FromMilliseconds(30)).PrepareLaunchAsync();

        Assert.Equal("account.ely.by", result.ApiRoot.Host);
    }

    [Fact]
    public async Task InvalidPrimaryJsonFallsBackToAccountEndpoint()
    {
        var handler = CreateHandler(request =>
        {
            if (IsArtifactRequest(request)) return ArtifactResponse(request);
            return request.RequestUri!.Host == "ely.by"
                ? Json("{not-json")
                : Bytes(MetadataBytes, "application/json");
        });

        var result = await CreateService(handler).PrepareLaunchAsync();

        Assert.Equal("account.ely.by", result.ApiRoot.Host);
    }

    [Fact]
    public async Task NonHttpsAliLocationIsRejectedAndFallsBack()
    {
        var handler = CreateHandler(request =>
        {
            if (IsArtifactRequest(request)) return ArtifactResponse(request);
            if (request.RequestUri!.Host == "ely.by")
            {
                var response = Bytes([], "application/json");
                response.Headers.Add("X-Authlib-Injector-API-Location", "http://account.ely.by/api/authlib-injector");
                return response;
            }
            return Bytes(MetadataBytes, "application/json");
        });

        var result = await CreateService(handler).PrepareLaunchAsync();

        Assert.Equal("https://account.ely.by/api/authlib-injector", result.ApiRoot.AbsoluteUri);
    }

    [Theory]
    [InlineData(AuthlibInjectorVersionMode.Latest, null, "/artifact/latest.json")]
    [InlineData(AuthlibInjectorVersionMode.Custom, "1.2.8", "/artifact/56.json")]
    public async Task VersionModesResolveThroughOfficialEndpoints(
        AuthlibInjectorVersionMode mode,
        string? customVersion,
        string expectedDescriptorPath)
    {
        var requestedPaths = new ConcurrentBag<string>();
        var handler = CreateHandler(request =>
        {
            requestedPaths.Add(request.RequestUri!.AbsolutePath);
            if (IsArtifactRequest(request)) return ArtifactResponse(request);
            return MetadataResponse(request);
        });

        var result = await CreateService(handler, mode, customVersion).PrepareLaunchAsync();

        Assert.Equal(56, result.BuildNumber);
        Assert.Contains(expectedDescriptorPath, requestedPaths);
    }

    [Fact]
    public async Task UnknownCustomVersionIsRejectedWithoutArbitraryDownload()
    {
        var handler = CreateHandler(request => request.RequestUri!.AbsolutePath == "/artifacts.json"
            ? Json(IndexJson())
            : new HttpResponseMessage(HttpStatusCode.NotFound));

        var exception = await Assert.ThrowsAnyAsync<InvalidOperationException>(() =>
            CreateService(handler, AuthlibInjectorVersionMode.Custom, "9.9.9").PrepareLaunchAsync());

        Assert.Contains("not present in the official catalog", exception.Message);
    }

    [Fact]
    public async Task CorruptedCachedJarIsRedownloadedAndReverified()
    {
        var jarDownloads = 0;
        var handler = CreateHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith(".jar", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref jarDownloads);
                return Bytes(JarBytes);
            }
            if (IsArtifactRequest(request)) return ArtifactResponse(request);
            return MetadataResponse(request);
        });
        var service = CreateService(handler);
        var first = await service.PrepareLaunchAsync();
        await File.WriteAllTextAsync(first.JarPath, "corrupted");

        var second = await service.PrepareLaunchAsync();

        Assert.Equal(2, jarDownloads);
        Assert.Equal(JarBytes, await File.ReadAllBytesAsync(second.JarPath));
    }

    [Fact]
    public async Task ArtifactApiOutageUsesCachedDescriptorButStillFetchesFreshMetadata()
    {
        var artifactOnline = true;
        var metadataRequests = 0;
        var handler = CreateHandler(request =>
        {
            if (request.RequestUri!.Host == "authlib-injector.yushi.moe")
                return artifactOnline ? ArtifactResponse(request) : new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            Interlocked.Increment(ref metadataRequests);
            return MetadataResponse(request);
        });
        var service = CreateService(handler);
        await service.PrepareLaunchAsync();
        artifactOnline = false;

        var result = await service.PrepareLaunchAsync();

        Assert.True(result.UsedCachedDescriptor);
        Assert.Equal(2, metadataRequests);
    }

    [Fact]
    public async Task InvalidDownloadNeverReplacesCacheAndTemporaryFileIsRemoved()
    {
        var handler = CreateHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith(".jar", StringComparison.Ordinal))
                return Bytes(Encoding.UTF8.GetBytes("wrong jar"));
            if (IsArtifactRequest(request)) return ArtifactResponse(request);
            return MetadataResponse(request);
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService(handler, attempts: 1).PrepareLaunchAsync());

        Assert.Empty(Directory.EnumerateFiles(_root, "*.tmp"));
        Assert.False(File.Exists(Path.Combine(_root, "authlib-injector-56.jar")));
    }

    [Fact]
    public async Task ConcurrentPreparationsShareOneVerifiedJarDownload()
    {
        var jarDownloads = 0;
        var handler = CreateHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith(".jar", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref jarDownloads);
                return Bytes(JarBytes);
            }
            if (IsArtifactRequest(request)) return ArtifactResponse(request);
            return MetadataResponse(request);
        });
        var service = CreateService(handler);

        await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => service.PrepareLaunchAsync()));

        Assert.Equal(1, jarDownloads);
    }

    [Fact]
    public async Task OversizedPrimaryMetadataFallsBackAndBothFailuresStopPreparation()
    {
        var fallbackWorks = true;
        var handler = CreateHandler(request =>
        {
            if (IsArtifactRequest(request)) return ArtifactResponse(request);
            if (request.RequestUri!.Host == "ely.by") return Bytes(new byte[257]);
            return fallbackWorks ? Bytes(MetadataBytes, "application/json") : new HttpResponseMessage(HttpStatusCode.BadGateway);
        });
        var options = DefaultOptions with { MaximumMetadataBytes = 256 };
        var service = CreateService(handler, options: options);

        var fallback = await service.PrepareLaunchAsync();
        Assert.Equal("account.ely.by", fallback.ApiRoot.Host);
        fallbackWorks = false;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.PrepareLaunchAsync());
        Assert.Contains("Minecraft was not started", exception.Message);
        Assert.Contains("HTTP 502", exception.Message);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private AuthlibInjectorService CreateService(
        HttpMessageHandler handler,
        AuthlibInjectorVersionMode mode = AuthlibInjectorVersionMode.Recommended,
        string? customVersion = null,
        int attempts = 2,
        AuthlibInjectorOptions? options = null,
        TimeSpan? responseHeadersTimeout = null)
        => new(
            NullLogger<AuthlibInjectorService>.Instance,
            new TestSettings(mode, customVersion),
            options ?? DefaultOptions,
            _root,
            new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan },
            new DownloadTimeouts
            {
                Attempts = attempts,
                ResponseHeadersTimeout = responseHeadersTimeout ?? TimeSpan.FromSeconds(2),
                InactivityTimeout = TimeSpan.FromSeconds(2)
            });

    private static AuthlibInjectorOptions DefaultOptions => new("1.2.8");

    private static HttpMessageHandler CreateHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        => new TestHandler(responseFactory);

    private static bool IsArtifactRequest(HttpRequestMessage request)
        => request.RequestUri!.Host == "authlib-injector.yushi.moe";

    private static HttpResponseMessage ArtifactResponse(HttpRequestMessage request) => request.RequestUri!.AbsolutePath switch
    {
        "/artifacts.json" => Json(IndexJson()),
        "/artifact/56.json" or "/artifact/latest.json" => Json(DescriptorJson()),
        "/artifact/56/authlib-injector-1.2.8.jar" => Bytes(JarBytes),
        _ => new(HttpStatusCode.NotFound)
    };

    private static HttpResponseMessage MetadataResponse(HttpRequestMessage request)
        => request.RequestUri!.Host == "ely.by"
            ? Bytes(MetadataBytes, "application/json")
            : new HttpResponseMessage(HttpStatusCode.NotFound);

    private static string IndexJson()
        => "{\"latest_build_number\":56,\"artifacts\":[{\"build_number\":56,\"version\":\"1.2.8\"}]}";

    private static string DescriptorJson()
        => System.Text.Json.JsonSerializer.Serialize(new
        {
            build_number = 56,
            version = "1.2.8",
            release_time = "2026-07-11T17:14:13Z",
            download_url = "https://authlib-injector.yushi.moe/artifact/56/authlib-injector-1.2.8.jar",
            checksums = new { sha256 = Convert.ToHexString(SHA256.HashData(JarBytes)).ToLowerInvariant() }
        });

    private static HttpResponseMessage Json(string json)
        => Bytes(Encoding.UTF8.GetBytes(json), "application/json");

    private static HttpResponseMessage Bytes(byte[] bytes, string mediaType = "application/octet-stream")
        => new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) { Headers = { ContentType = new(mediaType) } } };

    private sealed record TestSettings(AuthlibInjectorVersionMode VersionMode, string? CustomVersion)
        : IAuthlibInjectorSettings;

    private sealed class TestHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = responseFactory(request);
            response.RequestMessage ??= request;
            return Task.FromResult(response);
        }
    }

    private sealed class AsyncTestHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await responseFactory(request, cancellationToken);
            response.RequestMessage ??= request;
            return response;
        }
    }
}
