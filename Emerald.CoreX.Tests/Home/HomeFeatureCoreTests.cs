using CmlLib.Core.ProcessBuilder;
using CmlLib.Core;
using Emerald.CoreX.Models;
using Emerald.CoreX.Runtime;
using Emerald.CoreX.Services.Servers;
using Emerald.CoreX.Services.Worlds;
using Emerald.Services;
using Emerald.CoreX.Tests.Support;
using fNbt;
using Microsoft.Extensions.Logging.Abstractions;
using Emerald.CoreX.Installation;
using Emerald.CoreX.Services;
using Xunit;

namespace Emerald.CoreX.Tests.Home;

[Collection(IocCollection.Name)]
public sealed class HomeFeatureCoreTests
{
    [Theory]
    [InlineData("play.example.net", "play.example.net", 25565)]
    [InlineData("play.example.net:25570", "play.example.net", 25570)]
    [InlineData("127.0.0.1:25566", "127.0.0.1", 25566)]
    [InlineData("[2001:db8::1]:25567", "2001:db8::1", 25567)]
    [InlineData("2001:db8::1", "2001:db8::1", 25565)]
    public void ServerAddressParser_AcceptsSupportedForms(string input, string host, int port)
    {
        Assert.True(MinecraftServerAddressParser.TryParse(input, out var parsed, out var error), error);
        Assert.Equal(host, parsed.Host); Assert.Equal(port, parsed.Port);
    }

    [Theory]
    [InlineData("")]
    [InlineData("https://play.example.net")]
    [InlineData("play.example.net:0")]
    [InlineData("play.example.net:65536")]
    [InlineData("[2001:db8::1")]
    public void ServerAddressParser_RejectsInvalidForms(string input)
        => Assert.False(MinecraftServerAddressParser.TryParse(input, out _, out _));

    [Fact]
    public void LaunchTargets_ClearCompetingOptions()
    {
        var options = new MLaunchOption { ServerIp = "old", ServerPort = 24444, QuickPlaySingleplayer = "old-world", QuickPlayRealms = "realm" };
        Game.ApplyLaunchTarget(options, MinecraftLaunchTarget.MainMenu);
        Assert.Null(options.ServerIp); Assert.Null(options.QuickPlaySingleplayer); Assert.Null(options.QuickPlayRealms);

        Game.ApplyLaunchTarget(options, MinecraftLaunchTarget.ForServer("new.example.net", 25570));
        Assert.Equal("new.example.net", options.ServerIp); Assert.Equal(25570, options.ServerPort); Assert.Null(options.QuickPlaySingleplayer);

        Game.ApplyLaunchTarget(options, MinecraftLaunchTarget.ForWorld("World Folder"));
        Assert.Null(options.ServerIp); Assert.Equal("World Folder", options.QuickPlaySingleplayer); Assert.Null(options.QuickPlayRealms);
    }

