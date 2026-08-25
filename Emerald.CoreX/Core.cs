using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using CmlLib.Core;
using CmlLib.Core.Utils;
using CmlLib.Core.VersionMetadata;
using CommunityToolkit.Mvvm.ComponentModel;
using Emerald.CoreX.Helpers;
using Emerald.CoreX.Notifications;
using Emerald.CoreX.Runtime;
using Emerald.CoreX.Installation;
using Emerald.CoreX.Services;
using Emerald.CoreX.Store;
using Emerald.Services;
using Microsoft.Extensions.Logging;
namespace Emerald.CoreX;

public sealed class SavedGame
{
    public string Path { get; set; } = string.Empty;

    public Versions.Version Version { get; set; } = new();

    public bool UsesCustomGameSettings { get; set; }

    public Models.GameSettings? CustomGameSettings { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Models.GameSettings? GameOptions { get; set; }

    public Game ToGame(IGlobalGameSettingsService globalGameSettingsService, string? sharedMinecraftBasePath = null)
        => new(
            new MinecraftPath(Path),
            Version,
            UsesCustomGameSettings || GameOptions != null,
            CustomGameSettings ?? GameOptions,
            sharedMinecraftBasePath,
            globalGameSettingsService);

    public static SavedGame FromGame(Game game)
        => new()
        {
            Path = game.Path.BasePath,
            Version = game.Version,
            UsesCustomGameSettings = game.UsesCustomGameSettings,
            CustomGameSettings = game.UsesCustomGameSettings
                ? game.CustomGameSettings?.Clone()
                : null
        };
}

public sealed class SavedGameCollection
{
    public string BasePath { get; set; } = string.Empty;

