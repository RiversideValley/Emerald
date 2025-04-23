using Microsoft.Extensions.Logging;
using CmlLib.Core;
using CmlLib.Core.VersionMetadata;
using CmlLib.Core.Installers;
using Windows.System;
namespace Emerald.CoreX;

public class Game
{
    private readonly ILogger _logger;
    private readonly Notifications.INotificationService _notify;
    public MinecraftLauncher Launcher { get; set; }

    public Game(ILogger logger, Notifications.INotificationService notificationService , MinecraftPath path)
    {
        _notify = notificationService;
        Launcher = new MinecraftLauncher(path);
        _logger = logger;
    }

    public async Task DownloadVersion(Versions.Version version, bool showFileProgress = false)
    {
        
        var not = _notify.Create(
            "Initializing Version",
            $"Initializing {version.Type} version {version.DisplayName}",
            0,
            false
        );

        (string Files,string bytes, double prog) prog = (string.Empty, string.Empty, 0);

        await Launcher.InstallAsync(
            "1.20.4",
            new Progress<InstallerProgressChangedEventArgs>(e =>
            {
                prog.Files = $"{e.Name} ({e.ProgressedTasks}/{e.TotalTasks}). ";

                _notify.Update(
                    not.Id,
                    message: prog.Files + prog.bytes,
                    progress: prog.prog
                );
            }),
            new Progress<ByteProgress>(e =>
            {
                prog.bytes = $"{e.ProgressedBytes * Math.Pow(10, -6)} MB/{e.TotalBytes * Math.Pow(10, -6)} MB";

                _notify.Update(
                    not.Id,
                    message: prog.Files + prog.bytes,
                    progress: prog.prog
                );
            }),
            not.CancellationToken);
    }
}
