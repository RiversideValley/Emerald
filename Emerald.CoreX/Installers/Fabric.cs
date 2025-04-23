using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CmlLib.Core;

namespace Emerald.CoreX.Installers;

public class Fabric : IModLoaderInstaller
{

    public Task<List<string>> GetVersionsAsync(string mcVersion)
    {
        
        throw new NotImplementedException();
    }

    public Task InstallAsync(MinecraftPath path, string mcversion, string? modversion = null)
    {
        throw new NotImplementedException();
    }
}
