using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CmlLib.Core;
using CmlLib.Core.Installer.Forge.Versions;
using CmlLib.Core.Installer.Forge;
using Microsoft.Extensions.Logging;
using Windows.System;

namespace Emerald.CoreX.Installers;

public class Forge : IModLoaderInstaller
{
    private readonly Notifications.INotificationService _notify;
    public Forge(Notifications.INotificationService notificationService)
    {
        _notify = notificationService;
    }

    public Versions.Type Type => Versions.Type.Forge;

    public async Task<List<LoaderInfo>> GetVersionsAsync(string mcVersion)
    {
        var not = _notify.Create(
            "GettingForgeLoaders",
            mcVersion,
            isIndeterminate: true
        );
        this.Log().LogInformation("Getting Forge Loaders for {mcversion}", mcVersion);

        try
        {
            var versionLoader = new ForgeVersionLoader(new HttpClient());
            var versions = await versionLoader.GetForgeVersions(mcVersion);

            if (versions == null || !versions.Any())
                throw new NullReferenceException();

            var l = versions.Select(x => new LoaderInfo { Version = x.ForgeVersionName, Stable = x.IsRecommendedVersion });

            this.Log().LogInformation("Found {count} Forge Loaders", versions.Count());
            _notify.Complete(not.Id, true);

            return l.ToList();
        }
        catch (Exception ex)
        {
            this.Log().LogWarning("Failed to get Forge Loaders: {ex}", ex.Message);
            _notify.Complete(not.Id, false, ex.Message, ex);
            return new();
        }
    }

    public async Task<string> InstallAsync(MinecraftPath path, string mcversion, string? modversion = null, bool online = true)
    {
        var not = _notify.Create(
            "InstallForge",
            mcversion,
            isIndeterminate: true
        );
        this.Log().LogInformation("Installing Forge Loader for {mcversion}", mcversion);
        try
        {


            var forge = new ForgeInstaller(new(path));

            string? versionName = null;

            if (modversion == null)
                versionName = await forge.Install(mcversion);
            else
                versionName = await forge.Install(mcversion, modversion);

            this.Log().LogInformation("Installed Forge Loader {versionName}", versionName);
            _notify.Complete(not.Id, true);

            return versionName;
        }
        catch (Exception ex)
        {
            this.Log().LogError(ex, "Failed to install Forge for {0}", mcversion);
            _notify.Complete(not.Id, false, ex.Message, ex);
            return null;
        }
    }
}
