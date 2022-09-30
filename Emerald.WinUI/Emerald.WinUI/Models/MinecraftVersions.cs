using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Emerald.WinUI.Models
{
    public class MinecraftVersions : Model
    {
        private string _Block;
        public string BlockImageLocation { get => _Block; set => Set(ref _Block, value); }
        
        private string _MinecraftVersion;
        public string Version { get => _MinecraftVersion; set => Set(ref _MinecraftVersion, value); }
    }
}
