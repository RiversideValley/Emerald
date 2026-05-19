using System.Diagnostics;
using System.ComponentModel;
using CmlLib.Core;
using CmlLib.Core.Installers;
using CmlLib.Core.ProcessBuilder;
using CmlLib.Core.VersionLoader;
using CmlLib.Core.VersionMetadata;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using Emerald.CoreX.Runtime;
using Emerald.CoreX.Services;
using Emerald.CoreX.Services.Auth;
using Microsoft.Extensions.Logging;

namespace Emerald.CoreX;

public partial class Game : ObservableObject
{
    private readonly ILogger _logger;
    private readonly Notifications.INotificationService _notify;
    private readonly IGlobalGameSettingsService _globalGameSettingsService;
    private readonly string _instanceBasePath;
    private readonly string? _sharedMinecraftBasePath;
    private bool _launcherOfflineMode;
    private Models.GameSettings? _subscribedCustomGameSettings;

    private MinecraftLauncher Launcher { get; set; }

    public Versions.Version Version { get; set; } = new();
    public MinecraftPath Path { get; private set; }

    public string? SharedMinecraftBasePath => _sharedMinecraftBasePath;

    [ObservableProperty]
    private bool _usesCustomGameSettings;

    [ObservableProperty]
    private Models.GameSettings? _customGameSettings;

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

    public Models.GameSettings EffectiveSettings
        => Models.GameSettings.Resolve(_globalGameSettingsService.Settings, UsesCustomGameSettings, CustomGameSettings);

    public Models.GameSettings Options => EffectiveSettings;

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

    public Game(
        MinecraftPath path,
        Versions.Version version,
        bool usesCustomGameSettings = false,
        Models.GameSettings? customGameSettings = null,
        string? sharedMinecraftBasePath = null,
        IGlobalGameSettingsService? globalGameSettingsService = null)
    {
        _notify = Ioc.Default.GetService<Notifications.INotificationService>()
            ?? throw new InvalidOperationException("Notification service is required before creating games.");
        _logger = this.Log();
        _globalGameSettingsService = globalGameSettingsService
            ?? Ioc.Default.GetService<IGlobalGameSettingsService>()
            ?? throw new InvalidOperationException("Global game settings service is required before creating games.");
        _instanceBasePath = path.BasePath;
        _sharedMinecraftBasePath = sharedMinecraftBasePath;

        Launcher = new MinecraftLauncher();
        Version = version;
        _usesCustomGameSettings = usesCustomGameSettings;
        _customGameSettings = usesCustomGameSettings
            ? customGameSettings?.Clone() ?? _globalGameSettingsService.CloneCurrent()
            : null;
        AttachCustomGameSettings(_customGameSettings);
        Path = CreateConfiguredMinecraftPath();

        _globalGameSettingsService.Settings.PropertyChanged += GlobalSettings_PropertyChanged;

        _logger.LogInformation("Game instance created with path: {Path}. UsesCustomGameSettings: {UsesCustomGameSettings}", path, usesCustomGameSettings);
    }

    public Models.GameSettings GetEditableSettings()
        => UsesCustomGameSettings
            ? CustomGameSettings ??= _globalGameSettingsService.CloneCurrent()
            : _globalGameSettingsService.Settings;

    public void ResetCustomGameSettings()
    {
        if (!UsesCustomGameSettings)
        {
            return;
        }

        CustomGameSettings = _globalGameSettingsService.CloneCurrent();
        NotifyEffectiveSettingsChanged();
    }

    public void CreateMCLauncher(bool isOffline)
    {
        _launcherOfflineMode = isOffline;
        RefreshMinecraftPath();
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

    public async Task InstallVersion(bool isOffline = false, bool showFileProgress = false)
    {
        try
        {
            await InstallVersionOrThrow(isOffline, showFileProgress);
        }
        catch
        {
        }
    }

    public async Task InstallVersionOrThrow(bool isOffline = false, bool showFileProgress = false)
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

                throw new InvalidOperationException($"Version {Version.Type} {Version.ModVersion} {Version.BasedOn} not found.");
            }
            if (isOffline)
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

            if (isOffline)
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

            (string Files, string bytes, double prog, double? progbytes) prog = (string.Empty, string.Empty, 0, null);

            void UpdateProg()
            {
                string msg = prog.Files;
                if (!string.IsNullOrWhiteSpace(prog.bytes))
                {
                    msg += " | " + prog.bytes;
                }

                var realprog = prog.progbytes ?? prog.prog;

                _notify.Update(
                    not.Id,
                    message: msg,
                    progress: realprog,
                    isIndeterminate: false
                );
            }

