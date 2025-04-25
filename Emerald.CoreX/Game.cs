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

    public Models.GameSettings Options { get; set; }

    public Game(MinecraftPath path, Models.GameSettings options)
    {
        _notify = Ioc.Default.GetService<Notifications.INotificationService>();
        _logger = this.Log();
        Launcher = new MinecraftLauncher();
        Path = path;
        Options = options;
        _logger.LogInformation("Game instance created with path: {Path} and options: {Options}", path, options);
    }

    public async Task InstallVersion(bool isOffline = false, bool showFileProgress = false)
    {
        _logger.LogInformation("Starting InstallVersion with isOffline: {IsOffline}, showFileProgress: {ShowFileProgress}", isOffline, showFileProgress);
        var param = MinecraftLauncherParameters.CreateDefault(Path);

        if (isOffline)
        {
            param.VersionLoader = new LocalJsonVersionLoader(Path);
            _logger.LogInformation("Offline mode enabled. Using LocalJsonVersionLoader.");
        }

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
            _logger.LogInformation("Version initialization completed. Version: {Version}", ver);

            if (ver == null)
            {
                var vers = await Launcher.GetAllVersionsAsync();
                ver = vers.First(x => x.Name.ToLower().Contains(Version.BasedOn.ToLower())).Name;

                _logger.LogWarning("Version {VersionType} {ModVersion} {BasedOn} not found. Using {FallbackVersion} instead.", Version.Type, Version.ModVersion, Version.BasedOn, ver);

                _notify.Update(
                    not.Id,
                    message: $"Version {Version.Type} {Version.ModVersion} {Version.BasedOn} not found. Using {ver} instead.",
                    isIndeterminate: false
                );

                return;
            }

            (string Files, string bytes, double prog) prog = (string.Empty, string.Empty, 0);

            await Launcher.InstallAsync(
                ver,
                showFileProgress ?
                new Progress<InstallerProgressChangedEventArgs>(e =>
                {
                    prog.Files = $"{e.Name} ({e.ProgressedTasks}/{e.TotalTasks}). ";

                    _notify.Update(
                        not.Id,
                        message: prog.Files + prog.bytes,
                        progress: prog.prog,
                        isIndeterminate: false
                    );
                }) : null,
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

            _logger.LogInformation("Version {VersionType} {VersionDisplayName} installation completed successfully.", Version.Type, Version.DisplayName);
            _notify.Complete(not.Id, true, $"Finished downloading/verifying {Version.Type} version {Version.DisplayName}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred during version installation.");
        }
    }

    public async Task<Process> BuildProcess(string version)
    {
        _logger.LogInformation("Building process for version: {Version}", version);
        return await Launcher.BuildProcessAsync(
            version, Options.ToMLaunchOption());
    }
}
