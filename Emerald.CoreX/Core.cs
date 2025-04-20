using Microsoft.Extensions.Logging;
using CmlLib.Core;
using CmlLib.Core.VersionMetadata;
namespace Emerald.CoreX;

public class Core
{
    public ILogger _logger { get; set; }
    public MinecraftLauncher Launcher { get; set; }

    public Core(ILogger logger, MinecraftPath path )
    {
        _logger = logger;
        
    }

    public void InitializeLauncher()
    {
        
        Launcher = new MinecraftLauncher();
    }
}
