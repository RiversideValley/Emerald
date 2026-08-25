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
using Emerald.CoreX.Installation;
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
    public bool IsLauncherOfflineMode => _launcherOfflineMode;

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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InstallationStatusText))]
    [NotifyPropertyChangedFor(nameof(CanLaunch))]
    private InstanceInstallationState _installationState = InstanceInstallationState.Unknown;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InstallationStatusText))]
    private DateTimeOffset? _lastVerifiedAt;

    [ObservableProperty]
    private IReadOnlyList<IntegrityIssue> _integrityIssues = [];

    public bool CanLaunch => !HasActiveSession && InstallationState is not InstanceInstallationState.Installing and not InstanceInstallationState.Verifying;

    public bool CanStop => HasActiveSession;

    public bool CanModify => !HasActiveSession;

    public string InstallationStatusText => InstallationState switch
    {
        InstanceInstallationState.Ready => LastVerifiedAt is { } at ? $"Ready • verified {at.ToLocalTime():g}" : "Ready",
        InstanceInstallationState.ReadyWithWarnings => "Ready with warnings",
        InstanceInstallationState.NeedsRepair => "Needs repair",
        InstanceInstallationState.NotInstalled => "Not installed",
        InstanceInstallationState.Installing => "Installing",
        InstanceInstallationState.Verifying => "Verifying",
        InstanceInstallationState.Failed => "Installation failed",
        _ => "Installation unknown"
    };

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
        var sharedHttpClient = Ioc.Default.GetService<HttpClient>();
        if (sharedHttpClient != null) param.HttpClient = sharedHttpClient;

        if (isOffline)
        {
            param.VersionLoader = new LocalJsonVersionLoader(Path);
            _logger.LogInformation("Offline mode enabled. Using LocalJsonVersionLoader.");
        }
        else
        {
            var verifiedInstaller = Ioc.Default.GetService<Installation.VerifiedGameInstaller>();
            if (verifiedInstaller != null) param.GameInstaller = verifiedInstaller;
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
            var modLoaderRouter = Ioc.Default.GetService<Installers.ModLoaderRouter>()
                ?? throw new InvalidOperationException("Mod loader router service is not available.");
            var resolution = await modLoaderRouter.ResolveAsync(
                Path,
                Version,
                isOffline ? Installers.ModLoaderResolutionMode.LocalOnly : Installers.ModLoaderResolutionMode.Online,
                Version.RealVersion,
                not.CancellationToken.Value);
            string? ver = resolution.ResolvedVersion;
            _logger.LogInformation("Version initialization completed. Version: {Version}", ver);

            if (ver == null)
            {
                _logger.LogWarning("Version {VersionType} {ModVersion} {BasedOn} not found.", Version.Type, Version.ModVersion, Version.BasedOn);

                _notify.Complete(
                    not.Id,
                    message: $"Version {Version.Type} {Version.ModVersion} {Version.BasedOn} not found. Check your internet connection.",
                    success: false
                );

                throw new InvalidOperationException(resolution.Message ?? $"Version {Version.Type} {Version.ModVersion} {Version.BasedOn} not found.");
            }
            if (isOffline)
            {
                _logger.LogDebug("Validating version {Version} against the local offline manifest cache.", ver);
                if (!await IsVersionAvailableLocallyAsync(ver))
                {
                    _logger.LogWarning("Version {Version} not found in offline mode. Can't proceed installation.", ver);
                    throw new InvalidOperationException($"Version {ver} not found in offline mode. Can't proceed installation.");
                }
            }

            Version.RealVersion = ver;

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

    private async Task<bool> IsVersionAvailableLocallyAsync(string version)
    {
        var versions = await Launcher.GetAllVersionsAsync();
        return versions.Any(candidate => string.Equals(candidate.Name, version, StringComparison.Ordinal));
    }

    public async Task<Process> BuildProcess(
        string version,
        CmlLib.Core.Auth.MSession session,
        AccountRuntimeAuthOptions? runtimeAuthOptions = null)
    {
        _logger.LogInformation("Building process for version: {Version}", version);
        CreateMCLauncher(true);
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
