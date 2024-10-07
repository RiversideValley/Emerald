using Microsoft.Extensions.Logging;
using CmlLib.Core;
using CmlLib.Core.VersionMetadata;
namespace Emerald.CoreX;

public class Core(ILogger logger)
{
    public MinecraftLauncher Launcher { get; set; }
    public void InitializeLauncher()
    {
        Launcher = new MinecraftLauncher();
    }
}