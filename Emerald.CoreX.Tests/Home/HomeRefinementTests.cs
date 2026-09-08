using System.Net;
using System.Net.Sockets;
using System.Text;
using Emerald.CoreX.Runtime;
using Emerald.CoreX.Services.Servers;
using Emerald.CoreX.Installation;
using Emerald.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Emerald.CoreX.Tests.Home;

public sealed class HomeRefinementTests
{
    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("::1", true)]
    [InlineData("10.1.2.3", true)]
    [InlineData("172.31.2.3", true)]
    [InlineData("172.32.2.3", false)]
    [InlineData("192.168.1.5", true)]
    [InlineData("169.254.1.5", true)]
    [InlineData("fe80::1", true)]
    [InlineData("fd00::1", true)]
    [InlineData("::ffff:192.168.1.5", true)]
    [InlineData("8.8.8.8", false)]
    public void PrivateAddressClassification(string address, bool expected)
        => Assert.Equal(expected, LocalJavaServerStatus.IsPrivate(IPAddress.Parse(address)));

    [Theory]
    [InlineData("localhost")]
    [InlineData("minecraft.local")]
    [InlineData("my-server")]
    public async Task LocalNamesNeverRequirePublicLookup(string host)
        => Assert.True(await LocalJavaServerStatus.IsLocalAsync(host, CancellationToken.None));

    [Fact]
    public async Task LocalStatus_HandlesFragmentedPacketsCachesAndKeepsStaleData()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var address = new MinecraftServerAddress("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var server = ReplyOnceAsync(listener, deadline.Token);
        var handler = new RejectHttpHandler();
        var service = new ServerStatusService(new HttpClient(handler), new Network(), NullLogger<ServerStatusService>.Instance);
        var first = await service.GetStatusAsync(address, cancellationToken: deadline.Token);
        await server;
        listener.Stop();
        Assert.Equal(ServerStatusState.Online, first.State);
        Assert.Equal(ServerStatusSource.Local, first.Source);
        Assert.Equal(3, first.Players);
        Assert.Equal("Hello world", first.Motd);
        Assert.Null(first.IconDataUrl);
        Assert.NotNull(first.LatencyMilliseconds);
        Assert.Same(first, await service.GetStatusAsync(address));
        var stale = await service.GetStatusAsync(address, true);
        Assert.Equal(ServerStatusState.Stale, stale.State);
        Assert.Equal(3, stale.Players);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task LocalStatus_CancellationDoesNotBecomeAnOfflineResult()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var service = new ServerStatusService(new HttpClient(new RejectHttpHandler()), new Network(), NullLogger<ServerStatusService>.Instance);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetStatusAsync(new("localhost", 25565), cancellationToken: cancellation.Token));
    }

    [Theory]
    [InlineData("data:image/png;base64,not-base64")]
    [InlineData("https://example.com/icon.png")]
    public void InvalidFaviconFallsBack(string value) => Assert.False(LocalJavaServerStatus.ValidIcon(value));

    [Fact]
    public void Analytics_ClipsRankingsAndGroupsRenamedInstances()
    {
        using var data = new HistoryFixture();
        var now = new DateTimeOffset(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);
        data.Service.RecordSession(data.Session(now.AddDays(-7).AddHours(11), now.AddDays(-6).AddHours(-11), "Old name"));
        data.Service.RecordSession(data.Session(now.AddHours(-2), now.AddHours(-1), "New name"));
        var result = data.Service.GetAnalytics(PlaytimeScope.AllEmerald, PlaytimeRange.SevenDays, now, TimeZoneInfo.Utc);
        Assert.Equal(TimeSpan.FromHours(2), result.TotalPlaytime);
        var ranking = Assert.Single(result.InstanceRanking);
        Assert.Equal(result.TotalPlaytime, ranking.Duration);
        Assert.Equal("New name", ranking.DisplayName);
        Assert.Equal(2, result.ActiveDayCount);
    }

    [Fact]
    public void Analytics_ActiveOverlayDoesNotChangeCompletedStatisticsOrDuplicateOnExit()
    {
        using var data = new HistoryFixture();
        var now = new DateTimeOffset(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);
        data.Service.RecordSession(data.Session(now.AddHours(-4), now.AddHours(-3)));
        var active = data.Session(now.AddHours(-2), now);
        var before = data.Service.GetAnalytics(PlaytimeScope.AllEmerald, PlaytimeRange.SevenDays, now, TimeZoneInfo.Utc, [active]);
        Assert.Equal(TimeSpan.FromHours(3), before.TotalPlaytime);
        Assert.Equal(1, before.CompletedSessionCount);
        Assert.Equal(TimeSpan.FromHours(1), before.AverageCompletedSession);
        data.Service.RecordSession(active);
        var after = data.Service.GetAnalytics(PlaytimeScope.AllEmerald, PlaytimeRange.SevenDays, now, TimeZoneInfo.Utc, [active]);
        Assert.Equal(before.TotalPlaytime, after.TotalPlaytime);
        Assert.Equal(2, after.CompletedSessionCount);
    }

    [Fact]
    public void Analytics_ActiveOverlayRespectsInstanceScope()
    {
        using var data = new HistoryFixture();
        var now = DateTimeOffset.UtcNow;
        var active = data.Session(now.AddHours(-1), now);
        var result = data.Service.GetAnalytics(PlaytimeScope.ForInstance(data.Path, Guid.NewGuid()), PlaytimeRange.AllTime, now, TimeZoneInfo.Utc, [active]);
        Assert.Equal(TimeSpan.Zero, result.TotalPlaytime);
    }

    [Theory]
    [InlineData(2026, 3, 8, 6, 8, 1, 3)]
    [InlineData(2026, 11, 1, 5, 7, 1, 1)]
    public void Analytics_RecalculatesLocalHourAcrossDst(int year, int month, int day, int startHour, int endHour, int firstHour, int lastHour)
    {
        using var data = new HistoryFixture();
        var start = new DateTimeOffset(year, month, day, startHour, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(year, month, day, endHour, 0, 0, TimeSpan.Zero);
        data.Service.RecordSession(data.Session(start, end));
        var result = data.Service.GetAnalytics(PlaytimeScope.AllEmerald, PlaytimeRange.AllTime, end, TimeZoneInfo.FindSystemTimeZoneById("America/New_York"));
        Assert.Equal(TimeSpan.FromHours(2), result.TotalPlaytime);
        Assert.All(result.HeatmapBuckets, x => Assert.True(x.Hour == firstHour || x.Hour == lastHour));
        Assert.Equal(TimeSpan.FromHours(2), TimeSpan.FromTicks(result.HeatmapBuckets.Sum(x => x.Duration.Ticks)));
    }

    private static async Task ReplyOnceAsync(TcpListener listener, CancellationToken token)
    {
        using var client = await listener.AcceptTcpClientAsync(token);
        using var stream = client.GetStream();
        await ReadPacketAsync(stream, token); // handshake
        await ReadPacketAsync(stream, token); // status request
        var json = Encoding.UTF8.GetBytes("""{"version":{"name":"Test","protocol":763},"players":{"online":3,"max":10},"description":{"text":"Hello ","extra":[{"text":"world"}]},"favicon":"broken"}""");
        using var payload = new MemoryStream();
        payload.WriteByte(0); WriteVarInt(payload, json.Length); payload.Write(json);
        using var packet = new MemoryStream(); WriteVarInt(packet, (int)payload.Length); packet.Write(payload.ToArray());
        foreach (var b in packet.ToArray()) await stream.WriteAsync(new byte[] { b }, token);
        var ping = await ReadPacketAsync(stream, token);
        using var pong = new MemoryStream(); WriteVarInt(pong, ping.Length); pong.Write(ping);
        await stream.WriteAsync(pong.ToArray(), token);
    }
    private static async Task<byte[]> ReadPacketAsync(Stream stream, CancellationToken token)
    {
        var length = 0; var shift = 0; var single = new byte[1];
        do { await stream.ReadExactlyAsync(single, token); length |= (single[0] & 127) << shift; shift += 7; } while ((single[0] & 128) != 0);
        var data = new byte[length]; await stream.ReadExactlyAsync(data, token); return data;
    }
    private static void WriteVarInt(Stream stream, int value)
    {
        do { var part = (byte)(value & 127); value >>= 7; stream.WriteByte(value == 0 ? part : (byte)(part | 128)); } while (value != 0);
    }
    private sealed class RejectHttpHandler : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) { Calls++; throw new InvalidOperationException("A private endpoint reached HTTP."); }
    }
    private sealed class Network : INetworkCapabilityService
    {
        public event EventHandler<NetworkCapabilitySnapshot>? Changed { add { } remove { } }
        public NetworkCapabilitySnapshot GetSnapshot(NetworkCapability capability) => new(capability, NetworkAvailabilityState.Available, DateTimeOffset.UtcNow);
        public Task<NetworkCapabilitySnapshot> ProbeAsync(NetworkCapability capability, CancellationToken cancellationToken = default) => Task.FromResult(GetSnapshot(capability));
        public void ReportSuccess(NetworkCapability capability) { }
        public void ReportFailure(NetworkCapability capability, Exception exception) { }
        public void Dispose() { }
    }
    private sealed class HistoryFixture : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "emerald-refinement-" + Guid.NewGuid());
        private readonly Guid _instance = Guid.NewGuid();
        public InstancePlaytimeService Service { get; }
        public HistoryFixture() { Directory.CreateDirectory(Path); Service = new(new BaseSettingsService(NullLogger<BaseSettingsService>.Instance, Path), NullLogger<InstancePlaytimeService>.Instance); }
        public InstancePlaytimeSession Session(DateTimeOffset start, DateTimeOffset end, string name = "Instance") => new() { InstanceId = _instance, InstanceNameSnapshot = name, InstancePath = System.IO.Path.Combine(Path, "instance"), BasePathSnapshot = Path, StartedAt = start, EndedAt = end };
        public void Dispose() => Directory.Delete(Path, true);
    }
}
