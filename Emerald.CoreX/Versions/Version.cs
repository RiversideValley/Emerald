using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Emerald.CoreX.Versions;

public enum Type
{
    Vanilla,
    Forge,
    Fabric,
    Quilt,
    LiteLoader,
    OptiFine
}
public  class Version
{
    public Type Type { get; set; }
    
    public string BasedOn { get; set; }

    public string? ModVersion { get; set; }

    public CmlLib.Core.Version.MinecraftVersion? McVersion { get; set; }

    public string DisplayName;
}
