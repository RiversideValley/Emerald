using CmlLib.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.WinUI.Helpers;
using Emerald.Core.Store.Enums;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Windows.UI;
namespace Emerald.WinUI.Helpers.Settings.JSON
{
    public class JSON : Models.Model
    {
        public string Serialize()
            => JsonConvert.SerializeObject(this, Formatting.Indented);
    }

    public class SettingsBackup : JSON
    {
        public string Backup { get; set; }
        public string Name { get; set; }
        public DateTime Time { get; set; }
        //ik there is Time.ToString() lol
        public string DateString => $"{Time.ToLongDateString()} {Time.ToShortTimeString()}";
    }

    public class Backups : JSON
    {
        public SettingsBackup[] AllBackups { get; set; } = Array.Empty<SettingsBackup>();
        public string APIVersion { get; private set; } = "1.0";
    }

    public class Settings : JSON
    {
        public static Settings CreateNew() => new()
        {
            App = new()
            {
                Discord = new(),
                Appearance = new()
                {
                    MicaTintColor = (int)Enums.MicaTintColor.NoColor,
                    Theme = (int)ElementTheme.Default
                }
            },
            Minecraft = new()
            {
                Path = MinecraftPath.GetOSDefaultPath(),
                RAM = DirectResoucres.MaxRAM / 2,
                MCVerionsConfiguration = new(),
                JVM = new(),
                Downloader = new()
                {
                    AssetsCheck = true,
                    HashCheck = true
                }
            }
        };

        public string APIVersion { get; set; } = DirectResoucres.SettingsAPIVersion;
        public DateTime LastSaved { get; set; } = DateTime.Now;
        public Minecraft Minecraft { get; set; } = new();

        public App App { get; set; } = new();
    }

