using System.Net;
using System.Net.NetworkInformation;

namespace Emerald.CoreX.Installation;

public enum NetworkCapability { MinecraftMetadata, MinecraftFiles, LoaderMetadata, Modrinth, Authentication }
public enum NetworkAvailabilityState { Unknown, Checking, Available, Degraded, Unavailable }

public sealed record NetworkCapabilitySnapshot(
    NetworkCapability Capability,
    NetworkAvailabilityState State,
    DateTimeOffset ChangedAt,
    string? Detail = null,
    NetworkAvailabilityState? LastResolvedState = null)
{
    /// <summary>
    /// Returns the last meaningful reachability result while a probe is running.
    /// A transient Checking state must not make callers believe the network failed
    /// (or recovered) until the probe has a terminal result.
    /// </summary>
    public NetworkAvailabilityState EffectiveState
        => State == NetworkAvailabilityState.Checking
            ? LastResolvedState ?? NetworkAvailabilityState.Unknown
            : State;
}

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
    private readonly Dictionary<NetworkCapability, Task<NetworkCapabilitySnapshot>> _probes = new();
    private readonly Dictionary<NetworkCapability, Task> _recoveryPolls = new();
    private readonly HashSet<NetworkCapability> _requestedCapabilities = [];
    private readonly CancellationTokenSource _lifetime = new();
    private readonly object _gate = new();
    private CancellationTokenSource? _networkSignalDebounce;

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
        Task<NetworkCapabilitySnapshot> probe;
        TaskCompletionSource<NetworkCapabilitySnapshot>? starter = null;
        lock (_gate)
        {
            _requestedCapabilities.Add(capability);
            if (_probes.TryGetValue(capability, out probe!) && !probe.IsCompleted)
            {
                // The shared probe is awaited outside the lock so cancellation
                // cannot hold the state mutex while a network request is pending.
            }
            else
            {
                // A caller may cancel its wait without cancelling the shared
                // probe used by recovery polling and other operations.
                starter = new(TaskCreationOptions.RunContinuationsAsynchronously);
                probe = starter.Task;
                _probes[capability] = probe;
            }
        }

        if (starter != null)
        {
            _ = RunProbeAsync(capability, starter);
        }

        try
        {
            return await probe.WaitAsync(cancellationToken);
        }
        finally
        {
            lock (_gate)
            {
                if (_probes.TryGetValue(capability, out var current) && current.IsCompleted)
                {
                    _probes.Remove(capability);
                }
            }
        }
    }

    private async Task RunProbeAsync(NetworkCapability capability, TaskCompletionSource<NetworkCapabilitySnapshot> completion)
    {
        try
        {
            completion.TrySetResult(await ProbeCoreAsync(capability, _lifetime.Token));
        }
        catch (Exception ex)
        {
            completion.TrySetException(ex);
        }
    }

    private async Task<NetworkCapabilitySnapshot> ProbeCoreAsync(NetworkCapability capability, CancellationToken callerCancellationToken)
    {
        Set(capability, NetworkAvailabilityState.Checking);
        // ResponseHeadersRead keeps a reachability probe cheap; downloading the
        // endpoint body is unnecessary and would delay online/offline transitions.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(callerCancellationToken, _lifetime.Token);
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
        catch (OperationCanceledException) when (callerCancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (!_lifetime.IsCancellationRequested)
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
        var startRecovery = false;
        lock (_gate)
        {
            var old = _snapshots[capability];
            var lastResolved = state == NetworkAvailabilityState.Checking
                ? old.LastResolvedState ?? (old.State == NetworkAvailabilityState.Checking ? null : old.State)
                : state;
            if (old.State == state && old.Detail == detail && old.LastResolvedState == lastResolved) return old;
            snapshot = new(capability, state, DateTimeOffset.UtcNow, detail, lastResolved);
            _snapshots[capability] = snapshot;
            if (state == NetworkAvailabilityState.Unavailable
                && (!_recoveryPolls.TryGetValue(capability, out var existing) || existing.IsCompleted))
            {
                startRecovery = true;
            }
        }
        Changed?.Invoke(this, snapshot);
        if (startRecovery) StartRecoveryPoll(capability);
        return snapshot;
    }

    private void StartRecoveryPoll(NetworkCapability capability)
    {
        lock (_gate)
        {
            if (_recoveryPolls.TryGetValue(capability, out var existing) && !existing.IsCompleted)
            {
                return;
            }

            _recoveryPolls[capability] = PollForRecoveryAsync(capability);
        }
    }

    private async Task PollForRecoveryAsync(NetworkCapability capability)
    {
        var started = DateTimeOffset.UtcNow;
        try
        {
            while (!_lifetime.IsCancellationRequested && GetSnapshot(capability).EffectiveState == NetworkAvailabilityState.Unavailable)
            {
                // Poll aggressively for the first minute so a transient disconnect is
                // reflected quickly, then back off to avoid needless background work.
                var delay = DateTimeOffset.UtcNow - started < TimeSpan.FromMinutes(1) ? TimeSpan.FromSeconds(5) : TimeSpan.FromSeconds(30);
                await Task.Delay(delay, _lifetime.Token);
                await ProbeAsync(capability, _lifetime.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // Disposal is the normal way for this loop to stop.
        }
        finally
        {
            lock (_gate)
            {
                _recoveryPolls.Remove(capability);
            }
        }
    }

    private void OnNetworkChanged(object? sender, NetworkAvailabilityEventArgs e) => ScheduleRequestedProbes();
    private void OnAddressChanged(object? sender, EventArgs e) => ScheduleRequestedProbes();

    private void ScheduleRequestedProbes()
    {
        CancellationTokenSource debounce;
        lock (_gate)
        {
            _networkSignalDebounce?.Cancel();
            debounce = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            _networkSignalDebounce = debounce;
        }

        _ = ProbeRequestedCapabilitiesAsync(debounce);
    }

    private async Task ProbeRequestedCapabilitiesAsync(CancellationTokenSource debounce)
    {
        try
        {
            // Address changes can arrive in bursts while a VPN or Wi-Fi adapter
            // settles. Wait for the burst to finish before probing services.
            await Task.Delay(TimeSpan.FromMilliseconds(250), debounce.Token);
            NetworkCapability[] capabilities;
            lock (_gate)
            {
                capabilities = _requestedCapabilities.ToArray();
            }

            foreach (var capability in capabilities)
            {
                _ = ProbeAsync(capability, _lifetime.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // A newer network signal superseded this debounce window.
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_networkSignalDebounce, debounce))
                {
                    _networkSignalDebounce = null;
                }
            }
            debounce.Dispose();
        }
    }

    public void Dispose()
    {
        NetworkChange.NetworkAvailabilityChanged -= OnNetworkChanged;
        NetworkChange.NetworkAddressChanged -= OnAddressChanged;
        lock (_gate)
        {
            _networkSignalDebounce?.Cancel();
            _networkSignalDebounce = null;
        }
        _lifetime.Cancel();
        _lifetime.Dispose();
    }
}
