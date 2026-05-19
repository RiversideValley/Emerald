using CmlLib.Core;

namespace Emerald.CoreX;

public sealed class SplitMinecraftPath : MinecraftPath
{
    public SplitMinecraftPath(
        string globalBasePath,
        string instanceBasePath,
        bool shareAssets,
        bool shareLibraries,
        bool shareRuntime,
        bool shareVersions)
        : base(instanceBasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(globalBasePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceBasePath);

        if (shareAssets)
        {
            Assets = Path.Combine(globalBasePath, "assets");
            Resource = Path.Combine(globalBasePath, "resources");
        }

        if (shareLibraries)
        {
            Library = Path.Combine(globalBasePath, "libraries");
        }

        if (shareRuntime)
        {
            Runtime = Path.Combine(globalBasePath, "runtime");
        }

        if (shareVersions)
        {
            Versions = Path.Combine(globalBasePath, "versions");
        }
    }
}