            await Launcher.InstallAsync(
                ver,
                showFileProgress
                    ? new Progress<InstallerProgressChangedEventArgs>(e =>
                    {
                        prog.Files = $"{e.Name} \n({e.ProgressedTasks}/{e.TotalTasks})";
                        prog.prog = Math.Round((double)e.ProgressedTasks / e.TotalTasks * 100, 2);
                        UpdateProg();
                    })
                    : null,
                new Progress<ByteProgress>(e =>
                {
                    prog.bytes = $"{Math.Round((e.ProgressedBytes * Math.Pow(10, -6)), 0)} MB/{Math.Round((e.TotalBytes * Math.Pow(10, -6)), 0)} MB";
                    prog.progbytes = Math.Round((double)e.ProgressedBytes / e.TotalBytes * 100, 2);
                    
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
            throw;
        }
    }

    public async Task<Process> BuildProcess(
        string version,
        CmlLib.Core.Auth.MSession session,
        AccountRuntimeAuthOptions? runtimeAuthOptions = null)
    {
        _logger.LogInformation("Building process for version: {Version}", version);
        CreateMCLauncher(_launcherOfflineMode);
        var launchOpt = EffectiveSettings.ToMLaunchOption();
        launchOpt.Session = session;

        if (runtimeAuthOptions?.ExtraJvmArguments.Count > 0)
        {
            launchOpt.ExtraJvmArguments = launchOpt.ExtraJvmArguments
                .Concat(runtimeAuthOptions.ExtraJvmArguments)
                .ToArray();
        }

        if (EffectiveSettings.UseCustomJava)
        {
            var javaRuntimeCatalog = Ioc.Default.GetService<IJavaRuntimeCatalogService>()
                ?? throw new InvalidOperationException("Java runtime catalog service is not available.");

            var validation = await javaRuntimeCatalog.ValidateAsync(EffectiveSettings.JavaPath);
            if (!validation.IsValid || string.IsNullOrWhiteSpace(validation.NormalizedPath))
            {
                throw new InvalidOperationException(validation.ErrorMessage ?? "The selected Java runtime could not be used.");
            }

            launchOpt.JavaPath = validation.NormalizedPath;

            _logger.LogDebug(
                "Using custom Java runtime for {Version}. JavaPath: {JavaPath}. VersionInfo: {VersionInfo}.",
                version,
                validation.NormalizedPath,
                validation.Version);
        }

        _logger.LogDebug("Preparing launch options for {Version}. FullScreen: {FullScreen}. DockName: {DockName}.", version, EffectiveSettings.FullScreen, EffectiveSettings.DockName);
        return await Launcher.BuildProcessAsync(version, launchOpt);
    }

    partial void OnUsesCustomGameSettingsChanged(bool value)
    {
        if (value)
        {
            CustomGameSettings ??= _globalGameSettingsService.CloneCurrent();
        }
        else
        {
            CustomGameSettings = null;
        }

        NotifyEffectiveSettingsChanged();
    }

    partial void OnCustomGameSettingsChanged(Models.GameSettings? value)
    {
        AttachCustomGameSettings(value);
        NotifyEffectiveSettingsChanged();
    }

    private void NotifyEffectiveSettingsChanged()
    {
        RefreshMinecraftPath();
        OnPropertyChanged(nameof(EffectiveSettings));
        OnPropertyChanged(nameof(Options));
    }

    private void GlobalSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!UsesCustomGameSettings)
        {
            NotifyEffectiveSettingsChanged();
        }
    }

    private void CustomGameSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (UsesCustomGameSettings)
        {
            NotifyEffectiveSettingsChanged();
        }
    }

    private void AttachCustomGameSettings(Models.GameSettings? settings)
    {
        if (_subscribedCustomGameSettings != null)
        {
            _subscribedCustomGameSettings.PropertyChanged -= CustomGameSettings_PropertyChanged;
        }

        _subscribedCustomGameSettings = settings;

        if (settings != null)
        {
            settings.PropertyChanged -= CustomGameSettings_PropertyChanged;
            settings.PropertyChanged += CustomGameSettings_PropertyChanged;
        }
    }

    private MinecraftPath CreateConfiguredMinecraftPath()
    {
        var effectiveSettings = EffectiveSettings;
        if (string.IsNullOrWhiteSpace(_sharedMinecraftBasePath) || !effectiveSettings.UsesSharedMinecraftFolders)
        {
            return new MinecraftPath(_instanceBasePath);
        }

        return new SplitMinecraftPath(
            _sharedMinecraftBasePath,
            _instanceBasePath,
            effectiveSettings.UseSharedAssetsPath,
            effectiveSettings.UseSharedLibrariesPath,
            effectiveSettings.UseSharedRuntimePath,
            effectiveSettings.UseSharedVersionsPath);
    }

    private void RefreshMinecraftPath()
    {
        var nextPath = CreateConfiguredMinecraftPath();
        if (MinecraftPathsEqual(Path, nextPath))
        {
            return;
        }

        Path = nextPath;
        OnPropertyChanged(nameof(Path));
    }

    private static bool MinecraftPathsEqual(MinecraftPath? left, MinecraftPath? right)
    {
        if (left == null || right == null)
        {
            return left == right;
        }

        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return string.Equals(left.BasePath, right.BasePath, comparison)
               && string.Equals(left.Assets, right.Assets, comparison)
               && string.Equals(left.Resource, right.Resource, comparison)
               && string.Equals(left.Library, right.Library, comparison)
               && string.Equals(left.Runtime, right.Runtime, comparison)
               && string.Equals(left.Versions, right.Versions, comparison);
    }
}
