using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CmlLib.Core;

namespace Emerald.CoreX.Installers;

internal interface IModLoaderInstaller
{
    public Task<List<string>> GetVersionsAsync(string mcVersion);

    public Task InstallAsync(
        MinecraftPath path,
        string mcversion,
        string? modversion = null
    );
}
