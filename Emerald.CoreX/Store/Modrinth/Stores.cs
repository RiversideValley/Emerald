using CmlLib.Core;
using Emerald.CoreX.Store;
using Microsoft.Extensions.Logging;

namespace Emerald.CoreX.Store.Modrinth;

public sealed class ModStore : ModrinthStore
{
    public ModStore(MinecraftPath path, ILogger<ModStore> logger)
        : base(path, logger, "mod", "mods", StoreContentType.Mod)
    {
    }

    public ModStore(ILogger<ModStore> logger)
        : this(new MinecraftPath(MinecraftPath.GetOSDefaultPath()), logger)
    {
    }
}

public sealed class PluginStore : ModrinthStore
{
    public PluginStore(MinecraftPath path, ILogger<PluginStore> logger)
        : base(path, logger, "plugin", "plugins", StoreContentType.Plugin)
    {
    }

    public PluginStore(ILogger<PluginStore> logger)
        : this(new MinecraftPath(MinecraftPath.GetOSDefaultPath()), logger)
    {
    }
}

public sealed class ResourcePackStore : ModrinthStore
{
    public ResourcePackStore(MinecraftPath path, ILogger<ResourcePackStore> logger)
        : base(path, logger, "resourcepack", "resourcepacks", StoreContentType.ResourcePack)
    {
    }

    public ResourcePackStore(ILogger<ResourcePackStore> logger)
        : this(new MinecraftPath(MinecraftPath.GetOSDefaultPath()), logger)
    {
    }
}

public sealed class ShaderStore : ModrinthStore
{
    public ShaderStore(MinecraftPath path, ILogger<ShaderStore> logger)
        : base(path, logger, "shader", "shaderpacks", StoreContentType.Shader)
    {
    }

    public ShaderStore(ILogger<ShaderStore> logger)
        : this(new MinecraftPath(MinecraftPath.GetOSDefaultPath()), logger)
    {
    }
}

public sealed class DataPackStore : ModrinthStore
{
    public DataPackStore(MinecraftPath path, ILogger<DataPackStore> logger)
        : base(path, logger, "datapack", "datapacks", StoreContentType.DataPack)
    {
    }

    public DataPackStore(ILogger<DataPackStore> logger)
        : this(new MinecraftPath(MinecraftPath.GetOSDefaultPath()), logger)
    {
    }
}
