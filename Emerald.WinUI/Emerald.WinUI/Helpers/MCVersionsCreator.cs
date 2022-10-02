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
            var lr = Core.MainCore.Launcher.MCVersions.LatestReleaseVersion?.Name;
            var ls = Core.MainCore.Launcher.MCVersions.LatestSnapshotVersion;
            var l = CreateItem("Latest", "latest");
            l.SubVersions = new();
            if (lr != null)
            {
                l.SubVersions.Add(ReturnMCWithFabric(lr,"Latest Release"));
            }
            if (ls != null && ls.MType == CmlLib.Core.Version.MVersionType.Snapshot)
            {
                l.SubVersions.Add(ReturnMCWithFabric(ls.Name, "Latest Snapshot"));
            }
            Collection.Add(l);
            if (Configuration.Custom && LoadCustomVers() != null)
            {
                Collection.Add(LoadCustomVers());
            }
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
        public static ObservableCollection<MinecraftVersion> CreateAllVersions()
        {
            var l = new List<MinecraftVersion>();
            MinecraftVersion[] GetVers(string ver)
            {
                string fabricVer = Core.MainCore.Launcher.SearchFabric(ver);
                var verMdata = Core.MainCore.Launcher.MCVersions.Where(x => x.Name == ver).FirstOrDefault();
                if (string.IsNullOrEmpty(fabricVer))
                {
                    if (ConfigToList(true).Contains(verMdata.MType))
                    {
                        return new MinecraftVersion[] { CreateItem(ver, "vaniila-" + ver, type: verMdata.MType) };
                    }
                    else
                    {
                        return Array.Empty<MinecraftVersion>();
                    }
                }
                else
                {
                    if (ConfigToList(true).Contains(verMdata.MType))
                    {
                        return new MinecraftVersion[] { CreateItem($"{ver} Vanilla", "vaniila-" + ver, type: verMdata.MType), CreateItem($"{ver} Fabric", "fabricMC-" + fabricVer, type: verMdata.MType) };
                    }
                    else
                    {
                        return Array.Empty<MinecraftVersion>();
                    }
                }
            }

            foreach (var item in Core.MainCore.Launcher.MCVerNames)
            {
                l.AddRange(GetVers(item));
            }
            return new ObservableCollection<MinecraftVersion>(l);

        }
        private static void AddItem(string ver)
        {
            var m = GetFromStrings(ver);
            if (m != null)
            {
                Collection.Add(m);
            }
        }
        private static MinecraftVersion LoadCustomVers()
        {
            var m = CreateItem("Custom", "custom");
            var sub= Core.MainCore.Launcher.MCVersions.Where(x => x.MType == CmlLib.Core.Version.MVersionType.Custom);
            m.SubVersions = new();
            if(sub != null && sub.Count() > 0)
            {
                foreach (var item in sub)
                {
                    m.SubVersions.Add(CreateItem(item.Name, item.Name, CmlLib.Core.Version.MVersionType.Custom));
                }
            }
            if(m.SubVersions.Count > 0)
            {
                return m;
            }
            else
            {
                return null;
            }
        }
        private static List<CmlLib.Core.Version.MVersionType> ConfigToList(bool custom = false)
        {
            var list = new List<CmlLib.Core.Version.MVersionType>();
            if (Configuration.Release) { list.Add(CmlLib.Core.Version.MVersionType.Release); }
            if (Configuration.OldBeta) { list.Add(CmlLib.Core.Version.MVersionType.OldBeta); }
            if (Configuration.OldAlpha) { list.Add(CmlLib.Core.Version.MVersionType.OldAlpha); }
            if (Configuration.Snapshot) { list.Add(CmlLib.Core.Version.MVersionType.Snapshot); }
            if (Configuration.Custom && custom) { list.Add(CmlLib.Core.Version.MVersionType.Custom); }
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
                if (subVers.Length > 1)
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
                var subVers = Core.MainCore.Launcher.GetSubVersions(ver);
                if (subVers.Length > 1)
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
                else if(subVers.Length == 1)
                {
                    var SverMdata = Core.MainCore.Launcher.MCVersions.Where(x => x.Name == subVers.FirstOrDefault()).FirstOrDefault();
                    if (ConfigToList().Contains(SverMdata.MType))
                    {
                        return ReturnMCWithFabric(subVers.FirstOrDefault());
                    }
                    else
                    {
                        return null;
                    }
                }
                else
                {
                    return null;
                }
            }
        }
        private static MinecraftVersion ReturnMCWithFabric(string ver,string displayVer = null)
        {
            string fabricVer = Core.MainCore.Launcher.SearchFabric(ver);
            var verMdata = Core.MainCore.Launcher.MCVersions.Where(x => x.Name == ver).FirstOrDefault();
            if (string.IsNullOrEmpty(fabricVer))
            {
                return displayVer == null? CreateItem($"{ver} Vanilla", "vaniila-" + ver, type: verMdata.MType) : CreateItem($"{displayVer} Vanilla", "vaniila-" + ver, type: verMdata.MType);
            }
            else
            {
                var i = CreateItem(displayVer ?? ver, ver);
                i.SubVersions = new();
                i.SubVersions.Add(displayVer == null ? CreateItem($"{ver} Vanilla", "vaniila-" + ver, type: verMdata.MType) : CreateItem($"{displayVer} Vanilla", "vaniila-" + ver, type: verMdata.MType));
                i.SubVersions.Add(displayVer == null ? CreateItem($"{ver} Fabric", "fabricMC-" + fabricVer, type: verMdata.MType) : CreateItem($"{displayVer} Fabric", "fabricMC-" + fabricVer, type: verMdata.MType));
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
