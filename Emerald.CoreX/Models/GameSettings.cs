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
    private bool _HashCheck = true;

    [ObservableProperty]
    private bool _AssetsCheck = true;

    [ObservableProperty]
    private bool _IsAdmin;

    [ObservableProperty]
    private bool _UseCustomJava;

    [ObservableProperty]
    private string? _JavaPath;
    
    public ObservableCollection<string> JVMArgs { get; set; } = new();

    [JsonIgnore]
    public string ScreenSizeStatus =>
        FullScreen ? "FullScreen".Localize() : ((ScreenWidth > 0 && ScreenHeight > 0) ? $"{ScreenWidth} × {ScreenHeight}" : "Default".Localize());

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
            HashCheck = HashCheck,
            AssetsCheck = AssetsCheck,
            IsAdmin = IsAdmin,
            UseCustomJava = UseCustomJava,
            JavaPath = JavaPath
        };

        foreach (var arg in JVMArgs)
        {
            clone.JVMArgs.Add(arg);
        }

        return clone;
    }

    public static GameSettings Resolve(GameSettings globalSettings, bool usesCustomGameSettings, GameSettings? customGameSettings)
        => usesCustomGameSettings && customGameSettings != null
            ? customGameSettings
            : globalSettings;

    private void OnJvmArgsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => OnPropertyChanged(nameof(JVMArgs));
}
