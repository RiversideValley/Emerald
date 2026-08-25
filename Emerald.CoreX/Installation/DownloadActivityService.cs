namespace Emerald.CoreX.Installation;

/// <summary>
/// Coordinates downloads with the version catalog. Downloads may run together,
/// but a catalog/path refresh is exclusive so it cannot replace live game state.
/// </summary>
public sealed record DownloadActivitySnapshot(int ActiveDownloads, bool IsCatalogRefreshing)
{
    public bool IsBusy => ActiveDownloads > 0 || IsCatalogRefreshing;
}

public interface IDownloadActivityService
{
    event EventHandler<DownloadActivitySnapshot>? Changed;
    DownloadActivitySnapshot Snapshot { get; }
    ValueTask<IDisposable> AcquireDownloadAsync(CancellationToken cancellationToken = default);
    bool TryAcquireCatalogRefresh(out IDisposable? lease);
}

public sealed class DownloadActivityService : IDownloadActivityService
{
    private readonly object _gate = new();
    private int _activeDownloads;
    private bool _catalogRefreshing;
    private TaskCompletionSource? _catalogReleased;

    public event EventHandler<DownloadActivitySnapshot>? Changed;

    public DownloadActivitySnapshot Snapshot
    {
        get { lock (_gate) return new(_activeDownloads, _catalogRefreshing); }
    }

    public async ValueTask<IDisposable> AcquireDownloadAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            Task? waitForCatalog = null;
            lock (_gate)
            {
                if (!_catalogRefreshing)
                {
                    _activeDownloads++;
                    PublishLocked();
                    return new Lease(this, isCatalogRefresh: false);
                }

                waitForCatalog = _catalogReleased?.Task;
            }

            if (waitForCatalog != null)
                await waitForCatalog.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public bool TryAcquireCatalogRefresh(out IDisposable? lease)
    {
        lock (_gate)
        {
            if (_catalogRefreshing || _activeDownloads > 0)
            {
                lease = null;
                return false;
            }

            _catalogRefreshing = true;
            _catalogReleased = new(TaskCreationOptions.RunContinuationsAsynchronously);
            PublishLocked();
            lease = new Lease(this, isCatalogRefresh: true);
            return true;
        }
    }

    private void Release(bool isCatalogRefresh)
    {
        lock (_gate)
        {
            if (isCatalogRefresh)
            {
                if (!_catalogRefreshing) return;
                _catalogRefreshing = false;
                _catalogReleased?.TrySetResult();
                _catalogReleased = null;
            }
            else
            {
                if (_activeDownloads == 0) return;
                _activeDownloads--;
            }

            PublishLocked();
        }
    }

    private void PublishLocked() => Changed?.Invoke(this, new(_activeDownloads, _catalogRefreshing));

    private sealed class Lease(DownloadActivityService owner, bool isCatalogRefresh) : IDisposable
    {
        private int _disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                owner.Release(isCatalogRefresh);
        }
    }
}
