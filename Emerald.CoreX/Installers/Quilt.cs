using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CmlLib.Core;

namespace Emerald.CoreX.Installers;

public class Quilt : IModLoaderInstaller
{
    public Versions.Type Type => Versions.Type.Quilt;

    public Task<List<LoaderInfo>> GetVersionsAsync(string mcVersion)
    {
        throw new NotImplementedException();
    }

    public Task<string> InstallAsync(MinecraftPath path, string mcversion, string? modversion = null)
    {
        throw new NotImplementedException();
    }
}
