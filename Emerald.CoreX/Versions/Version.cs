using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Emerald.CoreX.Versions;

public  class Version
{
    public string Type { get; set; }
    private string BasedOn { get; set; }
    private CmlLib.Core.Version.MinecraftVersion? McVersion { get; set; }

    public string UniqueName;
}
