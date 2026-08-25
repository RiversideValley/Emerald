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
    private const int MaxVisibleIntegrityIssues = 20;
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
    [NotifyPropertyChangedFor(nameof(CanModify))]
    private InstanceInstallationState _installationState = InstanceInstallationState.Unknown;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InstallationStatusText))]
    private DateTimeOffset? _lastVerifiedAt;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasIntegrityIssues))]
    [NotifyPropertyChangedFor(nameof(IntegrityIssueCount))]
    private IReadOnlyList<IntegrityIssue> _integrityIssues = [];

    [ObservableProperty]
    private IReadOnlyList<IntegrityIssue> _visibleIntegrityIssues = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRemainingIntegrityIssues))]
    private int _remainingIntegrityIssueCount;

    public bool HasIntegrityIssues => IntegrityIssues.Count > 0;

    public int IntegrityIssueCount => IntegrityIssues.Count;

    public bool HasRemainingIntegrityIssues => RemainingIntegrityIssueCount > 0;

    public bool CanLaunch => !HasActiveSession && InstallationState is not InstanceInstallationState.Installing and not InstanceInstallationState.Verifying;

    public bool CanStop => HasActiveSession;

    public bool CanModify => !HasActiveSession
        && InstallationState is not InstanceInstallationState.Installing
        and not InstanceInstallationState.Verifying;

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

    partial void OnIntegrityIssuesChanged(IReadOnlyList<IntegrityIssue> value)
    {
        // Keep the full report for APIs and logs, while ensuring a damaged asset tree cannot
        // create thousands of XAML elements on the Games page.
        VisibleIntegrityIssues = value
            .OrderByDescending(issue => issue.Severity == IntegritySeverity.Critical)
            .Take(MaxVisibleIntegrityIssues)
            .ToArray();
        RemainingIntegrityIssueCount = Math.Max(0, value.Count - VisibleIntegrityIssues.Count);
    }

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

    public async Task InstallVersion(bool isOffline = false, bool showFileProgress = false, CancellationToken cancellationToken = default)
    {
        try
        {
            await InstallVersionOrThrow(isOffline, showFileProgress, cancellationToken: cancellationToken);
        }
        catch
        {
        }
    }

    public async Task InstallVersionOrThrow(
        bool isOffline = false,
        bool showFileProgress = false,
        IProgress<CmlLib.Core.Installers.InstallerProgressChangedEventArgs>? fileProgress = null,
        IProgress<CmlLib.Core.ByteProgress>? byteProgress = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting InstallVersion with isOffline: {IsOffline}, showFileProgress: {ShowFileProgress}", isOffline, showFileProgress);
        CreateMCLauncher(isOffline);

        var modLoaderRouter = Ioc.Default.GetService<Installers.ModLoaderRouter>()
            ?? throw new InvalidOperationException("Mod loader router service is not available.");
        var resolution = await modLoaderRouter.ResolveAsync(
            Path,
            Version,
            isOffline ? Installers.ModLoaderResolutionMode.LocalOnly : Installers.ModLoaderResolutionMode.Online,
            Version.RealVersion,
            cancellationToken);
        string? ver = resolution.ResolvedVersion;
        _logger.LogInformation("Version initialization completed. Version: {Version}", ver);

        if (ver == null)
        {
            _logger.LogWarning("Version {VersionType} {ModVersion} {BasedOn} not found.", Version.Type, Version.ModVersion, Version.BasedOn);
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
        await Launcher.InstallAsync(
            ver,
            showFileProgress ? fileProgress : null,
            byteProgress,
            cancellationToken);

        _logger.LogInformation("Version {VersionType} {VersionDisplayName} installation completed successfully.", Version.Type, Version.DisplayName);
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
