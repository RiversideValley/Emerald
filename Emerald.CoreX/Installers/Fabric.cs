using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CmlLib.Core;
using CmlLib.Core.ModLoaders.FabricMC;
using CmlLib.Core.ModLoaders.QuiltMC;
using Microsoft.Extensions.Logging;

namespace Emerald.CoreX.Installers;

public class Fabric : IModLoaderInstaller
{
    private readonly Notifications.INotificationService _notify;
    public Fabric(Notifications.INotificationService notificationService)
    {
        _notify = notificationService;
    }

    public Versions.Type Type => Versions.Type.Fabric;

    public async Task<List<LoaderInfo>> GetVersionsAsync(string mcVersion)
    {
        var not = _notify.Create(
            "GettingFabricLoaders",
            mcVersion,
            isIndeterminate: true
        );
        this.Log().LogInformation("Getting Fabric Loaders for {mcversion}", mcVersion);

        try
        {
            var fabricInstaller = new FabricInstaller(new HttpClient());
            var versions = await fabricInstaller.GetLoaders(mcVersion);

            if (versions == null || !versions.Any())
                throw new NullReferenceException();

            var l = versions.Select(x=> new LoaderInfo { Version = x.Version, Stable = x.Stable });

            this.Log().LogInformation("Found {count} Fabric Loaders", versions.Count);
            _notify.Complete(not.Id, true);

            return l.ToList();
        }
        catch (Exception ex)
        {
            this.Log().LogWarning("Failed to get Fabric Loaders: {ex}", ex.Message);
            _notify.Complete(not.Id, false, ex.Message,ex);
            return new();
        }
    }

    public async Task<string> InstallAsync(MinecraftPath path, string mcversion, string? modversion = null)
    {
        var not = _notify.Create(
            "InstallFabric",
            mcversion,
            isIndeterminate: true
        );
        this.Log().LogInformation("Installing Fabric Loader for {mcversion}", mcversion);
        try
        {

            var fabricInstaller = new FabricInstaller(new HttpClient());

            string? versionName = null;

            if (modversion == null)
                versionName = await fabricInstaller.Install(mcversion, path);
            else
                versionName = await fabricInstaller.Install(mcversion, modversion, path);

            this.Log().LogInformation("Installed Fabric Loader {versionName}", versionName);
            _notify.Complete(not.Id, true);

            return versionName;
        }
        catch (Exception ex)
        {
            this.Log().LogError(ex,"Failed to install Fabric for {0}", mcversion);
            _notify.Complete(not.Id, false, ex.Message, ex);
            return null;
        }
    }
}
