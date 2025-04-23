using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CmlLib.Core;
using CmlLib.Core.ModLoaders.LiteLoader;
using CmlLib.Core.ModLoaders.QuiltMC;
using Microsoft.Extensions.Logging;

namespace Emerald.CoreX.Installers;

public class LiteLoader : IModLoaderInstaller
{
    private readonly Notifications.INotificationService _notify;
    public LiteLoader(Notifications.INotificationService notificationService)
    {
        _notify = notificationService;
    }

    public Versions.Type Type => Versions.Type.LiteLoader;

    public async Task<List<LoaderInfo>> GetVersionsAsync(string mcVersion)
    {
        var not = _notify.Create(
            "GettingLiteLoaders",
            mcVersion,
            isIndeterminate: true
        );
        this.Log().LogInformation("Getting Liteloader Loaders for {mcversion}", mcVersion);

        try
        {
            var LiteLoaderInstaller = new LiteLoaderInstaller(new HttpClient());
            var versions = await LiteLoaderInstaller.GetAllLiteLoaders();

            if (versions == null || !versions.Any())
                throw new NullReferenceException();


            var filtered = versions.Where(x => x.BaseVersion == mcVersion);

            if (filtered == null || !filtered.Any())
                throw new NullReferenceException($"Can't find liteloder for specific minecraft version {mcVersion}");

            var l = filtered.Select(x => new LoaderInfo { Version = x.Version});

            this.Log().LogInformation("Found {count} LiteLoader Loaders", filtered.Count());
            _notify.Complete(not.Id, true);

            return l.ToList();
        }
        catch (Exception ex)
        {
            this.Log().LogWarning("Failed to get LiteLoader Loaders: {ex}", ex.Message);
            _notify.Complete(not.Id, false, ex.Message, ex);
            return new();
        }
    }

    public async Task<string> InstallAsync(MinecraftPath path, string mcversion, string? modversion = null)
    {
        var not = _notify.Create(
            "InstallLiteLoader",
            mcversion,
            isIndeterminate: true
        );
        this.Log().LogInformation("Installing LiteLoader Loader for {mcversion}", mcversion);
        try
        {

            var LiteLoaderInstaller = new LiteLoaderInstaller(new HttpClient());

            var launcher = new MinecraftLauncher(path);

            string? versionName = null;

            var loaders = await LiteLoaderInstaller.GetAllLiteLoaders();
            var loaderToInstall = loaders.First(loader => loader.BaseVersion == mcversion);

            if (modversion == null)
                versionName = await LiteLoaderInstaller.Install(
                    loaderToInstall,
                    await launcher.GetVersionAsync(mcversion),
                    path);
            else
                versionName = await LiteLoaderInstaller.Install(
                    loaders.First(x=> x.Version == modversion),
                    await launcher.GetVersionAsync(mcversion),
                    path);


            this.Log().LogInformation("Installed LiteLoader Loader {versionName}", versionName);
            _notify.Complete(not.Id, true);

            return versionName;
        }
        catch (Exception ex)
        {
            this.Log().LogError(ex, "Failed to install LiteLoader for {0}", mcversion);
            _notify.Complete(not.Id, false, ex.Message, ex);
            return null;
        }
    }
}
