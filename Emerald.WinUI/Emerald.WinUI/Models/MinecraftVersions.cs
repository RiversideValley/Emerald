using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Emerald.WinUI.Models
{
    public class MinecraftVersion : Model
    {
        private string _Block;
        public string BlockImageLocation
        {
            get
            {
                if(SubVersions.Count > 0)
                {
                    return SubVersions.Count <= 2 ? (SubVersions.FirstOrDefault().SubVersions.Count > 0 ? "/Assets/Blocks/Furnace_Burn.png" : "/Assets/Blocks/Furnace.png") : "/Assets/Blocks/Furnace_Burn.png";
                }
                else
                {
                    if (_Version.Contains("fabric"))
                    {
                        return "/Assets/Blocks/Redstone.png";
                    }
                    else
                    {
                        return "/Assets/Blocks/Dirt.png";
                    }
                }
            }
            set => Set(ref _Block, value);
        }

        public Visibility DescriptionVisibility
        {
            get => SubVersions.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        private CmlLib.Core.Version.MVersionType? _Type;
        public CmlLib.Core.Version.MVersionType? Type { get => _Type; set => Set(ref _Type, value); }

        private string _Version;
        public string Version { get => _Version; set => Set(ref _Version, value); }

        private string _DisplayVersion;
        public string DisplayVersion { get => _DisplayVersion; set => Set(ref _DisplayVersion, value); }

        private ObservableCollection<MinecraftVersion> _SubVersions;
        public ObservableCollection<MinecraftVersion> SubVersions { get => _SubVersions ?? new(); set => Set(ref _SubVersions, value); }
    }
}
