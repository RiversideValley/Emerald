using Emerald.CoreX.Store.Modrinth.JSON;

namespace Emerald.CoreX.Store;

public interface IGameStoreContentService
{
    Task<StoreCompatibilityResult> GetCompatibleVersionsAsync(
        Game game,
        StoreContentType contentType,
        string projectId,
        CancellationToken cancellationToken = default);

    Task<InstalledStoreItem> InstallAsync(
        Game game,
        StoreContentType contentType,
        StoreItem project,
        ItemVersion version,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InstalledStoreItem>> GetInstalledItemsAsync(
        Game game,
        StoreContentType contentType,
        CancellationToken cancellationToken = default);

    Task<bool> RemoveAsync(
        Game game,
        StoreContentType contentType,
        InstalledStoreItem item,
        bool forceUntracked = false,
        CancellationToken cancellationToken = default);
}
