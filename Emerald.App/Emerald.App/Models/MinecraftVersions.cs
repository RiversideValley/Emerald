using Emerald.WinUI.Helpers;
using Microsoft.UI.Xaml;
using System.Collections.ObjectModel;
using System.Linq;
using ProjBobcat.Class.Model.Optifine;

namespace Emerald.WinUI.Models
{
    public class MinecraftVersion : Model
    {
        public string BlockImageLocation
        {
            get => (MISC != null && MISC.GetType() == typeof(OptifineDownloadVersionModel)) || DisplayVersion == "Optifine"?
                        "/Assets/Blocks/CraftingTable.png"
                        :
                        (SubVersions.Count > 0 ?
                            (SubVersions.Count <= 3 ? 
                                (SubVersions.FirstOrDefault().SubVersions.Count > 0 ? 
                                    "/Assets/Blocks/Furnace_Burn.png" 
                                    : 
                                    "/Assets/Blocks/Furnace.png"
                                ) 
                            : 
                            "/Assets/Blocks/Furnace_Burn.png"
                            )
                        :
                        (Version == null ? 
                            "/Assets/Blocks/Dirt.png" 
                            : 
                            (Version.ToLower().Contains("fabric") ? 
                                "/Assets/Blocks/Redstone.png" 
                                :
                                (Version.ToLower().Contains("optifine") ? 
                                    "/Assets/Blocks/CraftingTable.png" 
                                    :
                                    "/Assets/Blocks/Dirt.png"
                                )
                            )
                        )
                        );
        }

        public Visibility DescriptionVisibility
        {
            get => SubVersions.Count > 0 || _Version == null ? Visibility.Collapsed : Visibility.Visible;
        }

        private CmlLib.Core.Version.MVersionType? _Type;
        public CmlLib.Core.Version.MVersionType? Type { get => _Type; set => Set(ref _Type, value); }

        public string TypeString
            => ("Type" + Type == null ? CmlLib.Core.Version.MVersionType.OldAlpha.ToString() : Type.ToString()).Localize();

        private string _Version;
        public string Version { get => _Version; set => Set(ref _Version, value); }

        private string _DisplayVersion;
        public string DisplayVersion { get => _DisplayVersion; set => Set(ref _DisplayVersion, value); }

        private ObservableCollection<MinecraftVersion> _SubVersions;
        public ObservableCollection<MinecraftVersion> SubVersions { get => _SubVersions ?? new(); set => Set(ref _SubVersions, value); }

        public object MISC { get; set; }

        public string GetLaunchVersion()
        {
            return Version.IsNullEmptyOrWhiteSpace()
                ? null : (Version.StartsWith("fabricMC-") ? Version.Replace("fabricMC-", "") : (Version.StartsWith("vanilla-") ? Version.Replace("vanilla-", "") : Version));
        }
    }
}
