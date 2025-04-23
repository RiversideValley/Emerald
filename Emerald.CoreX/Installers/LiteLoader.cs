using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CmlLib.Core;

namespace Emerald.CoreX.Installers;

public class LiteLoader : IModLoaderInstaller
{
    public Versions.Type Type => Versions.Type.LiteLoader;
    public Task<List<LoaderInfo>> GetVersionsAsync(string mcVersion)
    {
        throw new NotImplementedException();
    }

    public Task<string> InstallAsync(MinecraftPath path, string mcversion, string? modversion = null)
    {
        throw new NotImplementedException();
    }
}
