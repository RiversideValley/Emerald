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
    public ModLoaderRouter()
    {
       Installers = Ioc.Default.GetServices<IModLoaderInstaller>();
    }

    public async Task<string?> RouteAndInitializeAsync(MinecraftPath path, Versions.Version version)
    {

        if (version.Type == Versions.Type.Vanilla)
            return version.BasedOn;

       return await Installers.First(x=> x.Type == version.Type).InstallAsync(path, version.BasedOn, version.ModVersion);
    }
}
