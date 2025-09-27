using CmlLib.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using System.Text.Json;
using System.Text.Json.Serialization;
using System;
using System.Collections.Generic;
using Windows.UI;
using CmlLib.Core.ProcessBuilder;
using Emerald.CoreX.Models;
using Emerald.CoreX.Store.Modrinth;
using Emerald.CoreX.Helpers;
namespace Emerald.Helpers.Settings.JSON;

public class JSON : Models.Model
{
    public string Serialize()
        => JsonSerializer.Serialize(this);
}

public class SettingsBackup : JSON
{
    public string Backup { get; set; }
    public string Name { get; set; }
    public DateTime Time { get; set; }
    // ik there is Time.ToString() lol
    public string DateString => $"{Time.ToLongDateString()} {Time.ToShortTimeString()}";
}

public class Backups : JSON
{
    public SettingsBackup[] AllBackups { get; set; } = Array.Empty<SettingsBackup>();
    public string APIVersion { get; private set; } = "1.0";
}

public partial class Settings : JSON
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
           // RAM = DirectResoucres.MaxRAM / 2,
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

    [ObservableProperty]
    private CoreX.Models.GameSettings _GameSettings  = GameSettings.FromMLaunchOption(new MLaunchOption());
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
        get =>true;
        set
        {
            _Fabric = _Forge = _Adventure = _Cursed = _Decoration = _Equipment = _Food = _Library = _Magic = _Misc = _Optimization = _Storage = _Technology = _Utility = _Worldgen = false;
            InvokePropertyChanged(null);
        }
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
    private Color? _CustomMicaTintColor;


    [ObservableProperty]
    private int _TintOpacity = 10;

    public Appearance()
    {
        this.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != null)
                this.InvokePropertyChanged();
        };
    }
}