    [Fact]
    public void Analytics_SplitsCrossMidnightAndCalculatesPatterns()
    {
        using var temp = new TemporaryDirectory();
        var settings = new BaseSettingsService(NullLogger<BaseSettingsService>.Instance, temp.Path);
        var service = new InstancePlaytimeService(settings, NullLogger<InstancePlaytimeService>.Instance);
        var id = Guid.NewGuid(); var basePath = Path.Combine(temp.Path, "minecraft"); var instancePath = Path.Combine(basePath, "Instances", "One");
        service.RecordSession(new InstancePlaytimeSession { InstanceId = id, BasePathSnapshot = basePath, InstancePath = instancePath, InstanceNameSnapshot = "One", StartedAt = new DateTimeOffset(2026, 1, 1, 23, 30, 0, TimeSpan.Zero), EndedAt = new DateTimeOffset(2026, 1, 2, 0, 30, 0, TimeSpan.Zero) });
        var result = service.GetAnalytics(PlaytimeScope.ForInstance(basePath, id), PlaytimeRange.AllTime, new DateTimeOffset(2026, 1, 2, 12, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc);
        Assert.Equal(TimeSpan.FromHours(1), result.TotalPlaytime); Assert.Equal(2, result.ActiveDayCount); Assert.Equal(2, result.DailyBuckets.Count); Assert.Equal(2, result.HeatmapBuckets.Count); Assert.Equal(1, result.CompletedSessionCount);
    }

    [Fact]
    public void SavedServers_DeduplicatesCanonicalAddressAndKeepsCustomName()
    {
        using var temp = new TemporaryDirectory(); var settings = new BaseSettingsService(NullLogger<BaseSettingsService>.Instance, temp.Path); var service = new SavedServerService(settings, NullLogger<SavedServerService>.Instance);
        service.Save(new SavedServer { Name = "First", Host = "PLAY.EXAMPLE.NET", Port = 25565 });
        service.Save(new SavedServer { Name = "My server", Host = "play.example.net", Port = 25565 });
        var saved = Assert.Single(service.GetAll()); Assert.Equal("My server", saved.Name);
    }

    [Fact]
    public async Task WorldService_ReadsModernMetadataAndUsesFolderIdentity()
    {
        using var temp = new TemporaryDirectory(); var instance = Path.Combine(temp.Path, "instance"); var worldPath = Path.Combine(instance, "saves", "Folder Identity"); Directory.CreateDirectory(worldPath);
        var data = new NbtCompound("Data") { new NbtString("LevelName", "Pretty World"), new NbtLong("LastPlayed", 1_700_000_000_000), new NbtInt("GameType", 1), new NbtByte("Difficulty", 2), new NbtInt("DataVersion", 3953), new NbtCompound("Version") { new NbtString("Name", "1.21.4") }, new NbtCompound("WorldGenSettings") { new NbtLong("seed", 12345) } };
        new NbtFile(new NbtCompound("") { data }).SaveToFile(Path.Combine(worldPath, "level.dat"), NbtCompression.GZip);
        var game = new Game(new MinecraftPath(instance), new Emerald.CoreX.Versions.Version { DisplayName = "1.21.4", BasedOn = "1.21.4", ReleaseType = "release" }, globalGameSettingsService: new TestGlobalGameSettingsService());
        var service = new MinecraftWorldService(NullLogger<MinecraftWorldService>.Instance);
        var world = Assert.Single(await service.ScanAsync(game));
        Assert.Equal("Folder Identity", world.FolderName); Assert.Equal("Pretty World", world.DisplayName); Assert.Equal(12345, world.Seed); Assert.True(world.IsReadable);
    }

    [Fact]
    public async Task DirectoryService_CachesIdenticalQueriesAndSendsJavaFilters()
    {
        var handler = new RecordingHandler("""{"servers":[{"name":"A","host":"play.example.net","port":25570,"tagline":"A friendly server","version":"1.21.4","players":{"online":12,"max":100},"votes":{"month":42},"rating":{"average":4.5},"tags":["survival"]}],"total":1}""");
        var service = new ServerDirectoryService(new HttpClient(handler), new FakeNetworkCapabilityService(), NullLogger<ServerDirectoryService>.Instance);
        var first = await service.SearchAsync(new("emerald", Version: "1.21", PageSize: 20)); var second = await service.SearchAsync(new("emerald", Version: "1.21", PageSize: 20));
        var server = Assert.Single(first.Servers); Assert.Single(second.Servers); Assert.Equal(1, handler.Calls); Assert.Contains("edition=java", handler.LastRequest!.RequestUri!.Query); Assert.Contains("q=emerald", handler.LastRequest.RequestUri.Query); Assert.Contains("per=20", handler.LastRequest.RequestUri.Query); Assert.Equal(12, server.Players); Assert.Equal(100, server.MaxPlayers); Assert.Equal(42, server.Votes); Assert.Equal(4.5, server.Rating);
    }

    [Fact]
    public async Task StatusService_UsesRequiredUserAgentAndResolvedEndpoint()
    {
        var handler = new RecordingHandler("""{"online":true,"ip":"10.0.0.2","port":25580,"version":"1.21.4","players":{"online":3,"max":20},"motd":{"clean":["Hello"]}}""");
        var service = new ServerStatusService(new HttpClient(handler), new FakeNetworkCapabilityService(), NullLogger<ServerStatusService>.Instance, new PublicAddressClassifier());
        var result = await service.GetStatusAsync(new("play.example.net", 25565));
        Assert.Equal(ServerStatusState.Online, result.State); Assert.Equal("10.0.0.2", result.ResolvedIp); Assert.Equal(25580, result.ResolvedPort); Assert.Contains("Emerald-Launcher", handler.LastRequest!.Headers.UserAgent.ToString()); Assert.EndsWith("play.example.net", handler.LastRequest.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public void QuickProfiles_KeepBrokenReferencesAndReportAttention()
    {
        using var temp = new TemporaryDirectory(); var settings = new InMemoryMinecraftBaseSettingsService(); settings.UseBasePath(temp.Path); var service = new QuickProfileService(settings, NullLogger<QuickProfileService>.Instance);
        var profile = service.Save(new QuickProfile { Name = "Missing", InstanceId = Guid.NewGuid(), AccountUniqueId = "missing", TargetKind = MinecraftLaunchTargetKind.Server, SavedServerId = Guid.NewGuid() });
        var validation = service.Validate(profile, [], [], []);
        Assert.False(validation.IsValid); Assert.Contains(validation.Messages, x => x.Contains("instance")); Assert.Contains(profile, service.GetAll());
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"emerald-home-tests-{Guid.NewGuid():N}");
        public TemporaryDirectory() => Directory.CreateDirectory(Path);
        public void Dispose() { if (Directory.Exists(Path)) Directory.Delete(Path, true); }
    }
    private sealed class TestGlobalGameSettingsService : Emerald.CoreX.Services.IGlobalGameSettingsService
    {
        public GameSettings Settings { get; } = new();
        public GameSettings CloneCurrent() => Settings.Clone();
        public void LoadForBasePath(string basePath) { }
        public void Save() { }
    }
    private sealed class PublicAddressClassifier : IServerAddressClassifier { public Task<bool> IsLocalAsync(string host, CancellationToken token) => Task.FromResult(false); }
    private sealed class RecordingHandler(string json) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public HttpRequestMessage? LastRequest { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) { Calls++; LastRequest = request; return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(json) }); }
    }
    private sealed class FakeNetworkCapabilityService : INetworkCapabilityService
    {
        public event EventHandler<NetworkCapabilitySnapshot>? Changed { add { } remove { } }
        public NetworkCapabilitySnapshot GetSnapshot(NetworkCapability capability) => new(capability, NetworkAvailabilityState.Available, DateTimeOffset.UtcNow);
        public Task<NetworkCapabilitySnapshot> ProbeAsync(NetworkCapability capability, CancellationToken cancellationToken = default) => Task.FromResult(GetSnapshot(capability));
        public void ReportSuccess(NetworkCapability capability) { }
        public void ReportFailure(NetworkCapability capability, Exception exception) { }
        public void Dispose() { }
    }
}
