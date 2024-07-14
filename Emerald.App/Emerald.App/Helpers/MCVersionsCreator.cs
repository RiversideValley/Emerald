using Emerald.Core;
using Emerald.WinUI.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using SS = Emerald.WinUI.Helpers.Settings.SettingsSystem;

namespace Emerald.WinUI.Helpers
{
    public class MCVersionsCreator
    {
        private ObservableCollection<MinecraftVersion> Collection;

        public MinecraftVersion GetNotSelectedVersion()
        {
            return new MinecraftVersion() { DisplayVersion = Localized.PickVer.Localize() };
        }

        public ObservableCollection<MinecraftVersion> CreateVersions()
        {
            Collection = new();

            if (App.Current.Launcher.MCVersions != null && App.Current.Launcher.MCVersions.Any())
            {
                if (App.Current.Launcher.UseOfflineLoader)
                {
                    Collection = LoadCustomVers().SubVersions;
                }
                else
                {
                    var lr = App.Current.Launcher.MCVersions.LatestReleaseVersion?.Name;
                    var ls = App.Current.Launcher.MCVersions.LatestSnapshotVersion;
                    var l = CreateItem("Latest".Localize(), "latest");
                    l.SubVersions = new();

                    if (lr != null)
                        l.SubVersions.Add(ReturnMCWithModLoaders(lr, $"{"Latest".Localize()} {"TypeRelease".Localize()}", CmlLib.Core.Version.MVersionType.Release));
                    

                    if (ls != null && ls.MType == CmlLib.Core.Version.MVersionType.Snapshot)                    
                        l.SubVersions.Add(ReturnMCWithModLoaders(ls.Name, $"{"Latest".Localize()} {"TypeSnapshot".Localize()}", CmlLib.Core.Version.MVersionType.Snapshot));
                    

                    if (l.SubVersions.Any())
                        Collection.Add(l);
                    

                    if (SS.Settings.Minecraft.MCVerionsConfiguration.Custom && LoadCustomVers() != null)
                    {
                        Collection.Add(LoadCustomVers());
                    }

                    AddItem("1.21");
                    AddItem("1.20");
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

        public ObservableCollection<MinecraftVersion> CreateAllVersions()
        {
            if (App.Current.Launcher.UseOfflineLoader)
            {
                var c = LoadCustomVers();

                return c == null ? new() : c.SubVersions;
            }
            else
            {
                var l = new List<MinecraftVersion>();

                MinecraftVersion[] GetVers(string ver)
                {
                    string fabricVer = App.Current.Launcher.SearchFabric(ver);
                    var verMdata = App.Current.Launcher.MCVersions.Where(x => x.Name == ver).FirstOrDefault();

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

                foreach (var item in App.Current.Launcher.MCVerNames)
                {
                    l.AddRange(GetVers(item));
                }

                return new ObservableCollection<MinecraftVersion>(l);
            }
        }

        private void AddItem(string ver)
        {
            var m = GetFromStrings(ver);
            if (m != null && !(m.Type == null && m.SubVersions.Count == 0))
            {
                Collection.Add(m);
            }
        }

        private MinecraftVersion LoadCustomVers()
        {
            var m = CreateItem("TypeCustom".Localize(), "custom");
            var sub = App.Current.Launcher.MCVersions.Where(x => x.MType == CmlLib.Core.Version.MVersionType.Custom);
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

        private List<CmlLib.Core.Version.MVersionType> ConfigToList(bool custom = false)
        {
            var list = new List<CmlLib.Core.Version.MVersionType>();
            if (SS.Settings.Minecraft.MCVerionsConfiguration.Release) { list.Add(CmlLib.Core.Version.MVersionType.Release); }
            if (SS.Settings.Minecraft.MCVerionsConfiguration.OldBeta) { list.Add(CmlLib.Core.Version.MVersionType.OldBeta); }
            if (SS.Settings.Minecraft.MCVerionsConfiguration.OldAlpha) { list.Add(CmlLib.Core.Version.MVersionType.OldAlpha); }
            if (SS.Settings.Minecraft.MCVerionsConfiguration.Snapshot) { list.Add(CmlLib.Core.Version.MVersionType.Snapshot); }
            if (SS.Settings.Minecraft.MCVerionsConfiguration.Custom && custom) { list.Add(CmlLib.Core.Version.MVersionType.Custom); }
            return list;
        }

        private MinecraftVersion GetFromStrings(string ver)
        {
            if (App.Current.Launcher.MCVerNames.Contains(ver))
            {
                var verMdata = App.Current.Launcher.MCVersions.Where(x => x.Name == ver).FirstOrDefault();
                if (verMdata.Name.ToLower().Contains("optifine") || (!ConfigToList().Contains(verMdata.MType) && verMdata.MType != CmlLib.Core.Version.MVersionType.Custom))
                {
                    return null;
                }

                var subVers = App.Current.Launcher.GetSubVersions(ver);
                subVers = subVers.Where(x => !x.ToLower().Contains("optifine")).ToArray();

                if (subVers.Length > 1)
                {
                    MinecraftVersion f = CreateItem(ver, ver);
                    f.SubVersions = new();

                    foreach (var item in subVers)
                    {
                        var SverMdata = App.Current.Launcher.MCVersions.Where(x => x.Name == item).FirstOrDefault();
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
                var subVers = App.Current.Launcher.GetSubVersions(ver);
                if (subVers.Length > 1)
                {
                    MinecraftVersion f = CreateItem(ver, ver);
                    f.SubVersions = new();

                    foreach (var item in subVers)
                    {
                        var SverMdata = App.Current.Launcher.MCVersions.Where(x => x.Name == item).FirstOrDefault();
                        if (ConfigToList().Contains(SverMdata.MType) || (SverMdata.MType == CmlLib.Core.Version.MVersionType.Custom && !SverMdata.Name.ToLower().Contains("optifine")))
                        {
                            f.SubVersions.Add(ReturnMCWithModLoaders(item));
                        }
                    }

                    return f;
                }
                else if (subVers.Length == 1)
                {
                    var SverMdata = App.Current.Launcher.MCVersions.Where(x => x.Name == subVers.FirstOrDefault()).FirstOrDefault();
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

        private MinecraftVersion ReturnMCWithModLoaders(string ver, string displayVer = null, CmlLib.Core.Version.MVersionType? type = null)
        {
            string fabricVer = App.Current.Launcher.SearchFabric(ver);
            var verMdata = App.Current.Launcher.MCVersions.Where(x => x.Name == ver).FirstOrDefault();
            MinecraftVersion Optifines = new();

            if (App.Current.Launcher.OptifineMCVersions != null && App.Current.Launcher.OptifineMCVersions.Any())
            {
                var Optfvers = App.Current.Launcher.OptifineMCVersions.Where(x => x.McVersion == ver);
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
            else if (string.IsNullOrEmpty(fabricVer) && Optifines != null && Optifines.SubVersions.Any())
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
                if (Optifines.SubVersions.Any())
                {
                    i.SubVersions.Add(Optifines);
                }

                return i;
            }
        }

        private MinecraftVersion CreateItem(
            string DisplayVer,
            string ver, CmlLib.Core.Version.MVersionType? type = null,
            string blockPath = "/Assets/icon.png",
            object misc = null)
            => new() { MISC = misc, Type = type, Version = ver, DisplayVersion = DisplayVer };
    }
}
