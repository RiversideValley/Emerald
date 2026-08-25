using Emerald.CoreX.Installation;
using Xunit;

namespace Emerald.CoreX.Tests.Installation;

public sealed class DownloadActivityServiceTests
{
    [Fact]
    public async Task CatalogRefresh_WaitsUntilActiveDownloadsFinish()
    {
        var service = new DownloadActivityService();
        using var download = await service.AcquireDownloadAsync();

        Assert.False(service.TryAcquireCatalogRefresh(out var refresh));
        Assert.Null(refresh);

        download.Dispose();
        Assert.True(service.TryAcquireCatalogRefresh(out refresh));
        using (refresh!)
        {
            Assert.True(service.Snapshot.IsCatalogRefreshing);
        }

        Assert.False(service.Snapshot.IsBusy);
    }

    [Fact]
    public async Task Download_WaitsForCatalogRefreshLease()
    {
        var service = new DownloadActivityService();
        Assert.True(service.TryAcquireCatalogRefresh(out var refresh));

        var pending = service.AcquireDownloadAsync().AsTask();
        Assert.False(pending.IsCompleted);
        refresh!.Dispose();

        using var download = await pending.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.Equal(1, service.Snapshot.ActiveDownloads);
    }
}