    public partial class Minecraft : JSON
    {
        public Minecraft()
        {
            JVM.PropertyChanged += (_, _)
                => InvokePropertyChanged();
            PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != null)
                    InvokePropertyChanged();
            };
        }

        [JsonIgnore]
        public double RAMinGB => Math.Round((RAM / 1024.00), 2);


        [ObservableProperty]
        private string _Path;

        [ObservableProperty]
        private int _RAM;

        [ObservableProperty]
        private bool _IsAdmin;

        public Downloader Downloader { get; set; } = new();

        public MCVerionsConfiguration MCVerionsConfiguration { get; set; }

        public JVM JVM { get; set; } = new();

        public bool ReadLogs()
            => JVM.GameLogs && !IsAdmin;
    }

    public class Account : JSON
    {
        public string Type { get; set; }
        public string Username { get; set; }
        public string AccessToken { get; set; }
        public string ClientToken { get; set; }
        public string UUID { get; set; }
        public bool LastAccessed { get; set; }
    }

    public partial class Downloader : JSON
    {

        [ObservableProperty]
        private bool _HashCheck;

        [ObservableProperty]
        private bool _AssetsCheck;
    }

    public partial class JVM : JSON
    {
        public JVM()
        {
            this.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != null)
                    this.InvokePropertyChanged();
            };
        }

        [ObservableProperty]
        private string[] _Arguments;

        [ObservableProperty]
        private double _ScreenWidth;

        [ObservableProperty]
        private double _ScreenHeight;

        [ObservableProperty]
        private bool _FullScreen;

        [ObservableProperty]
        private bool _GameLogs;

        [JsonIgnore]
        public string ScreenSizeStatus =>
                 FullScreen ? "FullScreen".Localize() : ((ScreenWidth > 0 && ScreenHeight > 0) ? $"{ScreenWidth} × {ScreenHeight}" : "Default".Localize());

        [JsonIgnore]
        public bool SetSize => !(ScreenSizeStatus == "FullScreen".Localize() || ScreenSizeStatus == "Default".Localize());

    }

    public class App : JSON
    {
        public Appearance Appearance { get; set; } = new();
        public bool AutoLogin { get; set; }
        public Discord Discord { get; set; } = new();
        public NewsFilter NewsFilter { get; set; } = new();
        public Store Store { get; set; } = new();
        public Updates Updates { get; set; } = new();
        public bool AutoClose { get; set; }
        public bool HideOnLaunch { get; set; }
        public bool WindowsHello { get; set; }

    }
    public class Updates : JSON
    {
        
        public bool CheckAtStartup { get; set; } = true;
        public bool AutoDownload { get; set; }
        public bool IncludePreReleases { get; set; }
    }
    public partial class StoreFilter : JSON
    {
        [ObservableProperty]
        private bool _Fabric;

        [ObservableProperty]
        private bool _Forge;

        [ObservableProperty]
        private bool _Adventure;

        [ObservableProperty]
        private bool _Cursed;

        [ObservableProperty]
        private bool _Decoration;

        [ObservableProperty]
        private bool _Equipment;

        [ObservableProperty]
        private bool _Food;

        [ObservableProperty]
        private bool _Library;

        [ObservableProperty]
        private bool _Magic;

        [ObservableProperty]
        private bool _Misc;

        [ObservableProperty]
        private bool _Optimization;

        [ObservableProperty]
        private bool _Storage;

        [ObservableProperty]
        private bool _Technology;

        [ObservableProperty]
        private bool _Utility;

        [ObservableProperty]
        private bool _Worldgen;

        [JsonIgnore]
        public bool All
        {
            get => GetResult().Length == 0;
            set
            {
                _Fabric = _Forge = _Adventure = _Cursed = _Decoration = _Equipment = _Food = _Library = _Magic = _Misc = _Optimization = _Storage = _Technology = _Utility = _Worldgen = false;
                InvokePropertyChanged(null);
            }
        }
        public SearchCategories[] GetResult()
        {
            if (Fabric & Forge & Adventure & Cursed & Decoration & Equipment & Food & Library & Magic & Misc & Optimization & Storage & Technology & Utility & Worldgen)
                return Array.Empty<SearchCategories>();

            var r = new List<SearchCategories>();

            if (Fabric)
                r.Add(SearchCategories.Fabric);

            if (Forge)
                r.Add(SearchCategories.Forge);

            if (Adventure)
                r.Add(SearchCategories.Adventure);

            if (Cursed)
                r.Add(SearchCategories.Cursed);

            if (Decoration)
                r.Add(SearchCategories.Decoration);

            if (Equipment)
                r.Add(SearchCategories.Equipment);

            if (Food)
                r.Add(SearchCategories.Food);

            if (Library)
                r.Add(SearchCategories.Library);

            if (Magic)
                r.Add(SearchCategories.Magic);

            if (Misc)
                r.Add(SearchCategories.Misc);

            if (Optimization)
                r.Add(SearchCategories.Optimization);

            if (Storage)
                r.Add(SearchCategories.Storage);

            if (Technology)
                r.Add(SearchCategories.Technology);

            if (Utility)
                r.Add(SearchCategories.Utility);

            if (Worldgen)
                r.Add(SearchCategories.Worldgen);

            return r.ToArray();
        }
    }
    public class Store : JSON
    {
        public StoreFilter Filter { get; set; } = new();
        public StoreSortOptions SortOptions { get; set; } = new();
    }

    public partial class StoreSortOptions : JSON
    {

        [ObservableProperty]
        private bool _Relevance = true;

        [ObservableProperty]
        private bool _Downloads;

        [ObservableProperty]
        private bool _Follows;

        [ObservableProperty]
        private bool _Updated;

        [ObservableProperty]
        private bool _Newest;

        public SearchSortOptions GetResult()
        {
            if (!(Relevance || Downloads || Follows || Updated || Newest))
                return SearchSortOptions.Relevance;
            else
                return Relevance ? SearchSortOptions.Relevance : (Downloads ? SearchSortOptions.Downloads : (Follows ? SearchSortOptions.Follows : (Updated ? SearchSortOptions.Updated : SearchSortOptions.Newest)));
        }
    }
    public partial class NewsFilter : JSON
    {
        [ObservableProperty]
        private bool _Java = false;

        [ObservableProperty]
        private bool _Bedrock = false;

        [ObservableProperty]
        private bool _Dungeons = false;

        [ObservableProperty]
        private bool _Legends = false;

        [JsonIgnore]
        public bool All
        {
            get => GetResult().Length == 4;
            set
            {
                Java = Bedrock = Dungeons = Legends = false;
                InvokePropertyChanged(null);
            }
        }
        public string[] GetResult()
        {
            var r = new List<string>();

            if (!Java && !Bedrock && !Dungeons && !Legends)
            {
                r.Add("Minecraft: Java Edition");
                r.Add("Minecraft for Windows");
                r.Add("Minecraft Dungeons");
                r.Add("Minecraft Legends");
                return r.ToArray();
            }

            if (Java)
                r.Add("Minecraft: Java Edition");

            if (Bedrock)
                r.Add("Minecraft for Windows");

            if (Dungeons)
                r.Add("Minecraft Dungeons");

            if (Legends)
                r.Add("Minecraft Legends");

            return r.ToArray();
        }
    }
    public class Discord : JSON
    {
    }
    public partial class MCVerionsConfiguration : JSON
    {
        [ObservableProperty]
        private bool _Release = true;

        [ObservableProperty]
        private bool _Custom = false;

        [ObservableProperty]
        private bool _OldBeta = false;

        [ObservableProperty]
        private bool _OldAlpha = false;

        [ObservableProperty]
        private bool _Snapshot = false;
    }

    public partial class Appearance : JSON
    {
        [ObservableProperty]
        private int _NavIconType = 1;

        public bool ShowFontIcons => NavIconType == 0;

        [ObservableProperty]
        private int _Theme;

        [ObservableProperty]
        private int _MicaTintColor;

        [ObservableProperty]
        private int _MicaType = 0;

        [ObservableProperty]
        private (int A, int R, int G, int B)? _CustomMicaTintColor;


        //I had to do this because whenever the app has no Mica it won't change the background color when requested theme changes unless the Windows main theme gets changed.
        [JsonIgnore]
        public Color Win10BackgroundColor => (SystemInformation.Instance.OperatingSystemVersion.Build < 22000) ? (Emerald.WinUI.App.Current.ActualTheme == ElementTheme.Light ? Colors.White : Colors.Black) : Colors.Transparent;

        public Appearance()
        {
            this.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != null)
                    this.InvokePropertyChanged();
            };
        }
    }
}
