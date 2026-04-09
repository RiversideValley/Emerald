using Microsoft.Extensions.Logging;
using CmlLib.Core;
using CmlLib.Core.VersionMetadata;
using CmlLib.Core.Installers;
using Windows.System;
using CmlLib.Core.VersionLoader;
using CommunityToolkit.Mvvm.DependencyInjection;
using CmlLib.Core.ProcessBuilder;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using Emerald.CoreX.Runtime;
namespace Emerald.CoreX;

public partial class Game : ObservableObject
{
    public static Game FromTuple((string Path, Versions.Version version, Models.GameSettings Options) t)
        => new(new MinecraftPath(t.Path), t.Options, t.version);

    private readonly ILogger _logger;

    private readonly Notifications.INotificationService _notify;

    private MinecraftLauncher Launcher { get; set; }

    public Versions.Version Version { get; set; } = new();
    public MinecraftPath Path { get; set; }

    public Models.GameSettings Options { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanLaunch))]
    [NotifyPropertyChangedFor(nameof(CanStop))]
    [NotifyPropertyChangedFor(nameof(CanModify))]
    [NotifyPropertyChangedFor(nameof(RuntimeStatusText))]
    private GameRunState _runState = GameRunState.Idle;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanLaunch))]
    [NotifyPropertyChangedFor(nameof(CanStop))]
    [NotifyPropertyChangedFor(nameof(CanModify))]
    [NotifyPropertyChangedFor(nameof(RuntimeStatusText))]
    private bool _hasActiveSession;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RuntimeStatusText))]
    private int? _activeProcessId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RuntimeStatusText))]
    private int? _lastExitCode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RuntimeStatusText))]
    private DateTimeOffset? _lastRunEndedAt;

    public bool CanLaunch => !HasActiveSession;

    public bool CanStop => HasActiveSession;

    public bool CanModify => !HasActiveSession;

    public string RuntimeStatusText => RunState switch
    {
        GameRunState.Launching => "Launching",
        GameRunState.Running => ActiveProcessId is int pid ? $"Running • PID {pid}" : "Running",
        GameRunState.Stopping => "Stopping",
        GameRunState.Failed => LastExitCode is int failedCode ? $"Last run failed • exit {failedCode}" : "Last run failed",
        GameRunState.Exited => LastExitCode is int exitCode
            ? $"Last exit • code {exitCode}"
            : LastRunEndedAt is DateTimeOffset endedAt
                ? $"Last run ended • {endedAt.ToLocalTime():g}"
                : "Last run ended",
        _ => "Ready"
    };

    /// <summary>
    /// Represents a Game instance, responsible for managing the installation, configuration,
    /// and launching of Minecraft versions.
    /// </summary>
    public Game(MinecraftPath path, Models.GameSettings options, Versions.Version version)
    {
        _notify = Ioc.Default.GetService<Notifications.INotificationService>();
        _logger = this.Log();
        Launcher = new MinecraftLauncher();
        Path = path;
        Options = options;
        Version = version;
        _logger.LogInformation("Game instance created with path: {Path} and options: {Options}", path, options);
    }

    /// <summary>
    /// Creates a launcher instance configured for online or offline use for the current game path.
    /// </summary>
    public void CreateMCLauncher(bool isOffline)
    {
        _logger.LogDebug("Creating Minecraft launcher. OfflineMode: {IsOffline}.", isOffline);
        var param = MinecraftLauncherParameters.CreateDefault(Path);

        if (isOffline)
        {
            param.VersionLoader = new LocalJsonVersionLoader(Path);
            _logger.LogInformation("Offline mode enabled. Using LocalJsonVersionLoader.");
        }
        else
        {
            _logger.LogInformation("Online mode enabled. Using the default version loader.");
        }

        Launcher = new MinecraftLauncher(param);
    }
    /// <summary>
    /// Installs the specified Minecraft version, including downloading necessary files
    /// and handling both online and offline modes.
    /// </summary>
    /// <param name="isOffline">Indicates whether the installation is performed in offline mode, bypassing online resources.</param>
    /// <param name="showFileProgress">Determines whether detailed file progress information is displayed during the installation process.</param>
    /// <returns>A task that represents the asynchronous installation operation.</returns>
    public async Task InstallVersion(bool isOffline = false, bool showFileProgress = false)
    {
        _logger.LogInformation("Starting InstallVersion with isOffline: {IsOffline}, showFileProgress: {ShowFileProgress}", isOffline, showFileProgress);
        CreateMCLauncher(isOffline);

          var not = _notify.Create(
            "Initializing Version",
            $"Initializing {Version.Type} version {Version.DisplayName}",
            0,
            false,
            true
        );

        _notify.Update(
            not.Id,
            message: $"Initializing {Version.Type} version {Version.DisplayName}",
            isIndeterminate: true);

        try
        {
            string? ver = await Ioc.Default.GetService<Installers.ModLoaderRouter>().RouteAndInitializeAsync(Path, Version);
            _logger.LogInformation("Version initialization completed. Version: {Version}", ver);

            if (ver == null)
            {
                _logger.LogWarning("Version {VersionType} {ModVersion} {BasedOn} not found.", Version.Type, Version.ModVersion, Version.BasedOn);

                _notify.Complete(
                    not.Id,
                    message: $"Version {Version.Type} {Version.ModVersion} {Version.BasedOn} not found. Check your internet connection.",
                    success: false
                );

                return;
            }
            if (isOffline) //checking if verison actually exists
            {
                _logger.LogDebug("Validating version {Version} against the local offline manifest cache.", ver);
                var vers = await Launcher.GetAllVersionsAsync();
                var mver = vers.Where(x => x.Name == ver).First();
                if (mver == null)
                {
                    _logger.LogWarning("Version {Version} not found in offline mode. Can't proceed installation.", ver);
                    throw new NullReferenceException($"Version {ver} not found in offline mode. Can't proceed installation.");
                }
            }

            Version.RealVersion = ver;
            
            if (isOffline) //checking if verison actually exists
            {
                _logger.LogDebug("Rechecking offline version {Version} before install.", ver);
                var vers = await Launcher.GetAllVersionsAsync();
                var mver = vers.Where(x => x.Name == ver).First();
                if (mver == null)
                {
                    _logger.LogWarning("Version {Version} not found in offline mode. Can't proceed installation.", ver);
                    throw new NullReferenceException($"Version {ver} not found in offline mode. Can't proceed installation.");
                }
            }

            (string Files, string bytes, double prog) prog = (string.Empty, string.Empty, 0);

            void UpdateProg()
            {
                string msg = prog.Files;
                if (!string.IsNullOrWhiteSpace(prog.bytes))
                    msg += " | " + prog.bytes;
                _notify.Update(
                    not.Id,
                    message: msg,
                    progress: prog.prog,
                    isIndeterminate: false
                );
            }
            await Launcher.InstallAsync(
                ver,
                showFileProgress ?
                new Progress<InstallerProgressChangedEventArgs>(e =>
                {
                    prog.Files = $"{e.Name} \n({e.ProgressedTasks}/{e.TotalTasks})";
                    
                    prog.prog = Math.Round((double)e.ProgressedTasks / e.TotalTasks * 100, 2); 
                    UpdateProg();
                }) : null,
                new Progress<ByteProgress>(e =>
                {
                    prog.bytes = $"{Math.Round((e.ProgressedBytes * Math.Pow(10, -6)), 0)} MB/{Math.Round((e.TotalBytes * Math.Pow(10, -6)), 0)} MB";
                    UpdateProg();
                }),
                
                not.CancellationToken.Value);

            _logger.LogInformation("Version {VersionType} {VersionDisplayName} installation completed successfully.", Version.Type, Version.DisplayName);
            _notify.Complete(not.Id, true, $"Finished downloading/verifying {Version.Type} version {Version.DisplayName}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred during version installation.");
            _notify.Complete(not.Id, false, "Installation Failed", ex);
        }
    }

    /// <summary>
    /// Builds a process for launching a Minecraft instance using the specified game version.
    /// </summary>
    /// <param name="version">The version of the game to be launched.</param>
    /// <returns>A Task that represents the process used to launch the Minecraft instance.</returns>
    public async Task<Process> BuildProcess(string version, CmlLib.Core.Auth.MSession session)
    {
        _logger.LogInformation("Building process for version: {Version}", version);
        var launchOpt = Options.ToMLaunchOption();
        launchOpt.Session = session;
        _logger.LogDebug("Preparing launch options for {Version}. FullScreen: {FullScreen}. DockName: {DockName}.", version, Options.FullScreen, Options.DockName);
        return await Launcher.BuildProcessAsync(
            version, launchOpt);
    }
}
