using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Emerald.CoreX.Store.Modrinth.JSON;
using Microsoft.Extensions.Logging;
using CmlLib.Core;
namespace Emerald.CoreX.Store.Modrinth;

public class ModStore : ModrinthStore
{
    public ModStore(MinecraftPath path, ILogger logger) : base(path, logger, "mod")
    {
    }

    public ModStore(ILogger logger) : this(new MinecraftPath(MinecraftPath.GetOSDefaultPath()), logger)
    {
    }

    public override async Task<StoreItem?> GetItemAsync(string id)
    {
        // Implement mod-specific logic if needed
        return await base.GetItemAsync(id);
    }

    public override async Task<List<ItemVersion>?> GetVersionsAsync(string id)
    {
        // Implement mod-specific logic if needed
        return await base.GetVersionsAsync(id);
    }

    public override async Task DownloadItemAsync(ItemFile file, string projectType)
    {
        // Implement mod-specific download logic
        await base.DownloadItemAsync(file, "mods");
    }
}

public class PluginStore : ModrinthStore
{
    public PluginStore(MinecraftPath path, ILogger logger) : base(path, logger, "plugin")
    {
    }

    public PluginStore(ILogger logger) : this(new MinecraftPath(MinecraftPath.GetOSDefaultPath()), logger)
    {
    }

    public override async Task<StoreItem?> GetItemAsync(string id)
    {
        return await base.GetItemAsync(id);
    }

    public override async Task<List<ItemVersion>?> GetVersionsAsync(string id)
    {
        return await base.GetVersionsAsync(id);
    }

    public override async Task DownloadItemAsync(ItemFile file, string projectType)
    {
        await base.DownloadItemAsync(file, "mods");
    }
}

public class ResourcePackStore : ModrinthStore
{
    public ResourcePackStore(MinecraftPath path, ILogger logger) : base(path, logger, "resourcepack")
    {
    }

    public ResourcePackStore(ILogger logger) : this(new MinecraftPath(MinecraftPath.GetOSDefaultPath()), logger)
    {
    }

    public override async Task<StoreItem?> GetItemAsync(string id)
    {
        return await base.GetItemAsync(id);
    }

    public override async Task<List<ItemVersion>?> GetVersionsAsync(string id)
    {
        return await base.GetVersionsAsync(id);
    }

    public override async Task DownloadItemAsync(ItemFile file, string projectType)
    {
        await base.DownloadItemAsync(file, "resourcepacks");
    }
}

public class ShaderStore : ModrinthStore
{
    public ShaderStore(MinecraftPath path, ILogger logger) : base(path, logger, "shader")
    {
    }

    public ShaderStore(ILogger logger) : this(new MinecraftPath(MinecraftPath.GetOSDefaultPath()), logger)
    {
    }

    public override async Task<StoreItem?> GetItemAsync(string id)
    {
        // Implement shader-specific logic if needed
        return await base.GetItemAsync(id);
    }

    public override async Task<List<ItemVersion>?> GetVersionsAsync(string id)
    {
        // Implement shader-specific logic if needed
        return await base.GetVersionsAsync(id);
    }

    public override async Task DownloadItemAsync(ItemFile file, string projectType)
    {
        // Implement shader-specific download logic
        await base.DownloadItemAsync(file, "shaders");
    }
}

public class ModpackStore : ModrinthStore
{
    public ModpackStore(MinecraftPath path, ILogger logger) : base(path, logger, "modpack")
    {
    }

    public ModpackStore(ILogger logger) : this(new MinecraftPath(MinecraftPath.GetOSDefaultPath()), logger)
    {
    }

    public override async Task<StoreItem?> GetItemAsync(string id)
    {
        // Implement modpack-specific logic if needed
        return await base.GetItemAsync(id);
    }

    public override async Task<List<ItemVersion>?> GetVersionsAsync(string id)
    {
        // Implement modpack-specific logic if needed
        return await base.GetVersionsAsync(id);
    }

    public override async Task DownloadItemAsync(ItemFile file, string projectType)
    {
        // Implement modpack-specific download logic
        await base.DownloadItemAsync(file, "modpacks");
    }
}
