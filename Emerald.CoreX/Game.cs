using Microsoft.Extensions.Logging;
using CmlLib.Core;
using CmlLib.Core.VersionMetadata;
using CmlLib.Core.Installers;
using Windows.System;
using CmlLib.Core.VersionLoader;
using CommunityToolkit.Mvvm.DependencyInjection;
using CmlLib.Core.ProcessBuilder;
using System.Diagnostics;
namespace Emerald.CoreX;

public class Game
{
    private readonly ILogger _logger;

    private readonly Notifications.INotificationService _notify;

    private MinecraftLauncher Launcher { get; set; }

    public Versions.Version Version { get; set; } = new();
    public MinecraftPath Path { get; set; }


    public Models.Game Options { get; set; }

    public Game(MinecraftPath path, Models.Game options)
    {

        _notify = Ioc.Default.GetService<Notifications.INotificationService>();
        _logger = this.Log();
        Launcher = new MinecraftLauncher();
        Path = path;
        Options = options;
    }

    public async Task InstallVersion(bool isOffline = false, bool showFileProgress = false)
    {
        var param = MinecraftLauncherParameters.CreateDefault(Path);

        if (isOffline)
            param.VersionLoader = new LocalJsonVersionLoader(Path);

        Launcher = new MinecraftLauncher(param);


        var not = _notify.Create(
            "Initializing Version",
            $"Initializing {Version.Type} version {Version.DisplayName}",
            0,
            false
        );

        _notify.Update(
            not.Id,
            message: $"Initializing {Version.Type} version {Version.DisplayName}",
            isIndeterminate: true);

        try
        {
            string ver = await Ioc.Default.GetService<Installers.ModLoaderRouter>().RouteAndInitializeAsync(Path, Version);

            (string Files, string bytes, double prog) prog = (string.Empty, string.Empty, 0);

            await Launcher.InstallAsync(
                ver,
                new Progress<InstallerProgressChangedEventArgs>(e =>
                {
                    prog.Files = $"{e.Name} ({e.ProgressedTasks}/{e.TotalTasks}). ";

                    _notify.Update(
                        not.Id,
                        message: prog.Files + prog.bytes,
                        progress: prog.prog,
                        isIndeterminate: false
                    );
                }),
                new Progress<ByteProgress>(e =>
                {
                    prog.bytes = $"{e.ProgressedBytes * Math.Pow(10, -6)} MB/{e.TotalBytes * Math.Pow(10, -6)} MB";

                    _notify.Update(
                        not.Id,
                        message: prog.Files + prog.bytes,
                        progress: prog.prog,
                        isIndeterminate: false
                    );
                }),
                not.CancellationToken);


            _notify.Complete(not.Id, true, $"Finished downloading {Version.Type} version {Version.DisplayName}");
        }
        catch
        {
            
        }
    }

    public async Task<Process> BuildProcess(string version)
    {
        return await Launcher.BuildProcessAsync(
            version, Options.ToMLaunchOption());

    }
}
