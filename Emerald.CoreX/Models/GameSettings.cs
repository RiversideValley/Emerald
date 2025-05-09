using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CmlLib.Core.FileExtractors;
using CmlLib.Core.ProcessBuilder;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Emerald.CoreX.Models;
public partial class GameSettings : ObservableObject
{
    [ObservableProperty]
    private int _maximumRamMb;

    [ObservableProperty]
    private int _minimumRamMb;

    [ObservableProperty]
    private string? _dockName = "Minecraft";

    [ObservableProperty]
    private bool _isDemo;

    [ObservableProperty]
    private int _screenWidth;

    [ObservableProperty]
    private int _screenHeight;

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

    public readonly ObservableCollection<string> JVMArgs = new();


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
            ServerPort = _serverPort
        };
        var args = opt.ExtraJvmArguments.ToList();
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
            ServerPort = option.ServerPort
        };

        game.JVMArgs.Clear();
        foreach (var arg in option.ExtraJvmArguments)
        {
            game.JVMArgs.Add(arg.ToString());
        }

        return game;
    }
}
