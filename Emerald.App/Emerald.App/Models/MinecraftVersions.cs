using CommunityToolkit.Mvvm.ComponentModel;
using Emerald.WinUI.Helpers;
using Microsoft.UI.Xaml;
using ProjBobcat.Class.Model.Optifine;
using System.Collections.ObjectModel;
using System.Linq;
namespace Emerald.WinUI.Models
{
    public partial class MinecraftVersion : ObservableObject
    {
        public string BlockImageLocation
        {
            get => (MISC != null && MISC.GetType() == typeof(OptifineDownloadVersionModel)) || DisplayVersion == "Optifine" ?
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
            get => SubVersions.Count > 0 || Version == null ? Visibility.Collapsed : Visibility.Visible;
        }


        [ObservableProperty]
        private CmlLib.Core.Version.MVersionType? type;

        public string TypeString
            => ("Type" + Type == null ? CmlLib.Core.Version.MVersionType.OldAlpha.ToString() : Type.ToString()).Localize();

        [ObservableProperty]
        private string version;

        [ObservableProperty]
        private string displayVersion;

        [ObservableProperty]
        private ObservableCollection<MinecraftVersion> subVersions = new();

        public object MISC { get; set; }

        public string GetLaunchVersion()
        {
            return Version.IsNullEmptyOrWhiteSpace()
                ? null : (Version.StartsWith("fabricMC-") ? Version.Replace("fabricMC-", "") : (Version.StartsWith("vanilla-") ? Version.Replace("vanilla-", "") : Version));
        }
    }
}
