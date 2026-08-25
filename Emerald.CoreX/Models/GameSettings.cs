using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using CmlLib.Core.FileExtractors;
using CmlLib.Core.ProcessBuilder;
using Emerald.CoreX.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Runtime.Serialization;
using System.Collections.Specialized;
using Emerald.CoreX.Store;
namespace Emerald.CoreX.Models;
public partial class GameSettings : ObservableObject
{
    public GameSettings()
    {
        JVMArgs.CollectionChanged += OnJvmArgsChanged;
    }

    [JsonIgnore]
    public double MaxRAMinGB => Math.Round((MaximumRamMb / 1024.00), 2);

    [NotifyPropertyChangedFor(nameof(MaxRAMinGB))]
    [ObservableProperty]
    private int _maximumRamMb;

    [ObservableProperty]
    private int _minimumRamMb;

    [ObservableProperty]
    private string? _dockName = "Minecraft";

    [ObservableProperty]
    private bool _isDemo;

    [NotifyPropertyChangedFor(nameof(ScreenSizeStatus))]
    [ObservableProperty]
    private int _screenWidth;

    [NotifyPropertyChangedFor(nameof(ScreenSizeStatus))]
    [ObservableProperty]
    private int _screenHeight;

    [NotifyPropertyChangedFor(nameof(ScreenSizeStatus))]
    [ObservableProperty]
    private bool _fullScreen;

    [ObservableProperty]
    private string? _quickPlayPath;

    [ObservableProperty]
    private string? _quickPlaySingleplayer;

    [ObservableProperty]
    private string? _quickPlayRealms;

    [ObservableProperty]
    private string? _serverIp;

    [ObservableProperty]
    private int _serverPort = 25565;

    [ObservableProperty]
    private bool _IsAdmin;

    [ObservableProperty]
    private bool _UseCustomJava;

    [ObservableProperty]
    private string? _JavaPath;

    [NotifyPropertyChangedFor(nameof(UsesSharedMinecraftFolders))]
    [NotifyPropertyChangedFor(nameof(SharedMinecraftFoldersStatus))]
    [ObservableProperty]
    private bool _useSharedAssetsPath;

    [NotifyPropertyChangedFor(nameof(UsesSharedMinecraftFolders))]
    [NotifyPropertyChangedFor(nameof(SharedMinecraftFoldersStatus))]
    [ObservableProperty]
    private bool _useSharedLibrariesPath;

    [NotifyPropertyChangedFor(nameof(UsesSharedMinecraftFolders))]
    [NotifyPropertyChangedFor(nameof(SharedMinecraftFoldersStatus))]
    [ObservableProperty]
    private bool _useSharedRuntimePath;

    [NotifyPropertyChangedFor(nameof(UsesSharedMinecraftFolders))]
    [NotifyPropertyChangedFor(nameof(SharedMinecraftFoldersStatus))]
    [ObservableProperty]
    private bool _useSharedVersionsPath;

    [NotifyPropertyChangedFor(nameof(UsesSharedStoreFolders))]
    [NotifyPropertyChangedFor(nameof(SharedStoreFoldersStatus))]
    [ObservableProperty]
    private bool _useSharedStoreModsPath;

    [NotifyPropertyChangedFor(nameof(UsesSharedStoreFolders))]
    [NotifyPropertyChangedFor(nameof(SharedStoreFoldersStatus))]
    [ObservableProperty]
    private bool _useSharedStoreResourcePacksPath;

    [NotifyPropertyChangedFor(nameof(UsesSharedStoreFolders))]
    [NotifyPropertyChangedFor(nameof(SharedStoreFoldersStatus))]
    [ObservableProperty]
    private bool _useSharedStoreDataPacksPath;

    [NotifyPropertyChangedFor(nameof(UsesSharedStoreFolders))]
    [NotifyPropertyChangedFor(nameof(SharedStoreFoldersStatus))]
    [ObservableProperty]
    private bool _useSharedStoreShaderPacksPath;
    
    public ObservableCollection<string> JVMArgs { get; set; } = new();

