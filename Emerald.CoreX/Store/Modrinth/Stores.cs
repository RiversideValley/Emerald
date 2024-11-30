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
    /// <summary>
    /// Initializes a new instance of the ModStore class with a custom Minecraft path.
    /// </summary>
    /// <param name="path">The custom Minecraft path.</param>
    /// <param name="logger">The logger instance.</param>
    public ModStore(MinecraftPath path, ILogger logger) : base(path, logger, "mod")
    {
    }

    /// <summary>
    /// Initializes a new instance of the ModStore class with the default Minecraft path.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
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

    public override async Task DownloadItemAsync(ItemFile file, string projectType, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        // Implement mod-specific download logic
        await base.DownloadItemAsync(file, "mods",progress, cancellationToken);
    }
}

public class PluginStore : ModrinthStore
{
    /// <summary>
    /// Initializes a new instance of the PluginStore class with a custom Minecraft path.
    /// </summary>
    /// <param name="path">The custom Minecraft path.</param>
    /// <param name="logger">The logger instance.</param>
    public PluginStore(MinecraftPath path, ILogger logger) : base(path, logger, "plugin")
    {
    }

    /// <summary>
    /// Initializes a new instance of the PluginStore class with the default Minecraft path.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
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

    public override async Task DownloadItemAsync(ItemFile file, string projectType,
        IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        await base.DownloadItemAsync(file, "mods", progress, cancellationToken);
    }
}

public class ResourcePackStore : ModrinthStore
{
    /// <summary>
    /// Initializes a new instance of the ResourcePackStore class with a custom Minecraft path.
    /// </summary>
    /// <param name="path">The custom Minecraft path.</param>
    /// <param name="logger">The logger instance.</param>
    public ResourcePackStore(MinecraftPath path, ILogger logger) : base(path, logger, "resourcepack")
    {
    }

    /// <summary>
    /// Initializes a new instance of the ResourcePackStore class with the default Minecraft path.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
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

    public override async Task DownloadItemAsync(ItemFile file, string projectType,
        IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        await base.DownloadItemAsync(file, "resourcepacks", progress, cancellationToken);
    }
}

public class ShaderStore : ModrinthStore
{
    /// <summary>
    /// Initializes a new instance of the ShaderStore class with a custom Minecraft path.
    /// </summary>
    /// <param name="path">The custom Minecraft path.</param>
    /// <param name="logger">The logger instance.</param>
    public ShaderStore(MinecraftPath path, ILogger logger) : base(path, logger, "shader")
    {
    }

    /// <summary>
    /// Initializes a new instance of the ShaderStore class with the default Minecraft path.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
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

    public override async Task DownloadItemAsync(ItemFile file, string projectType,
        IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        // Implement shader-specific download logic
        await base.DownloadItemAsync(file, "shaderpacks", progress, cancellationToken);
    }
}

public class ModpackStore : ModrinthStore
{
    /// <summary>
    /// Initializes a new instance of the ModpackStore class with a custom Minecraft path.
    /// </summary>
    /// <param name="path">The custom Minecraft path.</param>
    /// <param name="logger">The logger instance.</param>
    public ModpackStore(MinecraftPath path, ILogger logger) : base(path, logger, "modpack")
    {
    }

    /// <summary>
    /// Initializes a new instance of the ModpackStore class with the default Minecraft path.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
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

    public override async Task DownloadItemAsync(ItemFile file, string projectType,
        IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        // Implement modpack-specific download logic
        await base.DownloadItemAsync(file, "modpacks", progress, cancellationToken);
    }
}
