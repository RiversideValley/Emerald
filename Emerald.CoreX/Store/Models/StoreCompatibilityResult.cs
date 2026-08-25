using Emerald.CoreX.Store.Modrinth.JSON;

namespace Emerald.CoreX.Store;

public sealed class StoreCompatibilityResult
{
    public IReadOnlyList<ItemVersion> Versions { get; init; } = [];
    public bool UsedFallback { get; init; }
    public string? Notice { get; init; }
}