    [JsonIgnore]
    public string ScreenSizeStatus =>
        FullScreen ? "FullScreen".Localize() : ((ScreenWidth > 0 && ScreenHeight > 0) ? $"{ScreenWidth} × {ScreenHeight}" : "Default".Localize());

    [JsonIgnore]
    public bool UsesSharedMinecraftFolders
        => UseSharedAssetsPath
           || UseSharedLibrariesPath
           || UseSharedRuntimePath
           || UseSharedVersionsPath;

    [JsonIgnore]
    public string SharedMinecraftFoldersStatus
    {
        get
        {
            if (!UsesSharedMinecraftFolders)
            {
                return "GodFoldersOffStatus".Localize();
            }

            var folders = new List<string>();
            if (UseSharedAssetsPath)
            {
                folders.Add("GodFoldersAssetsShort".Localize());
            }

            if (UseSharedLibrariesPath)
            {
                folders.Add("GodFoldersLibrariesShort".Localize());
            }

            if (UseSharedRuntimePath)
            {
                folders.Add("GodFoldersRuntimeShort".Localize());
            }

            if (UseSharedVersionsPath)
            {
                folders.Add("GodFoldersVersionsShort".Localize());
            }

            return string.Join(", ", folders);
        }
    }

    [JsonIgnore]
    public bool UsesSharedStoreFolders
        => UseSharedStoreModsPath
           || UseSharedStoreResourcePacksPath
           || UseSharedStoreDataPacksPath
           || UseSharedStoreShaderPacksPath;

    [JsonIgnore]
    public string SharedStoreFoldersStatus
    {
        get
        {
            if (!UsesSharedStoreFolders)
            {
                return "GodFoldersOffStatus".Localize();
            }

            var folders = new List<string>();
            if (UseSharedStoreModsPath)
            {
                folders.Add("StoreGodFoldersModsShort".Localize());
            }

            if (UseSharedStoreResourcePacksPath)
            {
                folders.Add("StoreGodFoldersResourcePacksShort".Localize());
            }

            if (UseSharedStoreDataPacksPath)
            {
                folders.Add("StoreGodFoldersDataPacksShort".Localize());
            }

            if (UseSharedStoreShaderPacksPath)
            {
                folders.Add("StoreGodFoldersShaderPacksShort".Localize());
            }

            return string.Join(", ", folders);
        }
    }

    public bool IsSharedStoreContentEnabled(StoreContentType contentType)
        => contentType switch
        {
            StoreContentType.Mod => UseSharedStoreModsPath,
            StoreContentType.ResourcePack => UseSharedStoreResourcePacksPath,
            StoreContentType.DataPack => UseSharedStoreDataPacksPath,
            StoreContentType.Shader => UseSharedStoreShaderPacksPath,
            _ => false
        };

    public MLaunchOption ToMLaunchOption()
    {
        var opt = new MLaunchOption
        {
            MaximumRamMb = _maximumRamMb,
            MinimumRamMb = _minimumRamMb,
            DockName = _dockName,
            IsDemo = _isDemo,
            ScreenWidth = _screenWidth,
            ScreenHeight = _screenHeight,
            FullScreen = _fullScreen,
            QuickPlayPath = _quickPlayPath,
            QuickPlaySingleplayer = _quickPlaySingleplayer,
            QuickPlayRealms = _quickPlayRealms,
            ServerIp = _serverIp,
            ServerPort = _serverPort,
            JavaPath = _UseCustomJava ? _JavaPath : null
        };
        var args = MLaunchOption.DefaultExtraJvmArguments.ToList();
         args.AddRange(JVMArgs.Select(x => new MArgument(x)));
        opt.ExtraJvmArguments = args.ToArray();
        return opt;
    }

