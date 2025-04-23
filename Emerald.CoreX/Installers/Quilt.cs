using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CmlLib.Core;
using CmlLib.Core.ModLoaders.QuiltMC;
using Microsoft.Extensions.Logging;

namespace Emerald.CoreX.Installers;

public class Quilt : IModLoaderInstaller
{
    private readonly Notifications.INotificationService _notify;
    public Quilt(Notifications.INotificationService notificationService)
    {
        _notify = notificationService;
    }

    public Versions.Type Type => Versions.Type.Quilt;

    public async Task<List<LoaderInfo>> GetVersionsAsync(string mcVersion)
    {
        var not = _notify.Create(
            "GettingQuiltLoaders",
            mcVersion,
            isIndeterminate: true
        );
        this.Log().LogInformation("Getting Quilt Loaders for {mcversion}", mcVersion);

        try
        {
            var QuiltInstaller = new QuiltInstaller(new HttpClient());
            var versions = await QuiltInstaller.GetLoaders(mcVersion);

            if (versions == null || !versions.Any())
                throw new NullReferenceException();

            var l = versions.Select(x => new LoaderInfo { Version = x.Version, Stable = x.Stable });

            this.Log().LogInformation("Found {count} Quilt Loaders", versions.Count);
            _notify.Complete(not.Id, true);

            return l.ToList();
        }
        catch (Exception ex)
        {
            this.Log().LogWarning("Failed to get Quilt Loaders: {ex}", ex.Message);
            _notify.Complete(not.Id, false, ex.Message, ex);
            return new();
        }
    }

    public async Task<string> InstallAsync(MinecraftPath path, string mcversion, string? modversion = null)
    {
        var not = _notify.Create(
            "InstallQuilt",
            mcversion,
            isIndeterminate: true
        );
        this.Log().LogInformation("Installing Quilt Loader for {mcversion}", mcversion);
        try
        {

            var QuiltInstaller = new QuiltInstaller(new HttpClient());

            string? versionName = null;

            if (modversion == null)
                versionName = await QuiltInstaller.Install(mcversion, path);
            else
                versionName = await QuiltInstaller.Install(mcversion, modversion, path);

            this.Log().LogInformation("Installed Quilt Loader {versionName}", versionName);
            _notify.Complete(not.Id, true);

            return versionName;
        }
        catch (Exception ex)
        {
            this.Log().LogError(ex, "Failed to install Quilt for {0}", mcversion);
            _notify.Complete(not.Id, false, ex.Message, ex);
            return null;
        }
    }
}
