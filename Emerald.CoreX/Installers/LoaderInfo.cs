using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Emerald.CoreX.Installers;
public class LoaderInfo
{
    public string? Separator { get; set; } 

    public int? Build { get; set; }

    public string? Maven { get; set; }

    public string? Version { get; set; }

    public bool? Stable { get; set; } = null;
}
