using System.Net;
using Emerald.CoreX.Installation;
using Xunit;

namespace Emerald.CoreX.Tests.Installation;

public sealed class NetworkCapabilityServiceTests
{
    [Fact]
    public async Task ProbeAsync_SharesAnInFlightRequestPerCapability()
    {
        using var handler = new BlockingHandler();
        using var service = new NetworkCapabilityService(new HttpClient(handler));

        var first = service.ProbeAsync(NetworkCapability.MinecraftMetadata);
        await handler.Started.Task;
        var second = service.ProbeAsync(NetworkCapability.MinecraftMetadata);

        Assert.Equal(1, handler.RequestCount);
        handler.Release();

        var results = await Task.WhenAll(first, second);
        Assert.All(results, result => Assert.Equal(NetworkAvailabilityState.Available, result.State));
        Assert.Equal(NetworkAvailabilityState.Available, service.GetSnapshot(NetworkCapability.MinecraftMetadata).EffectiveState);
    }

    [Fact]
    public async Task ProbeAsync_ClassifiesServerFailureAsDegraded()
    {
        using var service = new NetworkCapabilityService(new HttpClient(new StatusHandler(HttpStatusCode.ServiceUnavailable)));

        var result = await service.ProbeAsync(NetworkCapability.MinecraftMetadata);

        Assert.Equal(NetworkAvailabilityState.Degraded, result.State);
        Assert.Equal(NetworkAvailabilityState.Degraded, result.EffectiveState);
    }

    [Fact]
    public async Task ProbeAsync_PreservesEffectiveOfflineStateWhileCheckingRecovery()
    {
        using var handler = new BlockingHandler();
        using var service = new NetworkCapabilityService(new HttpClient(handler));
        service.ReportFailure(NetworkCapability.MinecraftMetadata, new HttpRequestException("offline"));

        var probe = service.ProbeAsync(NetworkCapability.MinecraftMetadata);
        await handler.Started.Task;

        var checking = service.GetSnapshot(NetworkCapability.MinecraftMetadata);
        Assert.Equal(NetworkAvailabilityState.Checking, checking.State);
        Assert.Equal(NetworkAvailabilityState.Unavailable, checking.EffectiveState);

        handler.Release();
        var recovered = await probe;
        Assert.Equal(NetworkAvailabilityState.Available, recovered.EffectiveState);
    }

    private sealed class BlockingHandler : HttpMessageHandler
    {
        public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int RequestCount { get; private set; }

        public void Release() => _release.TrySetResult(true);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            Started.TrySetResult(true);
            await _release.Task.WaitAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private sealed class StatusHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(statusCode));
    }
}
