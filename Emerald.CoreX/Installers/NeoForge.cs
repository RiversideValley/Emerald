using CmlLib.Core;
using CmlLib.Core.Installer.NeoForge;
using CmlLib.Core.Installer.NeoForge.Versions;
using Microsoft.Extensions.Logging;

namespace Emerald.CoreX.Installers;

public class NeoForge : IModLoaderInstaller
{
    private readonly Notifications.INotificationService _notify;

    public NeoForge(Notifications.INotificationService notificationService)
    {
        _notify = notificationService;
    }

    public Versions.Type Type => Versions.Type.NeoForge;

    public async Task<List<LoaderInfo>> GetVersionsAsync(string mcVersion)
    {
        var not = _notify.Create(
            "GettingNeoForgeLoaders",
            mcVersion,
            isIndeterminate: true
        );
        this.Log().LogInformation("Getting NeoForge Loaders for {mcversion}", mcVersion);

        try
        {
            var versionLoader = new NeoForgeVersionLoader(new HttpClient());
            var versions = await versionLoader.GetNeoForgeVersions(mcVersion);

            if (versions == null || !versions.Any())
            {
                throw new NullReferenceException();
            }

            var loaders = versions.Select(version => new LoaderInfo
            {
                Version = version.VersionName,
                Stable = true
            });

            this.Log().LogInformation("Found {count} NeoForge Loaders", versions.Count());
            _notify.Complete(not.Id, true);

            return loaders.ToList();
        }
        catch (Exception ex)
        {
            this.Log().LogWarning("Failed to get NeoForge Loaders: {ex}", ex.Message);
            _notify.Complete(not.Id, false, ex.Message, ex);
            return new();
        }
    }

    public async Task<string> InstallAsync(MinecraftPath path, string mcversion, string? modversion = null, bool online = true)
    {
        var not = _notify.Create(
            "InstallNeoForge",
            mcversion,
            isIndeterminate: true
        );
        this.Log().LogInformation("Installing NeoForge Loader for {mcversion}", mcversion);

        try
        {
            var neoForge = new NeoForgeInstaller(new(path));

            string? versionName;
            if (modversion == null)
            {
                versionName = await neoForge.Install(mcversion);
            }
            else
            {
                versionName = await neoForge.Install(mcversion, modversion);
            }

            this.Log().LogInformation("Installed NeoForge Loader {versionName}", versionName);
            _notify.Complete(not.Id, true);

            return versionName;
        }
        catch (Exception ex)
        {
            this.Log().LogError(ex, "Failed to install NeoForge for {0}", mcversion);
            _notify.Complete(not.Id, false, ex.Message, ex);
            return null;
        }
    }
}
