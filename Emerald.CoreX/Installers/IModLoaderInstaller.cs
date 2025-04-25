using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CmlLib.Core;

namespace Emerald.CoreX.Installers;

public interface IModLoaderInstaller
{
    public Versions.Type Type { get; }
    public Task<List<LoaderInfo>> GetVersionsAsync(string mcVersion);

    public Task<string> InstallAsync(
        MinecraftPath path,
        string mcversion,
        string? modversion = null,
        bool online = true
    );
}