    public SavedGame[] Games { get; set; } = [];
}

public partial class Core(
    ILogger<Core> _logger,
    INotificationService _notify,
    IBaseSettingsService settingsService,
    IMinecraftBaseSettingsService minecraftBaseSettingsService,
    IGameRuntimeService runtimeService,
    IGlobalGameSettingsService globalGameSettingsService,
    IStoreInstallRecordRepository storeInstallRecordRepository,
    IStoreSharedContentSettingsService storeSharedContentSettingsService,
    IInstanceInstallationService? installationService = null,
    INetworkCapabilityService? networkCapabilityService = null,
    HttpClient? httpClient = null,
    IDownloadActivityService? downloadActivity = null,
    IUiDispatcher? uiDispatcher = null) : ObservableObject
{
    private bool _networkSubscribed;
    private bool _downloadActivitySubscribed;
    private readonly object _refreshGate = new();
    private Task? _refreshTask;
    private Task? _localInitializationTask;
    private bool _catalogRefreshPending;
    private readonly object _auditGate = new();
    private readonly Queue<InstallationAuditWorkItem> _pendingAudits = new();
    private readonly Dictionary<Game, InstallationAuditWorkItem> _auditsByGame = new();
    private readonly HashSet<InstallationAuditWorkItem> _activeAudits = new();
    private bool _auditWorkerRunning;
    private int _gamesGeneration;

    /// <summary>
    /// Tracks a local audit without relying on the UI-bound <see cref="Games"/> collection.
    /// The cancellation source is owned by the queue and is disposed once this item is discarded.
    /// </summary>
    private sealed class InstallationAuditWorkItem(Game game, int generation)
    {
        public Game Game { get; } = game;
        public int Generation { get; } = generation;
        public IntegrityCheckLevel CheckLevel { get; set; } = IntegrityCheckLevel.Quick;
        public CancellationTokenSource Cancellation { get; } = new();
    }
    public const string GamesFolderName = "Instances";
    public MinecraftLauncher Launcher { get; set; }
    public IGlobalGameSettingsService GlobalGameSettingsService => globalGameSettingsService;

    public event EventHandler? VersionsRefreshed;

    public bool IsRunning { get; set; } = false;
    public MinecraftPath? BasePath { get; private set; } = null;
    [ObservableProperty]
    private bool _isOfflineMode = false;

    public readonly ObservableCollection<Versions.Version> VanillaVersions = new();

    public readonly ObservableCollection<Game> Games = new();

    [ObservableProperty]
    private bool _initialized = false;

    [ObservableProperty]
    private bool _isRefreshing = false;

    public Models.GameSettings GameOptions => globalGameSettingsService.Settings;
    private IUiDispatcher UiDispatcher => uiDispatcher ?? new InlineUiDispatcher();

    public void LoadGames()
    {
        if (BasePath == null)
        {
            _logger.LogWarning("Cannot load games, BasePath is not set");
            throw new InvalidOperationException("Cannot load games, BasePath is not set");
        }

        var gamesFolder = Path.Combine(BasePath.BasePath, GamesFolderName);
        if (!Path.Exists(gamesFolder))
        {
            _logger.LogInformation("Games folder does not exist, creating...");
            Directory.CreateDirectory(gamesFolder);
        }

        PrepareBaseScopedServices();
        var savedGames = LoadOrMigrateSavedGames();
        var generation = Interlocked.Increment(ref _gamesGeneration);
        lock (_auditGate)
        {
            CancelAllInstallationAuditsLocked();
        }

        Games.Clear();
        if (savedGames.Length == 0)
        {
            _logger.LogInformation("Saved games paths does not contain any games");
            return;
        }

        foreach (var sg in savedGames)
        {
            try
            {
                var game = sg.ToGame(globalGameSettingsService, BasePath.BasePath);
                Games.Add(game);
                QueueInstallationAudit(game, generation);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to load game from {dir}: {ex}", sg.Path, ex.Message);
                _notify.Error("FailedToLoadGame", $"Failed to load game from {sg.Path}", ex: ex);
            }
        }

        _logger.LogInformation("Loaded {count} games from", Games.Count);
    }
    public void SaveGames()
    {
        _logger.LogInformation("Saving {count} games", Games.Count);

        var toSave = Games.Select(x =>
            SavedGame.FromGame(x)
        ).ToArray();

        try
        {
            PrepareBaseScopedServices();
            minecraftBaseSettingsService.Set(SettingsKeys.SavedGames, toSave);

            _logger.LogInformation("Saved {count} games", toSave.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save games");
            throw;
        }
    }

    private void PrepareBaseScopedServices()
    {
        if (BasePath == null)
        {
            throw new InvalidOperationException("Cannot initialize base-scoped settings, BasePath is not set");
        }

        minecraftBaseSettingsService.UseBasePath(BasePath.BasePath);
        globalGameSettingsService.LoadForBasePath(BasePath.BasePath);
        storeInstallRecordRepository.LoadForBasePath(BasePath.BasePath);
        storeSharedContentSettingsService.LoadForBasePath(BasePath.BasePath);
    }

    private SavedGame[] LoadOrMigrateSavedGames()
    {
        if (BasePath == null)
        {
            throw new InvalidOperationException("Cannot load games, BasePath is not set");
        }

        if (minecraftBaseSettingsService.Exists(SettingsKeys.SavedGames))
        {
            return minecraftBaseSettingsService.Get<SavedGame[]>(SettingsKeys.SavedGames, []);
        }

        if (settingsService.Exists(SettingsKeys.SavedGames))
        {
            var savedCollections = settingsService.Get<SavedGameCollection[]>(SettingsKeys.SavedGames, []);
            var collection = savedCollections.FirstOrDefault(existing =>
                PathsEqual(existing.BasePath, BasePath.BasePath));

            if (collection != null)
            {
                minecraftBaseSettingsService.Set(SettingsKeys.SavedGames, collection.Games);

                var remaining = savedCollections
                    .Where(existing => !PathsEqual(existing.BasePath, BasePath.BasePath))
                    .ToArray();
                if (remaining.Length == 0)
                {
                    settingsService.Delete(SettingsKeys.SavedGames);
                }
                else
                {
                    settingsService.Set(SettingsKeys.SavedGames, remaining);
                }

                return collection.Games;
            }
        }

        return minecraftBaseSettingsService.Get<SavedGame[]>(SettingsKeys.SavedGames, []);
    }

    /// <summary>Loads local settings and saved games without performing network requests.</summary>
    public Task InitializeLocalAsync(MinecraftPath? basePath = null)
    {
        lock (_refreshGate)
        {
            if (_localInitializationTask is { IsCompleted: false }) return _localInitializationTask;
            _localInitializationTask = InitializeLocalCoreAsync(basePath);
            return _localInitializationTask;
        }
    }

    private Task InitializeLocalCoreAsync(MinecraftPath? basePath)
    {
        SubscribeToNetworkState();
        SubscribeToDownloadActivity();
        if (!Initialized && basePath == null)
            throw new InvalidOperationException("Minecraft Path must be set on first initialize");

        if (basePath != null && (!Initialized || !PathsEqual(basePath.BasePath, BasePath?.BasePath)))
        {
            if (downloadActivity?.Snapshot.ActiveDownloads > 0)
                throw new InvalidOperationException("Minecraft path cannot be changed while downloads are active.");

            BasePath = basePath;
            Launcher = CreateCatalogLauncher(basePath);
            PrepareBaseScopedServices();
            LoadGames();
        }

        Initialized = true;
        foreach (var game in Games) game.CreateMCLauncher(IsOfflineMode);
        return Task.CompletedTask;
    }

    /// <summary>Refreshes remote metadata only; local games and cached versions survive failures.</summary>
    public Task RefreshVersionCatalogAsync(CancellationToken cancellationToken = default)
    {
        lock (_refreshGate)
        {
            if (_refreshTask is { IsCompleted: false }) return _refreshTask;
            _refreshTask = RefreshVersionCatalogCoreAsync(cancellationToken);
            return _refreshTask;
        }
    }

    [Obsolete("Use InitializeLocalAsync followed by RefreshVersionCatalogAsync.")]
    public async Task InitializeAndRefresh(MinecraftPath? basePath = null)
    {
        await InitializeLocalAsync(basePath);
        await RefreshVersionCatalogAsync();
    }

    private async Task RefreshVersionCatalogCoreAsync(CancellationToken cancellationToken)
    {
        if (!Initialized || BasePath == null) return;
        IDisposable? refreshLease = null;
        if (downloadActivity != null && !downloadActivity.TryAcquireCatalogRefresh(out refreshLease))
        {
            _catalogRefreshPending = true;
            return;
        }

        using (refreshLease)
        {
            IsRefreshing = true;
            try
            {
                if (networkCapabilityService != null)
                {
                    var capability = await networkCapabilityService.ProbeAsync(NetworkCapability.MinecraftMetadata, cancellationToken);
                    if (capability.EffectiveState is NetworkAvailabilityState.Unavailable or NetworkAvailabilityState.Degraded)
                    {
                        IsOfflineMode = true;
                        return;
                    }
                }

                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                deadline.CancelAfter(TimeSpan.FromSeconds(10));
                var versions = await Launcher.GetAllVersionsAsync(deadline.Token);
                await UiDispatcher.InvokeAsync(() =>
                {
                    VanillaVersions.Clear();
                    VanillaVersions.AddRange(versions.Select(x => new Versions.Version { ReleaseTime = x.ReleaseTime.DateTime, BasedOn = x.Name, ReleaseType = x.Type }));
                    IsOfflineMode = false;
                    foreach (var game in Games) game.CreateMCLauncher(false);
                });
                networkCapabilityService?.ReportSuccess(NetworkCapability.MinecraftMetadata);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                IsOfflineMode = true;
                networkCapabilityService?.ReportFailure(NetworkCapability.MinecraftMetadata, new TimeoutException("Minecraft version catalog request timed out."));
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Failed to refresh vanilla Minecraft versions; retaining local catalog.");
                IsOfflineMode = true;
                networkCapabilityService?.ReportFailure(NetworkCapability.MinecraftMetadata, ex);
            }
            finally
            {
                IsRefreshing = false;
                VersionsRefreshed?.Invoke(this, new());
            }
        }
    }

    private MinecraftLauncher CreateCatalogLauncher(MinecraftPath basePath)
    {
        var parameters = MinecraftLauncherParameters.CreateDefault(basePath);
        if (httpClient != null) parameters.HttpClient = httpClient;
        return new MinecraftLauncher(parameters);
    }

    private void SubscribeToDownloadActivity()
    {
        if (_downloadActivitySubscribed || downloadActivity == null) return;
        downloadActivity.Changed += (_, snapshot) =>
        {
            if (snapshot.ActiveDownloads == 0 && _catalogRefreshPending)
            {
                _catalogRefreshPending = false;
                _ = RefreshVersionCatalogAsync();
            }
        };
        _downloadActivitySubscribed = true;
    }

    /// <summary>
    /// Installs the specified game version with optional file progress display.
    /// </summary>
    /// <param name="version">The version of the game to be installed. Must exist in the collection of games.</param>
    /// <param name="showFileprog">Specifies whether to display file progress during installation.</param>
    /// <returns>A task that represents the asynchronous operation of installing the game version.</returns>
    public async Task InstallGame(Game game, bool showFileprog = false)
    {
        try
        {
            await InstallGameOrThrow(game, showFileprog);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Installation cancelled for game {version}", game?.Version.BasedOn);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to install game {version}: {ex}", game?.Version.BasedOn, ex.Message);
            _notify.Error("GameInstallError", ex.Message, ex: ex);
        }
    }

    public async Task InstallGameOrThrow(Game game, bool showFileprog = false)
    {
        if (game == null)
        {
            throw new ArgumentNullException(nameof(game));
        }

        var version = game.Version;
        _logger.LogInformation("Installing game {version}", version.BasedOn);

        var installer = installationService ?? CommunityToolkit.Mvvm.DependencyInjection.Ioc.Default.GetService<IInstanceInstallationService>()
            ?? throw new InvalidOperationException("Instance installation service is not available.");
        var result = await installer.InstallAsync(game);
        if (!result.Success)
        {
            throw new InvalidOperationException(result.FailureReason ?? "Installation failed.");
        }

        SaveGames();
    }

    public Task<InstanceIntegrityReport> VerifyGameAsync(Game game, IntegrityCheckLevel level, CancellationToken cancellationToken = default)
        => (installationService ?? CommunityToolkit.Mvvm.DependencyInjection.Ioc.Default.GetRequiredService<IInstanceInstallationService>())
            .VerifyAsync(game, level, cancellationToken: cancellationToken);

    public async Task<InstanceInstallResult> RepairGameAsync(Game game, CancellationToken cancellationToken = default)
    {
        var installer = installationService ?? CommunityToolkit.Mvvm.DependencyInjection.Ioc.Default.GetRequiredService<IInstanceInstallationService>();
        var result = await installer.RepairAsync(game, cancellationToken: cancellationToken);
        if (result.Success) SaveGames();
        return result;
    }

    public Game CreateGame(Versions.Version version, string? folderName = null)
    {
        _logger.LogInformation("Adding game {version}", version.BasedOn);

        if (BasePath == null)
        {
            throw new InvalidOperationException("Cannot add a game before the base path is initialized.");
        }

        var resolvedFolderName = string.IsNullOrWhiteSpace(folderName)
            ? version.DisplayName
            : folderName.Trim();
        var path = Path.Combine(BasePath.BasePath, GamesFolderName, resolvedFolderName);

        var game = new Game(new(path), version, sharedMinecraftBasePath: BasePath.BasePath, globalGameSettingsService: globalGameSettingsService);
        game.InstallationState = InstanceInstallationState.NotInstalled;

        Games.Add(game);
        try
        {
            SaveGames();
        }
        catch
        {
            Games.Remove(game);
            throw;
        }

        _notify.Info(
            "AddedGame",
            $"{version.DisplayName} based on {version.BasedOn} {version.Type}"
        );

        return game;
    }

    public void AddGame(Versions.Version version, string? folderName = null)
    {
        try
        {
            CreateGame(version, folderName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to add game {version}: {ex}", version.BasedOn, ex.Message);
            _notify.Error(
                "FailedToAddGame",
                $"Failed to add game {version.DisplayName} based on {version.BasedOn} {version.Type}",
               ex: ex
            );
        }
    }

    public void RemoveGame(Game game, bool deleteFolder = false)
    {
        try
        {
            _logger.LogInformation("Removing game {version}", game.Version.BasedOn);
            if (!Games.Contains(game))
            {
                _logger.LogWarning("Game {version} not found in collection", game.Version.BasedOn);
                throw new NullReferenceException($"Game {game.Version.BasedOn} not found in collection");
            }

            if (runtimeService.TryGetActiveSession(game) != null)
            {
                _logger.LogWarning("Refusing to remove running game {version}", game.Version.BasedOn);
                _notify.Warning("GameStillRunning", $"{game.Version.DisplayName} is still running. Stop it before removing the game.");
                return;
            }

            CancelInstallationAudit(game);
            Games.Remove(game);
            SaveGames();

            if (deleteFolder && Path.Exists(game.Path.BasePath))
            {
                _logger.LogInformation("Deleting game folder {path}", game.Path.BasePath);
                Directory.Delete(game.Path.BasePath, true);
            }

            var not = _notify.Info(
                "RemovedGame",
                $"{game.Version.DisplayName} based on {game.Version.BasedOn} {game.Version.Type}"
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to remove game {version}: {ex}", game.Version.BasedOn, ex.Message);
            _notify.Error(
                "FailedToRemoveGame",
                $"Failed to remove game {game.Version.DisplayName} based on {game.Version.BasedOn} {game.Version.Type}",
               ex: ex
            );
        }
    }

    private void SubscribeToNetworkState()
    {
        if (_networkSubscribed || networkCapabilityService == null) return;
        networkCapabilityService.Changed += NetworkCapabilityService_Changed;
        _networkSubscribed = true;
    }

    private void NetworkCapabilityService_Changed(object? sender, NetworkCapabilitySnapshot snapshot)
    {
        if (snapshot.Capability != NetworkCapability.MinecraftMetadata) return;
        // Checking is a transient probe state. Keep the last terminal result so
        // recovery polling cannot make the Home page flicker online/offline.
        if (snapshot.State == NetworkAvailabilityState.Checking) return;
        IsOfflineMode = snapshot.EffectiveState == NetworkAvailabilityState.Unavailable;
    }

    private void QueueInstallationAudit(Game game, int generation)
    {
        var installer = installationService ?? CommunityToolkit.Mvvm.DependencyInjection.Ioc.Default.GetService<IInstanceInstallationService>();
        if (installer == null) return;

        var startWorker = false;
        lock (_auditGate)
        {
            CancelInstallationAuditLocked(game);

            var item = new InstallationAuditWorkItem(game, generation);
            _auditsByGame[game] = item;
            _pendingAudits.Enqueue(item);
            if (!_auditWorkerRunning)
            {
                _auditWorkerRunning = true;
                startWorker = true;
            }
        }

        if (startWorker) _ = Task.Run(ProcessInstallationAuditsAsync);
    }

    /// <summary>
    /// Runs migration and stale full audits after catalog refresh has completed. Queue state is
    /// deliberately separate from <see cref="Games"/>, because that collection belongs to the UI thread.
    /// </summary>
    private async Task ProcessInstallationAuditsAsync()
    {
        var installer = installationService ?? CommunityToolkit.Mvvm.DependencyInjection.Ioc.Default.GetService<IInstanceInstallationService>();
        if (installer == null)
        {
            lock (_auditGate) _auditWorkerRunning = false;
            return;
        }

        try
        {
            while (true)
            {
                // The page is interactive once catalog refresh has finished. Unlike a fixed delay,
                // this also behaves correctly when remote metadata is slow.
                while (IsRefreshing)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100));
                }

                var item = TakeNextInstallationAudit();
                if (item == null) break;

                var requeued = false;
                try
                {
                    var result = await installer.VerifyWhenIdleAsync(
                        item.Game,
                        item.CheckLevel,
                        cancellationToken: item.Cancellation.Token);
                    if (result == null)
                    {
                        // A user operation or game session owns this instance. Give the other
                        // instances a turn, then retry this audit instead of silently losing it.
                        requeued = RequeueInstallationAudit(item);
                        if (requeued) await Task.Delay(TimeSpan.FromSeconds(1));
                        continue;
                    }

                    if (item.CheckLevel == IntegrityCheckLevel.Quick)
                    {
                        var store = CommunityToolkit.Mvvm.DependencyInjection.Ioc.Default.GetService<IInstallationStateStore>();
                        var receipt = store == null ? null : await store.ReadAsync(item.Game, item.Cancellation.Token);
                        if (result.CanLaunch
                            && receipt?.FullVerificationAt is DateTimeOffset verified
                            && DateTimeOffset.UtcNow - verified > TimeSpan.FromDays(7))
                        {
                            item.CheckLevel = IntegrityCheckLevel.Full;
                            requeued = RequeueInstallationAudit(item);
                        }
                    }
                }
                catch (OperationCanceledException) when (item.Cancellation.IsCancellationRequested)
                {
                    // A reload or removal invalidated this work item.
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Background installation audit failed for {game}.", item.Game.Version.DisplayName);
                }
                finally
                {
                    if (!requeued) CompleteInstallationAudit(item);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Background installation audit queue stopped unexpectedly.");
        }
        finally
        {
            lock (_auditGate)
            {
                _auditWorkerRunning = false;
                if (_pendingAudits.Count > 0)
                {
                    _auditWorkerRunning = true;
                    _ = Task.Run(ProcessInstallationAuditsAsync);
                }
            }
        }
    }

    private InstallationAuditWorkItem? TakeNextInstallationAudit()
    {
        lock (_auditGate)
        {
            while (_pendingAudits.TryDequeue(out var item))
            {
                if (!IsCurrentInstallationAuditLocked(item))
                {
                    item.Cancellation.Dispose();
                    continue;
                }

                _activeAudits.Add(item);
                return item;
            }

            return null;
        }
    }

    private bool RequeueInstallationAudit(InstallationAuditWorkItem item)
    {
        lock (_auditGate)
        {
            _activeAudits.Remove(item);
            if (!IsCurrentInstallationAuditLocked(item))
            {
                return false;
            }

            _pendingAudits.Enqueue(item);
            return true;
        }
    }

    private void CompleteInstallationAudit(InstallationAuditWorkItem item)
    {
        lock (_auditGate)
        {
            _activeAudits.Remove(item);
            if (_auditsByGame.TryGetValue(item.Game, out var current) && ReferenceEquals(current, item))
            {
                _auditsByGame.Remove(item.Game);
            }
        }

        item.Cancellation.Dispose();
    }

    private void CancelInstallationAudit(Game game)
    {
        lock (_auditGate)
        {
            CancelInstallationAuditLocked(game);
        }
    }

    private void CancelInstallationAuditLocked(Game game)
    {
        if (_auditsByGame.Remove(game, out var item))
        {
            item.Cancellation.Cancel();
        }
    }

    private void CancelAllInstallationAuditsLocked()
    {
        var queued = _pendingAudits.ToArray();
        var queuedSet = queued.ToHashSet();
        _pendingAudits.Clear();

        foreach (var item in queued)
        {
            item.Cancellation.Cancel();
            item.Cancellation.Dispose();
        }

        foreach (var item in _auditsByGame.Values.Where(item => !queuedSet.Contains(item)))
        {
            item.Cancellation.Cancel();
        }

        _auditsByGame.Clear();
    }

    private bool IsCurrentInstallationAuditLocked(InstallationAuditWorkItem item)
        => item.Generation == Volatile.Read(ref _gamesGeneration)
            && !item.Cancellation.IsCancellationRequested
            && _auditsByGame.TryGetValue(item.Game, out var current)
            && ReferenceEquals(current, item);

    private static bool PathsEqual(string left, string right)
        => string.Equals(
            Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
