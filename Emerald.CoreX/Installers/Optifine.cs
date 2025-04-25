using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CmlLib.Core;

namespace Emerald.CoreX.Installers;

public class Optifine : IModLoaderInstaller
{
    public Versions.Type Type => Versions.Type.OptiFine;
    public Task<List<LoaderInfo>> GetVersionsAsync(string mcVersion)
    {
        throw new NotImplementedException();
    }

    public Task<string> InstallAsync(MinecraftPath path, string mcversion, string? modversion = null, bool online = true)
    {
        throw new NotImplementedException();
    }
}