    public static GameSettings FromMLaunchOption(MLaunchOption option)
    {
        var game = new GameSettings
        {
            MaximumRamMb = option.MaximumRamMb,
            MinimumRamMb = option.MinimumRamMb,
            DockName = option.DockName,
            IsDemo = option.IsDemo,
            ScreenWidth = option.ScreenWidth,
            ScreenHeight = option.ScreenHeight,
            FullScreen = option.FullScreen,
            QuickPlayPath = option.QuickPlayPath,
            QuickPlaySingleplayer = option.QuickPlaySingleplayer,
            QuickPlayRealms = option.QuickPlayRealms,
            ServerIp = option.ServerIp,
            ServerPort = option.ServerPort,
            UseCustomJava = !string.IsNullOrWhiteSpace(option.JavaPath),
            JavaPath = option.JavaPath
        };

        game.JVMArgs.Clear();
        foreach (var arg in option.ExtraJvmArguments)
        {
            if(MLaunchOption.DefaultExtraJvmArguments.Contains(arg))
                continue;
            
            game.JVMArgs.AddRange(arg.Values.ToArray());
        }

        return game;
    }

    public GameSettings Clone()
    {
        var clone = new GameSettings
        {
            MaximumRamMb = MaximumRamMb,
            MinimumRamMb = MinimumRamMb,
            DockName = DockName,
            IsDemo = IsDemo,
            ScreenWidth = ScreenWidth,
            ScreenHeight = ScreenHeight,
            FullScreen = FullScreen,
            QuickPlayPath = QuickPlayPath,
            QuickPlaySingleplayer = QuickPlaySingleplayer,
            QuickPlayRealms = QuickPlayRealms,
            ServerIp = ServerIp,
            ServerPort = ServerPort,
            IsAdmin = IsAdmin,
            UseCustomJava = UseCustomJava,
            JavaPath = JavaPath,
            UseSharedAssetsPath = UseSharedAssetsPath,
            UseSharedLibrariesPath = UseSharedLibrariesPath,
            UseSharedRuntimePath = UseSharedRuntimePath,
            UseSharedVersionsPath = UseSharedVersionsPath,
            UseSharedStoreModsPath = UseSharedStoreModsPath,
            UseSharedStoreResourcePacksPath = UseSharedStoreResourcePacksPath,
            UseSharedStoreDataPacksPath = UseSharedStoreDataPacksPath,
            UseSharedStoreShaderPacksPath = UseSharedStoreShaderPacksPath
        };

        foreach (var arg in JVMArgs)
        {
            clone.JVMArgs.Add(arg);
        }

        return clone;
    }

    public void ApplyFrom(GameSettings source)
    {
        MaximumRamMb = source.MaximumRamMb;
        MinimumRamMb = source.MinimumRamMb;
        DockName = source.DockName;
        IsDemo = source.IsDemo;
        ScreenWidth = source.ScreenWidth;
        ScreenHeight = source.ScreenHeight;
        FullScreen = source.FullScreen;
        QuickPlayPath = source.QuickPlayPath;
        QuickPlaySingleplayer = source.QuickPlaySingleplayer;
        QuickPlayRealms = source.QuickPlayRealms;
        ServerIp = source.ServerIp;
        ServerPort = source.ServerPort;
        IsAdmin = source.IsAdmin;
        UseCustomJava = source.UseCustomJava;
        JavaPath = source.JavaPath;
        UseSharedAssetsPath = source.UseSharedAssetsPath;
        UseSharedLibrariesPath = source.UseSharedLibrariesPath;
        UseSharedRuntimePath = source.UseSharedRuntimePath;
        UseSharedVersionsPath = source.UseSharedVersionsPath;
        UseSharedStoreModsPath = source.UseSharedStoreModsPath;
        UseSharedStoreResourcePacksPath = source.UseSharedStoreResourcePacksPath;
        UseSharedStoreDataPacksPath = source.UseSharedStoreDataPacksPath;
        UseSharedStoreShaderPacksPath = source.UseSharedStoreShaderPacksPath;

        JVMArgs.Clear();
        foreach (var arg in source.JVMArgs)
        {
            JVMArgs.Add(arg);
        }
    }

    public static GameSettings Resolve(GameSettings globalSettings, bool usesCustomGameSettings, GameSettings? customGameSettings)
        => usesCustomGameSettings && customGameSettings != null
            ? customGameSettings
            : globalSettings;

    private void OnJvmArgsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => OnPropertyChanged(nameof(JVMArgs));
}
