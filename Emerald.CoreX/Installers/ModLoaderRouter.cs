using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CmlLib.Core;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Emerald.CoreX.Installers;
public class ModLoaderRouter
{
    public readonly IEnumerable<IModLoaderInstaller> Installers;

    public ModLoaderRouter(IEnumerable<IModLoaderInstaller>? installers = null)
    {
       Installers = installers ?? Ioc.Default.GetServices<IModLoaderInstaller>();
    }

    public async Task<string?> RouteAndInitializeAsync(
        MinecraftPath path,
        Versions.Version version,
        bool online = true,
        string? installedVersion = null)
    {
        if (!online && !string.IsNullOrWhiteSpace(installedVersion))
        {
            return installedVersion;
        }

        if (version.Type == Versions.Type.Vanilla)
            return version.BasedOn;

       return await Installers.First(x=> x.Type == version.Type).InstallAsync(path, version.BasedOn, version.ModVersion, online);
    }
}
