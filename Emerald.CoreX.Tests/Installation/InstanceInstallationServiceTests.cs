using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using CmlLib.Core;
using CmlLib.Core.Files;
using CommunityToolkit.Mvvm.DependencyInjection;
using Emerald.CoreX.Installation;
using Emerald.CoreX.Services;
using Emerald.CoreX.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Emerald.CoreX.Tests.Installation;

[Collection(IocCollection.Name)]
public sealed class InstanceInstallationServiceTests
{
    [Fact]
    public async Task Verify_MigratesHealthyLegacyInstance_WithoutHttpRequests()
    {
        using var fixture = await LocalGameFixture.CreateAsync();
        var handler = new FailingHttpHandler();
        using var network = new NetworkCapabilityService(new HttpClient(handler));
        var service = CreateService(network);

        var report = await service.VerifyAsync(fixture.Game, IntegrityCheckLevel.Quick);

        Assert.True(report.CanLaunch);
        Assert.Equal(IntegrityCheckLevel.Full, report.Level);
        Assert.Equal(0, handler.RequestCount);
        Assert.True(File.Exists(Path.Combine(fixture.Game.Path.BasePath, ".emerald", "install-state.v1.json")));
    }

    [Fact]
    public async Task FullVerify_DetectsCorruptClient_AndPreflightBlocksLaunch()
    {
        using var fixture = await LocalGameFixture.CreateAsync();
        using var network = new NetworkCapabilityService(new HttpClient(new FailingHttpHandler()));
        var service = CreateService(network);
        await service.VerifyAsync(fixture.Game, IntegrityCheckLevel.Full);
        await File.WriteAllTextAsync(fixture.ClientPath, "corrupt");

        var report = await service.VerifyAsync(fixture.Game, IntegrityCheckLevel.Full);
        var readiness = await service.PrepareLaunchAsync(fixture.Game);

        Assert.Equal(InstanceInstallationState.NeedsRepair, report.State);
        Assert.Contains(report.Issues, issue => issue.Code is "wrong-size" or "hash-mismatch");
        Assert.False(readiness.CanLaunch);
    }

    [Fact]
    public async Task VerifiedInstaller_RejectsBadDownload_AndPreservesExistingFile()
    {
        var root = Path.Combine(Path.GetTempPath(), "emerald-installer-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var destination = Path.Combine(root, "client.jar");
        await File.WriteAllTextAsync(destination, "last-good-copy");
        var expected = Convert.ToHexString(SHA1.HashData("expected-copy"u8.ToArray())).ToLowerInvariant();
        using var network = new NetworkCapabilityService(new HttpClient(new StaticHttpHandler("bad-copy"u8.ToArray())));
        var installer = new VerifiedGameInstaller(new HttpClient(new StaticHttpHandler("bad-copy"u8.ToArray())), network);
        var file = new GameFile("client.jar") { Path = destination, Url = "https://invalid.example/client.jar", Hash = expected, Size = "expected-copy"u8.Length };

        await Assert.ThrowsAsync<AggregateException>(async () => await installer.Install([file], null, null, CancellationToken.None));

        Assert.Equal("last-good-copy", await File.ReadAllTextAsync(destination));
        Assert.Empty(Directory.GetFiles(root, "*.emerald-download-*"));
        Directory.Delete(root, true);
    }

    private static InstanceInstallationService CreateService(INetworkCapabilityService network)
        => new(NullLogger<InstanceInstallationService>.Instance, new InstallationStateStore(), network);

    private sealed class FailingHttpHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            throw new Xunit.Sdk.XunitException($"Local verification attempted HTTP: {request.RequestUri}");
        }
    }

    private sealed class StaticHttpHandler(byte[] content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) });
    }

    private sealed class LocalGameFixture : IDisposable
    {
        public required string Root { get; init; }
        public required string ClientPath { get; init; }
        public required Game Game { get; init; }

        public static async Task<LocalGameFixture> CreateAsync()
        {
            var root = Path.Combine(Path.GetTempPath(), "emerald-install-tests", Guid.NewGuid().ToString("N"));
            var path = new MinecraftPath(root);
            const string versionName = "test-version";
            var versionDirectory = Path.Combine(path.Versions, versionName);
            Directory.CreateDirectory(versionDirectory);
            var clientPath = Path.Combine(versionDirectory, versionName + ".jar");
            var bytes = "healthy-client"u8.ToArray();
            await File.WriteAllBytesAsync(clientPath, bytes);
            var sha1 = Convert.ToHexString(SHA1.HashData(bytes)).ToLowerInvariant();
            var metadata = new
            {
                id = versionName,
                downloads = new { client = new { sha1, size = bytes.Length, url = "https://invalid.example/client.jar" } },
                libraries = Array.Empty<object>()
            };
            await File.WriteAllTextAsync(
                Path.Combine(versionDirectory, versionName + ".json"),
                JsonSerializer.Serialize(metadata));

            var game = new Game(path, new Emerald.CoreX.Versions.Version
            {
                DisplayName = "Test",
                BasedOn = versionName,
                RealVersion = versionName,
                ReleaseType = "release"
            }, globalGameSettingsService: new TestGlobalGameSettingsService());
            return new() { Root = root, ClientPath = clientPath, Game = game };
        }

        public void Dispose() { if (Directory.Exists(Root)) Directory.Delete(Root, true); }
    }

    private sealed class TestGlobalGameSettingsService : IGlobalGameSettingsService
    {
        public Emerald.CoreX.Models.GameSettings Settings { get; } = new();
        public Emerald.CoreX.Models.GameSettings CloneCurrent() => Settings.Clone();
        public void LoadForBasePath(string basePath) { }
        public void Save() { }
    }
}
