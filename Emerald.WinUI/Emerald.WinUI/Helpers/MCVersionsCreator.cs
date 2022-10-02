using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using Emerald.WinUI.Models;
namespace Emerald.WinUI.Helpers
{
    public static class MCVersionsCreator
    {
        public static class Configuration
        {
            public static bool Release { get; set; } = true;
            public static bool Custom { get; set; } = false;
            public static bool OldBeta { get; set; } = false;
            public static bool OldAlpha { get; set; } = false;
            public static bool Snapshot { get; set; } = false;
        }
        private static ObservableCollection<MinecraftVersion> Collection;
        public static ObservableCollection<MinecraftVersion> CreateVersions()
        {
            Collection = new();
            AddItem("1.19");
            AddItem("1.18");
            AddItem("1.17");
            AddItem("1.16");
            AddItem("1.15");
            AddItem("1.12");
            AddItem("1.11");
            AddItem("1.10");
            AddItem("1.9");
            AddItem("1.8");
            AddItem("1.7");
            AddItem("1.6");
            AddItem("1.5");
            AddItem("1.4");
            AddItem("1.3");
            AddItem("1.2");
            AddItem("1.1");
            return Collection;
        }
        private static void AddItem(string ver)
        {
            var m = GetFromStrings(ver);
            if (m != null)
            {
                Collection.Add(m);
            }
        }
        private static List<CmlLib.Core.Version.MVersionType> ConfigToList()
        {
            var list = new List<CmlLib.Core.Version.MVersionType>();
            if (Configuration.Release) { list.Add(CmlLib.Core.Version.MVersionType.Release); }
            if (Configuration.OldBeta) { list.Add(CmlLib.Core.Version.MVersionType.OldBeta); }
            if (Configuration.OldAlpha) { list.Add(CmlLib.Core.Version.MVersionType.OldAlpha); }
            if (Configuration.Snapshot) { list.Add(CmlLib.Core.Version.MVersionType.Snapshot); }
            if (Configuration.Custom) { list.Add(CmlLib.Core.Version.MVersionType.Custom); }
            return list;
        }
        private static MinecraftVersion GetFromStrings(string ver)
        {
            if (Core.MainCore.Launcher.MCVerNames.Contains(ver))
            {
                var verMdata = Core.MainCore.Launcher.MCVersions.Where(x => x.Name == ver).FirstOrDefault();
                if (!ConfigToList().Contains(verMdata.MType))
                {
                    return null;
                }
                var subVers = Core.MainCore.Launcher.GetSubVersions(ver);
                if (subVers.Count() > 1)
                {
                    MinecraftVersion f = CreateItem(ver, ver);
                    f.SubVersions = new();
                    foreach (var item in subVers)
                    {
                        var SverMdata = Core.MainCore.Launcher.MCVersions.Where(x => x.Name == item).FirstOrDefault();
                        if (ConfigToList().Contains(SverMdata.MType))
                        {
                            f.SubVersions.Add(ReturnMCWithFabric(item));
                        }
                    }
                    return f;
                }
                else
                {
                    return CreateItem($"{ver} Vanilla", "vaniila-" + ver);
                }
            }
            else
            {
                return null;
            }
        }
        private static MinecraftVersion ReturnMCWithFabric(string ver)
        {
            string fabricVer = Core.MainCore.Launcher.SearchFabric(ver);
            if (string.IsNullOrEmpty(fabricVer))
            {
                return CreateItem(ver, "vaniila-" + ver);
            }
            else
            {
                var i = CreateItem(ver, ver);
                i.SubVersions = new();
                i.SubVersions.Add(CreateItem($"{ver} Vanilla", "vaniila-" + ver));
                i.SubVersions.Add(CreateItem($"{ver} Fabric", "fabricMC-" + fabricVer));
                return i;
            }
        }
        private static MinecraftVersion CreateItem(string DisplayVer, string ver, CmlLib.Core.Version.MVersionType? type = null, string blockPath = "/Assets/icon.png") => new() { Type = type, Version = ver, DisplayVersion = DisplayVer, BlockImageLocation = blockPath };

        //private void MCVerClick(object sender, RoutedEventArgs e)
        //{
        //    if (sender is MenuFlyoutItem mit)
        //    {
        //        if (mit.Tag.ToString().StartsWith("fabricMC-"))
        //        {
        //            this.ItemInvoked(this, new ItemInvokedArgs(mit.Tag.ToString().Replace("fabricMC-", "").ToString(), mit.Text, MCType.Fabric));
        //        }
        //        else if (mit.Tag.ToString().StartsWith("vanilla-"))
        //        {
        //            this.ItemInvoked(this, new ItemInvokedArgs(mit.Tag.ToString().Replace("vanilla-", "").ToString(), mit.Text, MCType.Vanilla));
        //        }
        //    }
        //}
    }
}
