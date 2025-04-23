using Microsoft.Extensions.Logging;
using CmlLib.Core;
using CmlLib.Core.VersionMetadata;
using CmlLib.Core.Installers;
using Windows.System;
namespace Emerald.CoreX;

public class Game
{
    public ILogger _logger { get; set; }
    public MinecraftLauncher Launcher { get; set; }

    public Game(ILogger logger, MinecraftPath path)
    {
        Launcher = new MinecraftLauncher(path);
        _logger = logger;
    }

    public async Task DownloadVersion(Versions.Version version, bool showFileProgress = false)
    {
        
        var not = Notifications.Noti.fy.Create(
            "Initializing Version",
            $"Initializing {version.Type} version {version.UniqueName}",
            0,
            false
        );

        (string Files,string bytes, double prog) prog = (string.Empty, string.Empty, 0);

        await Launcher.InstallAsync(
            "1.20.4",
            new Progress<InstallerProgressChangedEventArgs>(e =>
            {
                prog.Files = $"{e.Name} ({e.ProgressedTasks}/{e.TotalTasks}). ";

                Notifications.Noti.fy.Update(
                    not.Id,
                    message: prog.Files + prog.bytes,
                    progress: prog.prog
                );
            }),
            new Progress<ByteProgress>(e =>
            {
                prog.bytes = $"{e.ProgressedBytes * Math.Pow(10, -6)} MB/{e.TotalBytes * Math.Pow(10, -6)} MB";

                Notifications.Noti.fy.Update(
                    not.Id,
                    message: prog.Files + prog.bytes,
                    progress: prog.prog
                );
            }),
            not.CancellationToken);
    }
}
