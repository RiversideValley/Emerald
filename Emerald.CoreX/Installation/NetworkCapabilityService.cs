using System.Net;
using System.Net.NetworkInformation;

namespace Emerald.CoreX.Installation;

public enum NetworkCapability { MinecraftMetadata, MinecraftFiles, LoaderMetadata, Modrinth, Authentication }
public enum NetworkAvailabilityState { Unknown, Checking, Available, Degraded, Unavailable }

public sealed record NetworkCapabilitySnapshot(
    NetworkCapability Capability,
    NetworkAvailabilityState State,
    DateTimeOffset ChangedAt,
    string? Detail = null);

public interface INetworkCapabilityService : IDisposable
{
    event EventHandler<NetworkCapabilitySnapshot>? Changed;
    NetworkCapabilitySnapshot GetSnapshot(NetworkCapability capability);
    Task<NetworkCapabilitySnapshot> ProbeAsync(NetworkCapability capability, CancellationToken cancellationToken = default);
    void ReportSuccess(NetworkCapability capability);
    void ReportFailure(NetworkCapability capability, Exception exception);
}

public sealed class NetworkCapabilityService : INetworkCapabilityService
{
    // Probe the service needed by an operation, not a generic connectivity host.
    private static readonly IReadOnlyDictionary<NetworkCapability, Uri> Endpoints = new Dictionary<NetworkCapability, Uri>
    {
        [NetworkCapability.MinecraftMetadata] = new("https://piston-meta.mojang.com/mc/game/version_manifest_v2.json"),
        [NetworkCapability.MinecraftFiles] = new("https://resources.download.minecraft.net/"),
        [NetworkCapability.LoaderMetadata] = new("https://meta.fabricmc.net/v2/versions/loader"),
        [NetworkCapability.Modrinth] = new("https://api.modrinth.com/v2/tag/project_type"),
        [NetworkCapability.Authentication] = new("https://login.live.com/")
    };

    private readonly HttpClient _httpClient;
    private readonly Dictionary<NetworkCapability, NetworkCapabilitySnapshot> _snapshots;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly object _gate = new();

    public event EventHandler<NetworkCapabilitySnapshot>? Changed;

    public NetworkCapabilityService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _snapshots = Enum.GetValues<NetworkCapability>().ToDictionary(
            x => x,
            x => new NetworkCapabilitySnapshot(x, NetworkAvailabilityState.Unknown, DateTimeOffset.UtcNow));
        NetworkChange.NetworkAvailabilityChanged += OnNetworkChanged;
        NetworkChange.NetworkAddressChanged += OnAddressChanged;
    }

    public NetworkCapabilitySnapshot GetSnapshot(NetworkCapability capability)
    {
        lock (_gate) return _snapshots[capability];
    }

    public async Task<NetworkCapabilitySnapshot> ProbeAsync(NetworkCapability capability, CancellationToken cancellationToken = default)
    {
        Set(capability, NetworkAvailabilityState.Checking);
        // ResponseHeadersRead keeps a reachability probe cheap; downloading the
        // endpoint body is unnecessary and would delay online/offline transitions.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        deadline.CancelAfter(TimeSpan.FromSeconds(2));
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, Endpoints[capability]);
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            var state = (int)response.StatusCode >= 500
                ? NetworkAvailabilityState.Degraded
                : NetworkAvailabilityState.Available;
            return Set(capability, state, $"HTTP {(int)response.StatusCode}");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && !_lifetime.IsCancellationRequested)
        {
            return Set(capability, NetworkAvailabilityState.Unavailable, "Probe timed out");
        }
        catch (HttpRequestException ex)
        {
            return Set(capability, NetworkAvailabilityState.Unavailable, ex.Message);
        }
    }

    public void ReportSuccess(NetworkCapability capability) => Set(capability, NetworkAvailabilityState.Available);

    public void ReportFailure(NetworkCapability capability, Exception exception)
        => Set(capability, exception is HttpRequestException { StatusCode: >= HttpStatusCode.InternalServerError }
            ? NetworkAvailabilityState.Degraded
            : NetworkAvailabilityState.Unavailable, exception.Message);

    private NetworkCapabilitySnapshot Set(NetworkCapability capability, NetworkAvailabilityState state, string? detail = null)
    {
        NetworkCapabilitySnapshot snapshot;
        lock (_gate)
        {
            var old = _snapshots[capability];
            if (old.State == state && old.Detail == detail) return old;
            snapshot = new(capability, state, DateTimeOffset.UtcNow, detail);
            _snapshots[capability] = snapshot;
        }
        Changed?.Invoke(this, snapshot);
        if (state == NetworkAvailabilityState.Unavailable) _ = PollForRecoveryAsync(capability);
        return snapshot;
    }

    private async Task PollForRecoveryAsync(NetworkCapability capability)
    {
        var started = DateTimeOffset.UtcNow;
        while (!_lifetime.IsCancellationRequested && GetSnapshot(capability).State == NetworkAvailabilityState.Unavailable)
        {
            // Poll aggressively for the first minute so a transient disconnect is
            // reflected quickly, then back off to avoid needless background work.
            var delay = DateTimeOffset.UtcNow - started < TimeSpan.FromMinutes(1) ? TimeSpan.FromSeconds(5) : TimeSpan.FromSeconds(30);
            try { await Task.Delay(delay, _lifetime.Token); await ProbeAsync(capability, _lifetime.Token); }
            catch (OperationCanceledException) { return; }
        }
    }

    private void OnNetworkChanged(object? sender, NetworkAvailabilityEventArgs e) => ProbeAll();
    private void OnAddressChanged(object? sender, EventArgs e) => ProbeAll();
    private void ProbeAll() { foreach (var capability in Enum.GetValues<NetworkCapability>()) _ = ProbeAsync(capability, _lifetime.Token); }

    public void Dispose()
    {
        NetworkChange.NetworkAvailabilityChanged -= OnNetworkChanged;
        NetworkChange.NetworkAddressChanged -= OnAddressChanged;
        _lifetime.Cancel();
        _lifetime.Dispose();
    }
}
