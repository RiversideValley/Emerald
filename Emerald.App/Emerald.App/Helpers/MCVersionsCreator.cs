using Emerald.Core;
using Emerald.WinUI.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using SS = Emerald.WinUI.Helpers.Settings.SettingsSystem;
namespace Emerald.WinUI.Helpers
{
    public static class MCVersionsCreator
    {
        private static ObservableCollection<MinecraftVersion> Collection;
        public static MinecraftVersion GetNotSelectedVersion()
        {
            return new MinecraftVersion() { DisplayVersion = Core.Localized.PickVer.ToLocalizedString() };
        }
        public static ObservableCollection<MinecraftVersion> CreateVersions()
        {
            Collection = new();
            if (App.Launcher.MCVersions != null && App.Launcher.MCVersions.Any())
            {
                if (App.Launcher.UseOfflineLoader)
                {
                    Collection = LoadCustomVers().SubVersions;
                }
                else
                {
                    var lr = App.Launcher.MCVersions.LatestReleaseVersion?.Name;
                    var ls = App.Launcher.MCVersions.LatestSnapshotVersion;
                    var l = CreateItem("Latest", "latest");
                    l.SubVersions = new();
                    if (lr != null)
                    {
                        l.SubVersions.Add(ReturnMCWithModLoaders(lr, "Latest Release", CmlLib.Core.Version.MVersionType.Release));
                    }
                    if (ls != null && ls.MType == CmlLib.Core.Version.MVersionType.Snapshot)
                    {
                        l.SubVersions.Add(ReturnMCWithModLoaders(ls.Name, "Latest Snapshot", CmlLib.Core.Version.MVersionType.Snapshot));
                    }
                    if (l.SubVersions.Count > 0)
                    {
                        Collection.Add(l);
                    }
                    if (SS.Settings.Minecraft.MCVerionsConfiguration.Custom && LoadCustomVers() != null)
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
                }
            }
            return Collection;
        }
        public static ObservableCollection<MinecraftVersion> CreateAllVersions()
        {
            if (App.Launcher.UseOfflineLoader)
            {
                var c = LoadCustomVers();
                return c == null ? new() : c.SubVersions;
            }
            else
            {
                var l = new List<MinecraftVersion>();
                MinecraftVersion[] GetVers(string ver)
                {
                    string fabricVer = App.Launcher.SearchFabric(ver);
                    var verMdata = App.Launcher.MCVersions.Where(x => x.Name == ver).FirstOrDefault();
                    if (string.IsNullOrEmpty(fabricVer))
                    {
                        if (ConfigToList(true).Contains(verMdata.MType))
                        {
                            return new MinecraftVersion[] { CreateItem(ver, "vanilla-" + ver, type: verMdata.MType) };
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
                            return new MinecraftVersion[] { CreateItem($"{ver} Vanilla", "vanilla-" + ver, type: verMdata.MType), CreateItem($"{ver} Fabric", "fabricMC-" + fabricVer, type: verMdata.MType) };
                        }
                        else
                        {
                            return Array.Empty<MinecraftVersion>();
                        }
                    }
                }

                foreach (var item in App.Launcher.MCVerNames)
                {
                    l.AddRange(GetVers(item));
                }
                return new ObservableCollection<MinecraftVersion>(l);
            }
        }
        private static void AddItem(string ver)
        {
            var m = GetFromStrings(ver);
            if (m != null && !(m.Type == null && m.SubVersions.Count == 0))
            {
                Collection.Add(m);
            }
        }
        private static MinecraftVersion LoadCustomVers()
        {
            var m = CreateItem("Custom", "custom");
            var sub = App.Launcher.MCVersions.Where(x => x.MType == CmlLib.Core.Version.MVersionType.Custom);
            m.SubVersions = new();
            if (sub != null && sub.Any())
            {
                foreach (var item in sub)
                {
                    m.SubVersions.Add(CreateItem(item.Name, item.Name, CmlLib.Core.Version.MVersionType.Custom));
                }
            }
            if (m.SubVersions.Count > 0)
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
            if (SS.Settings.Minecraft.MCVerionsConfiguration.Release) { list.Add(CmlLib.Core.Version.MVersionType.Release); }
            if (SS.Settings.Minecraft.MCVerionsConfiguration.OldBeta) { list.Add(CmlLib.Core.Version.MVersionType.OldBeta); }
            if (SS.Settings.Minecraft.MCVerionsConfiguration.OldAlpha) { list.Add(CmlLib.Core.Version.MVersionType.OldAlpha); }
            if (SS.Settings.Minecraft.MCVerionsConfiguration.Snapshot) { list.Add(CmlLib.Core.Version.MVersionType.Snapshot); }
            if (SS.Settings.Minecraft.MCVerionsConfiguration.Custom && custom) { list.Add(CmlLib.Core.Version.MVersionType.Custom); }
            return list;
        }
        private static MinecraftVersion GetFromStrings(string ver)
        {
            if (App.Launcher.MCVerNames.Contains(ver))
            {
                var verMdata = App.Launcher.MCVersions.Where(x => x.Name == ver).FirstOrDefault();
                if (verMdata.Name.ToLower().Contains("optifine") || (!ConfigToList().Contains(verMdata.MType) && verMdata.MType != CmlLib.Core.Version.MVersionType.Custom))
                {
                    return null;
                }
                var subVers = App.Launcher.GetSubVersions(ver);
                subVers = subVers.Where(x => !x.ToLower().Contains("optifine")).ToArray();
                if (subVers.Length > 1)
                {
                    MinecraftVersion f = CreateItem(ver, ver);
                    f.SubVersions = new();
                    foreach (var item in subVers)
                    {
                        var SverMdata = App.Launcher.MCVersions.Where(x => x.Name == item).FirstOrDefault();
                        if (ConfigToList().Contains(SverMdata.MType) || SverMdata.MType == CmlLib.Core.Version.MVersionType.Custom)
                        {
                            f.SubVersions.Add(ReturnMCWithModLoaders(item));
                        }
                    }
                    return f;
                }
                else
                {
                    return CreateItem($"{ver} Vanilla", "vanilla-" + ver);
                }
            }
            else
            {
                var subVers = App.Launcher.GetSubVersions(ver);
                if (subVers.Length > 1)
                {
                    MinecraftVersion f = CreateItem(ver, ver);
                    f.SubVersions = new();
                    foreach (var item in subVers)
                    {
                        var SverMdata = App.Launcher.MCVersions.Where(x => x.Name == item).FirstOrDefault();
                        if (ConfigToList().Contains(SverMdata.MType) || (SverMdata.MType == CmlLib.Core.Version.MVersionType.Custom && !SverMdata.Name.ToLower().Contains("optifine")))
                        {
                            f.SubVersions.Add(ReturnMCWithModLoaders(item));
                        }
                    }
                    return f;
                }
                else if (subVers.Length == 1)
                {
                    var SverMdata = App.Launcher.MCVersions.Where(x => x.Name == subVers.FirstOrDefault()).FirstOrDefault();
                    if (ConfigToList().Contains(SverMdata.MType) || (SverMdata.MType == CmlLib.Core.Version.MVersionType.Custom && !SverMdata.Name.ToLower().Contains("optifine")))
                    {
                        return ReturnMCWithModLoaders(subVers.FirstOrDefault());
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
        private static MinecraftVersion ReturnMCWithModLoaders(string ver, string displayVer = null, CmlLib.Core.Version.MVersionType? type = null)
        {
            string fabricVer = App.Launcher.SearchFabric(ver);
            var verMdata = App.Launcher.MCVersions.Where(x => x.Name == ver).FirstOrDefault();
            MinecraftVersion Optifines = new();
            if(App.Launcher.OptifineMCVersions != null && App.Launcher.OptifineMCVersions.Any())
            {
                var Optfvers = App.Launcher.OptifineMCVersions.Where(x => x.McVersion == ver);
                if (Optfvers.Any())
                {
                    Optifines = CreateItem("Optifine", null);
                    Optifines.SubVersions = new(Optfvers.Select(x => CreateItem($"{displayVer ?? x.McVersion} Optifine {x.Type}-{x.Patch}", x.ToFullVersion(), type ?? verMdata.MType, misc: x)).ToArray());
                }
            }
            if (string.IsNullOrEmpty(fabricVer) && (Optifines == null || !Optifines.SubVersions.Any()))
            {
                return CreateItem($"{displayVer ?? ver} Vanilla", "vanilla-" + ver, type: type ?? verMdata.MType);
            }
            else if(string.IsNullOrEmpty(fabricVer) && Optifines != null && Optifines.SubVersions.Any())
            {
                var i = CreateItem(displayVer ?? ver, ver);
                i.SubVersions = new()
                {
                    CreateItem($"{displayVer ?? ver} Vanilla", "vanilla-" + ver, type: type ?? verMdata.MType),
                    Optifines
                };
                return i;
            }
            else
            {
                var i = CreateItem(displayVer ?? ver, ver);
                i.SubVersions = new()
                {
                    CreateItem($"{displayVer ?? ver} Vanilla", "vanilla-" + ver, type: type ?? verMdata.MType),
                    CreateItem($"{displayVer ?? ver} Fabric", "fabricMC-" + fabricVer,  type: type ?? verMdata.MType)
                };
                if(Optifines.SubVersions.Any())
                {
                    i.SubVersions.Add(Optifines);
                }
                return i;
            }
        }
        private static MinecraftVersion CreateItem(string DisplayVer, string ver, CmlLib.Core.Version.MVersionType? type = null, string blockPath = "/Assets/icon.png",object misc = null) => new() { MISC = misc, Type = type, Version = ver, DisplayVersion = DisplayVer };
    }
}