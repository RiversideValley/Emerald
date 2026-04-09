using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using Emerald.CoreX.Models;
using Emerald.CoreX.Notifications;
using Emerald.CoreX.Services;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;

namespace Emerald.CoreX.Runtime;

/// <summary>
/// Launches games, tracks live sessions, and publishes normalized runtime logs to the UI.
/// </summary>
public sealed class GameRuntimeService : IGameRuntimeService
{
    private static readonly TimeSpan TextEventSettleDelay = TimeSpan.FromMilliseconds(100);

    private sealed class ActiveSessionRuntime
    {
        public required string PathKey { get; init; }
        public required GameSession Session { get; init; }
        public required bool LogCaptureEnabled { get; init; }
        public required HashSet<string> ExistingCrashReports { get; init; }
        public required CancellationTokenSource Cancellation { get; init; }
        public required Dictionary<GameLogSource, MinecraftLogEventAssembler> Assemblers { get; init; }
        public required GameLogDeduplicator Deduplicator { get; init; }

        public Process? Process { get; set; }
        public bool ProcessStarted { get; set; }
        public bool CanReadStandardStreams { get; set; }
        public bool ExitHandled { get; set; }
        public GameStopMode? RequestedStopMode { get; set; }
        public DataReceivedEventHandler? OutputHandler { get; set; }
        public DataReceivedEventHandler? ErrorHandler { get; set; }
        public EventHandler? ExitedHandler { get; set; }
        public object LogGate { get; } = new();
    }

    private readonly ILogger<GameRuntimeService> _logger;
    private readonly INotificationService _notificationService;
    private readonly IAccountService _accountService;
    private readonly IGameRuntimeSettings _settings;
    private readonly DispatcherQueue _dispatcher;
    private readonly object _syncRoot = new();
    private readonly Dictionary<string, ActiveSessionRuntime> _activeSessions = new(StringComparer.OrdinalIgnoreCase);

    public ObservableCollection<GameSession> Sessions { get; } = new();

    /// <summary>
    /// Initializes a new runtime service instance with the dependencies needed to track sessions.
    /// </summary>
    public GameRuntimeService(
        ILogger<GameRuntimeService> logger,
        INotificationService notificationService,
        IAccountService accountService,
        IGameRuntimeSettings settings,
        DispatcherQueue dispatcher)
    {
        _logger = logger;
        _notificationService = notificationService;
        _accountService = accountService;
        _settings = settings;
        _dispatcher = dispatcher;
        _logger.LogInformation("Game runtime service initialized.");
    }

    /// <summary>
    /// Returns the active runtime session for the supplied game, if one exists.
    /// </summary>
    public GameSession? TryGetActiveSession(Game game)
    {
        if (game == null)
        {
            _logger.LogDebug("Skipping active session lookup because no game was provided.");
            return null;
        }

        lock (_syncRoot)
        {
            var session = _activeSessions.TryGetValue(GetPathKey(game.Path.BasePath), out var runtime)
                ? runtime.Session
                : null;

            _logger.LogDebug(
                "Active session lookup completed for {GameName}. FoundSession: {FoundSession}.",
                game.Version.DisplayName,
                session != null);

            return session;
        }
    }

    /// <summary>
    /// Finds the newest known session for the supplied game path.
    /// </summary>
    public GameSession? FindLatestSession(string gamePath)
    {
        if (string.IsNullOrWhiteSpace(gamePath))
        {
            _logger.LogDebug("Skipping latest session lookup because the game path was empty.");
            return null;
        }

        var session = RunOnUI(() => Sessions.FirstOrDefault(x => string.Equals(GetPathKey(x.GamePath), GetPathKey(gamePath), StringComparison.OrdinalIgnoreCase)));
        _logger.LogDebug(
            "Latest session lookup completed for path {GamePath}. FoundSession: {FoundSession}.",
            gamePath,
            session != null);
        return session;
    }

    /// <summary>
    /// Launches the supplied game and starts tracking its runtime session.
    /// </summary>
    public async Task<GameSession?> LaunchAsync(Game game, EAccount? account = null)
    {
        if (game == null)
        {
            _logger.LogWarning("Skipping launch because no game was provided.");
            return null;
        }

        _logger.LogInformation(
            "Launching game runtime for {GameName}. Path: {GamePath}.",
            game.Version.DisplayName,
            game.Path.BasePath);

        if (string.IsNullOrWhiteSpace(game.Version.RealVersion))
        {
            _logger.LogWarning(
                "Skipping launch for {GameName} because no installed runtime version is available.",
                game.Version.DisplayName);
            _notificationService.Warning("InstallRequired", $"Install or update {game.Version.DisplayName} before launching.");
            return null;
        }

        account ??= _accountService.GetSelectedAccount();
        if (account == null)
        {
            _logger.LogWarning(
                "Skipping launch for {GameName} because no account is available.",
                game.Version.DisplayName);
            _notificationService.Warning("NoAccount", "Please sign in to an account first");
            return null;
        }

        ActiveSessionRuntime runtime;
        lock (_syncRoot)
        {
            var pathKey = GetPathKey(game.Path.BasePath);
            if (_activeSessions.TryGetValue(pathKey, out var existingRuntime))
            {
                _logger.LogInformation(
                    "Launch request ignored because {GameName} is already running.",
                    game.Version.DisplayName);
                _notificationService.Warning("GameAlreadyRunning", $"{game.Version.DisplayName} is already running.");
                return existingRuntime.Session;
            }

            var session = new GameSession(game, DateTimeOffset.Now)
            {
                State = GameRunState.Launching,
                CaptureMode = _settings.IsLogCaptureEnabled ? GameCaptureMode.StandardOutputOnly : GameCaptureMode.LifecycleOnly
            };

            runtime = new ActiveSessionRuntime
            {
                PathKey = pathKey,
                Session = session,
                LogCaptureEnabled = _settings.IsLogCaptureEnabled,
                ExistingCrashReports = SnapshotCrashReports(game.Path.BasePath),
                Cancellation = new CancellationTokenSource(),
                Assemblers = new Dictionary<GameLogSource, MinecraftLogEventAssembler>
                {
                    [GameLogSource.StandardOutput] = new(GameLogSource.StandardOutput),
                    [GameLogSource.StandardError] = new(GameLogSource.StandardError)
                },
                Deduplicator = new GameLogDeduplicator()
            };

            _activeSessions[pathKey] = runtime;
        }

        _logger.LogDebug(
            "Created runtime session for {GameName}. LogCaptureEnabled: {LogCaptureEnabled}. KnownCrashReports: {CrashReportCount}.",
            game.Version.DisplayName,
            runtime.LogCaptureEnabled,
            runtime.ExistingCrashReports.Count);

        RunOnUI(() =>
        {
            Sessions.Insert(0, runtime.Session);
            ApplyActiveState(game, runtime.Session, null);
        });

        AppendLifecycle(runtime, GameLogLevel.Info, $"Preparing launch for {game.Version.DisplayName}.");

        if (!runtime.LogCaptureEnabled)
        {
            AppendLifecycle(runtime, GameLogLevel.Info, "Game log capture is disabled in settings.");
        }

        try
        {
            _logger.LogDebug("Authenticating launch account for {GameName}.", game.Version.DisplayName);
            var mcSession = await _accountService.AuthenticateAccountAsync(account);
            ThrowIfLaunchCancelled(runtime);

            var process = await game.BuildProcess(game.Version.RealVersion, mcSession);
            runtime.Process = process;

            _logger.LogDebug(
                "Process created for {GameName}. Verb: {Verb}. UseShellExecute: {UseShellExecute}.",
                game.Version.DisplayName,
                process.StartInfo.Verb,
                process.StartInfo.UseShellExecute);

            ConfigureProcess(process, runtime);
            AttachProcessHandlers(runtime);

            ThrowIfLaunchCancelled(runtime);

            if (!process.Start())
            {
                throw new InvalidOperationException($"Failed to start {game.Version.DisplayName}.");
            }

            runtime.ProcessStarted = true;

            if (runtime.CanReadStandardStreams)
            {
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }

            RunOnUI(() =>
            {
                runtime.Session.ProcessId = TryGetProcessId(process);
                runtime.Session.State = GameRunState.Running;
                runtime.Session.CaptureMode = runtime.LogCaptureEnabled
                    ? runtime.CanReadStandardStreams ? GameCaptureMode.StandardOutputOnly : GameCaptureMode.StandardOutputUnavailable
                    : GameCaptureMode.LifecycleOnly;

                ApplyActiveState(game, runtime.Session, runtime.Session.ProcessId);
            });

            _logger.LogInformation(
                "Game process started for {GameName}. PID: {ProcessId}. CaptureMode: {CaptureMode}.",
                game.Version.DisplayName,
                runtime.Session.ProcessId,
                runtime.Session.CaptureMode);

            if (runtime.LogCaptureEnabled && !runtime.CanReadStandardStreams)
            {
                AppendLifecycle(runtime, GameLogLevel.Warn, "Standard output capture is unavailable for this session. Only lifecycle events will be shown.");
            }

            AppendLifecycle(runtime, GameLogLevel.Info, $"Launched {game.Version.DisplayName}.");
            _notificationService.Info("GameLaunched", $"Launched {game.Version.DisplayName}");
            return runtime.Session;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Launch cancelled for {GameName}.", game.Version.DisplayName);
            AppendLifecycle(runtime, GameLogLevel.Warn, $"Launch cancelled for {game.Version.DisplayName}.");
            CompleteFailedLaunch(runtime, GameRunState.Exited);
            return runtime.Session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to launch game {GameName}", game.Version.DisplayName);
            AppendSyntheticEntry(runtime, GameLogLevel.Error, $"Failed to launch {game.Version.DisplayName}: {ex.Message}", GameLogSource.Lifecycle);
            CompleteFailedLaunch(runtime, GameRunState.Failed, ex);
            _notificationService.Error("LaunchError", $"Failed to launch {game.Version.DisplayName}", ex: ex);
            return runtime.Session;
        }
    }

    /// <summary>
    /// Stops the supplied game if it currently has an active runtime session.
    /// </summary>
    public async Task StopAsync(Game game, GameStopMode mode)
    {
        if (game == null)
        {
            _logger.LogDebug("Skipping stop request because no game was provided.");
            return;
        }

        ActiveSessionRuntime? runtime;
        lock (_syncRoot)
        {
            _activeSessions.TryGetValue(GetPathKey(game.Path.BasePath), out runtime);
        }

        if (runtime == null)
        {
            _logger.LogDebug(
                "Ignoring stop request for {GameName} because no active session exists.",
                game.Version.DisplayName);
            return;
        }

        _logger.LogInformation(
            "Stop requested for {GameName}. Mode: {Mode}.",
            runtime.Session.DisplayName,
            mode);

        runtime.RequestedStopMode = mode;

        RunOnUI(() =>
        {
            runtime.Session.State = GameRunState.Stopping;
            ApplyActiveState(game, runtime.Session, runtime.Session.ProcessId);
        });

        if (!runtime.ProcessStarted || runtime.Process == null)
        {
            _logger.LogWarning(
                "Stop requested for {GameName} while the launch was still starting.",
                runtime.Session.DisplayName);
            AppendLifecycle(runtime, GameLogLevel.Warn, "Stop requested while the launch is still starting.");
            runtime.Cancellation.Cancel();
            return;
        }

        if (runtime.Process.HasExited)
        {
            _logger.LogInformation(
                "Ignoring stop request for {GameName} because the process has already exited.",
                runtime.Session.DisplayName);
            return;
        }

        if (mode == GameStopMode.Gentle)
        {
            _logger.LogInformation("Requesting graceful shutdown for {GameName}.", runtime.Session.DisplayName);
            AppendLifecycle(runtime, GameLogLevel.Info, "Requesting graceful shutdown.");

            bool closeRequested;
            try
            {
                closeRequested = runtime.Process.CloseMainWindow();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to request graceful shutdown for {GameName}", runtime.Session.DisplayName);
                closeRequested = false;
            }

            if (!closeRequested)
            {
                _logger.LogWarning(
                    "Graceful shutdown is unavailable for {GameName}; the main window could not be closed.",
                    runtime.Session.DisplayName);
                AppendLifecycle(runtime, GameLogLevel.Warn, "Graceful shutdown is unavailable for this process. Force Stop is still available.");
                RestoreRunningState(runtime);
                return;
            }

            if (!await WaitForExitAsync(runtime.Process, TimeSpan.FromSeconds(5)))
            {
                _logger.LogWarning(
                    "Graceful shutdown timed out for {GameName}.",
                    runtime.Session.DisplayName);
                AppendLifecycle(runtime, GameLogLevel.Warn, "The game did not exit in time. Force Stop is still available.");
                RestoreRunningState(runtime);
            }

            return;
        }

        _logger.LogInformation("Force stopping {GameName}.", runtime.Session.DisplayName);
        AppendLifecycle(runtime, GameLogLevel.Warn, "Force stopping the game process.");

        try
        {
            runtime.Process.Kill(true);
        }
        catch (Exception ex) when (!runtime.Process.HasExited)
        {
            _logger.LogWarning(ex, "Falling back to single-process kill for {GameName}", runtime.Session.DisplayName);
            runtime.Process.Kill();
        }

        await WaitForExitAsync(runtime.Process, TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// Configures standard output capture on the supplied process when the session supports it.
    /// </summary>
    private void ConfigureProcess(Process process, ActiveSessionRuntime runtime)
    {
        process.EnableRaisingEvents = true;

        if (!runtime.LogCaptureEnabled)
        {
            runtime.CanReadStandardStreams = false;
            _logger.LogDebug(
                "Standard stream capture disabled for {GameName} because runtime log capture is off.",
                runtime.Session.DisplayName);
            return;
        }

        var canRedirect = string.IsNullOrWhiteSpace(process.StartInfo.Verb);
        if (!canRedirect)
        {
            runtime.CanReadStandardStreams = false;
            _logger.LogWarning(
                "Standard stream capture is unavailable for {GameName} because the process verb requires shell execution. Verb: {Verb}.",
                runtime.Session.DisplayName,
                process.StartInfo.Verb);
            return;
        }

        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.StandardOutputEncoding = Encoding.UTF8;
        process.StartInfo.StandardErrorEncoding = Encoding.UTF8;
        runtime.CanReadStandardStreams = true;
        _logger.LogDebug("Enabled standard stream capture for {GameName}.", runtime.Session.DisplayName);
    }

    /// <summary>
    /// Attaches the process event handlers used for log capture and exit tracking.
    /// </summary>
    private void AttachProcessHandlers(ActiveSessionRuntime runtime)
    {
        if (runtime.Process == null)
        {
            _logger.LogDebug(
                "Skipping process handler attachment for {GameName} because no process is available.",
                runtime.Session.DisplayName);
            return;
        }

        runtime.ExitedHandler = (_, _) => _ = HandleProcessExitAsync(runtime);
        runtime.Process.Exited += runtime.ExitedHandler;

        if (!runtime.CanReadStandardStreams)
        {
            return;
        }

        runtime.OutputHandler = (_, args) =>
        {
            if (args.Data != null)
            {
                AppendCapturedLine(runtime, args.Data, GameLogSource.StandardOutput);
            }
        };

        runtime.ErrorHandler = (_, args) =>
        {
            if (args.Data != null)
            {
                AppendCapturedLine(runtime, args.Data, GameLogSource.StandardError);
            }
        };

        runtime.Process.OutputDataReceived += runtime.OutputHandler;
        runtime.Process.ErrorDataReceived += runtime.ErrorHandler;
        _logger.LogDebug("Attached process handlers for {GameName}.", runtime.Session.DisplayName);
    }

    /// <summary>
    /// Finalizes session state when the tracked process exits.
    /// </summary>
    private async Task HandleProcessExitAsync(ActiveSessionRuntime runtime)
    {
        if (runtime.Process == null)
        {
            _logger.LogDebug(
                "Skipping process exit handling for {GameName} because no process is available.",
                runtime.Session.DisplayName);
            return;
        }

        lock (runtime.LogGate)
        {
            if (runtime.ExitHandled)
            {
                return;
            }

            runtime.ExitHandled = true;
        }

        try
        {
            _logger.LogDebug("Handling process exit for {GameName}.", runtime.Session.DisplayName);
            await runtime.Process.WaitForExitAsync();
            runtime.Cancellation.Cancel();

            FlushPendingAssemblers(runtime);

            var crashReports = FindNewCrashReports(runtime);
            if (crashReports.Count > 0)
            {
                _logger.LogWarning(
                    "Detected {CrashReportCount} new crash report(s) for {GameName}.",
                    crashReports.Count,
                    runtime.Session.DisplayName);
            }

            foreach (var crashReportPath in crashReports)
            {
                AppendSyntheticEntry(runtime, GameLogLevel.Fatal, $"Crash report generated: {crashReportPath}", GameLogSource.CrashReport);
            }

            var exitCode = SafeGetExitCode(runtime.Process);
            var finalState = DetermineFinalState(runtime, exitCode, crashReports.Count > 0);

            AppendLifecycle(
                runtime,
                finalState == GameRunState.Failed ? GameLogLevel.Error : GameLogLevel.Info,
                $"Process exited with code {exitCode}.");

            RunOnUI(() =>
            {
                runtime.Session.State = finalState;
                runtime.Session.EndedAt = DateTimeOffset.Now;
                runtime.Session.ExitCode = exitCode;
                runtime.Session.HasCrashReport = crashReports.Count > 0;
                runtime.Session.CrashReportPath = crashReports.FirstOrDefault();

                ApplyInactiveState(runtime.Session.Game, runtime.Session);
            });

            _logger.LogInformation(
                "Process exit finalized for {GameName}. ExitCode: {ExitCode}. FinalState: {FinalState}. CrashReports: {CrashReportCount}.",
                runtime.Session.DisplayName,
                exitCode,
                finalState,
                crashReports.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed while finalizing process exit for {GameName}", runtime.Session.DisplayName);
            RunOnUI(() =>
            {
                runtime.Session.State = GameRunState.Failed;
                runtime.Session.EndedAt = DateTimeOffset.Now;
                ApplyInactiveState(runtime.Session.Game, runtime.Session);
            });
        }
        finally
        {
            lock (_syncRoot)
            {
                _activeSessions.Remove(runtime.PathKey);
            }

            DetachProcessHandlers(runtime);
            runtime.Process.Dispose();
        }
    }

    /// <summary>
    /// Detaches any process handlers previously attached for a session runtime.
    /// </summary>
    private void DetachProcessHandlers(ActiveSessionRuntime runtime)
    {
        if (runtime.Process == null)
        {
            return;
        }

        if (runtime.OutputHandler != null)
        {
            runtime.Process.OutputDataReceived -= runtime.OutputHandler;
        }

        if (runtime.ErrorHandler != null)
        {
            runtime.Process.ErrorDataReceived -= runtime.ErrorHandler;
        }

        if (runtime.ExitedHandler != null)
        {
            runtime.Process.Exited -= runtime.ExitedHandler;
        }
    }

    /// <summary>
    /// Publishes a raw captured line into the assembler pipeline for a specific source.
    /// </summary>
    private void AppendCapturedLine(ActiveSessionRuntime runtime, string rawLine, GameLogSource source)
    {
        List<GameLogEntry> finalizedEntries;
        long? pendingTextVersion = null;

        lock (runtime.LogGate)
        {
            if (!runtime.Assemblers.TryGetValue(source, out var assembler))
            {
                return;
            }

            finalizedEntries = [.. assembler.AppendLine(rawLine, DateTimeOffset.Now)];
            if (assembler.HasPendingText)
            {
                pendingTextVersion = assembler.PendingTextVersion;
            }
        }

        foreach (var entry in finalizedEntries)
        {
            PublishEntry(runtime, entry);
        }

        if (pendingTextVersion.HasValue)
        {
            _ = FlushPendingTextAfterDelayAsync(runtime, source, pendingTextVersion.Value);
        }
    }

    /// <summary>
    /// Flushes a pending text event after a short settle delay if no newer lines arrive.
    /// </summary>
    private async Task FlushPendingTextAfterDelayAsync(ActiveSessionRuntime runtime, GameLogSource source, long expectedVersion)
    {
        try
        {
            await Task.Delay(TextEventSettleDelay, runtime.Cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        GameLogEntry? entry = null;
        lock (runtime.LogGate)
        {
            if (runtime.Assemblers.TryGetValue(source, out var assembler))
            {
                assembler.TryFlushPendingText(expectedVersion, DateTimeOffset.Now, out entry);
            }
        }

        if (entry != null)
        {
            PublishEntry(runtime, entry);
        }
    }

    /// <summary>
    /// Flushes any pending assembler state before a session finishes.
    /// </summary>
    private void FlushPendingAssemblers(ActiveSessionRuntime runtime)
    {
        List<GameLogEntry> pendingEntries;
        lock (runtime.LogGate)
        {
            pendingEntries = runtime.Assemblers.Values
                .SelectMany(assembler => assembler.FlushPending(DateTimeOffset.Now, includeXmlFallback: true))
                .ToList();
        }

        if (pendingEntries.Count > 0)
        {
            _logger.LogDebug(
                "Flushing {PendingEntryCount} pending log entries for {GameName} before session completion.",
                pendingEntries.Count,
                runtime.Session.DisplayName);
        }

        foreach (var entry in pendingEntries)
        {
            PublishEntry(runtime, entry);
        }
    }

    /// <summary>
    /// Applies deduplication and appends the entry to the session if it remains visible.
    /// </summary>
    private void PublishEntry(ActiveSessionRuntime runtime, GameLogEntry entry)
    {
        GameLogEntry? replacedEntry = null;
        bool shouldAppend;

        lock (runtime.LogGate)
        {
            var dedupeResult = runtime.Deduplicator.Register(entry, DateTimeOffset.Now);
            shouldAppend = dedupeResult.ShouldAppend;
            replacedEntry = dedupeResult.EntryToRemove;
        }

        if (!shouldAppend)
        {
            return;
        }

        if (replacedEntry != null)
        {
            RemoveEntry(runtime, replacedEntry);
        }

        AppendEntry(runtime, entry);
    }

    private void AppendLifecycle(ActiveSessionRuntime runtime, GameLogLevel level, string message)
        => AppendSyntheticEntry(runtime, level, message, GameLogSource.Lifecycle);

    /// <summary>
    /// Publishes a synthetic runtime entry for lifecycle or crash-report notifications.
    /// </summary>
    private void AppendSyntheticEntry(ActiveSessionRuntime runtime, GameLogLevel level, string message, GameLogSource source)
    {
        var entry = new GameLogEntry
        {
            Timestamp = DateTimeOffset.Now,
            Level = level,
            Message = message,
            Source = source,
            RawPayload = message,
            IsSynthetic = true
        };

        PublishEntry(runtime, entry);
    }

    /// <summary>
    /// Appends an entry to the UI-bound session collection.
    /// </summary>
    private void AppendEntry(ActiveSessionRuntime runtime, GameLogEntry entry)
    {
        RunOnUI(() =>
        {
            runtime.Session.Entries.Add(entry);
            UpdateSessionLogSummary(runtime.Session);
        });
    }

    /// <summary>
    /// Removes a replaced entry from the UI-bound session collection.
    /// </summary>
    private void RemoveEntry(ActiveSessionRuntime runtime, GameLogEntry entry)
    {
        RunOnUI(() =>
        {
            if (runtime.Session.Entries.Remove(entry))
            {
                UpdateSessionLogSummary(runtime.Session);
            }
        });
    }

    /// <summary>
    /// Updates the session summary fields shown in the logs and games pages.
    /// </summary>
    private static void UpdateSessionLogSummary(GameSession session)
    {
        session.EntryCount = session.Entries.Count;
        session.LastMessagePreview = session.Entries.LastOrDefault()?.Message.Trim();
    }

    /// <summary>
    /// Cleans up a launch attempt that ended before a stable running state was reached.
    /// </summary>
    private void CompleteFailedLaunch(ActiveSessionRuntime runtime, GameRunState state, Exception? ex = null)
    {
        if (state == GameRunState.Failed)
        {
            _logger.LogWarning(
                "Completing failed launch for {GameName}. HasException: {HasException}.",
                runtime.Session.DisplayName,
                ex != null);
        }
        else
        {
            _logger.LogInformation(
                "Completing interrupted launch for {GameName}. FinalState: {FinalState}.",
                runtime.Session.DisplayName,
                state);
        }

        lock (_syncRoot)
        {
            _activeSessions.Remove(runtime.PathKey);
        }

        runtime.Cancellation.Cancel();
        if (runtime.Process != null)
        {
            DetachProcessHandlers(runtime);
            runtime.Process.Dispose();
        }

        RunOnUI(() =>
        {
            runtime.Session.State = state;
            runtime.Session.EndedAt = DateTimeOffset.Now;
            runtime.Session.ExitCode = null;
            ApplyInactiveState(runtime.Session.Game, runtime.Session);
        });
    }

    /// <summary>
    /// Restores a session to the running state when a stop attempt could not complete.
    /// </summary>
    private void RestoreRunningState(ActiveSessionRuntime runtime)
    {
        if (runtime.Process == null || runtime.Process.HasExited)
        {
            return;
        }

        runtime.RequestedStopMode = null;
        _logger.LogInformation("Restoring running state for {GameName}.", runtime.Session.DisplayName);

        RunOnUI(() =>
        {
            runtime.Session.State = GameRunState.Running;
            ApplyActiveState(runtime.Session.Game, runtime.Session, runtime.Session.ProcessId);
        });
    }

    /// <summary>
    /// Copies active runtime state back onto the corresponding game model.
    /// </summary>
    private void ApplyActiveState(Game game, GameSession session, int? processId)
    {
        game.HasActiveSession = true;
        game.ActiveProcessId = processId;
        game.RunState = session.State;
        game.LastExitCode = null;
        game.LastRunEndedAt = null;
    }

    /// <summary>
    /// Copies inactive runtime state back onto the corresponding game model.
    /// </summary>
    private void ApplyInactiveState(Game game, GameSession session)
    {
        game.HasActiveSession = false;
        game.ActiveProcessId = null;
        game.RunState = session.State;
        game.LastExitCode = session.ExitCode;
        game.LastRunEndedAt = session.EndedAt;
    }

    /// <summary>
    /// Throws when launch cancellation has been requested for the current session.
    /// </summary>
    private void ThrowIfLaunchCancelled(ActiveSessionRuntime runtime)
    {
        if (runtime.Cancellation.IsCancellationRequested)
        {
            throw new OperationCanceledException(runtime.Cancellation.Token);
        }
    }

    private static int? TryGetProcessId(Process process)
    {
        try
        {
            return process.Id;
        }
        catch
        {
            return null;
        }
    }

    private static int SafeGetExitCode(Process process)
    {
        try
        {
            return process.ExitCode;
        }
        catch
        {
            return -1;
        }
    }

    /// <summary>
    /// Waits for the supplied process to exit up to the requested timeout.
    /// </summary>
    private static async Task<bool> WaitForExitAsync(Process process, TimeSpan timeout)
    {
        try
        {
            var waitTask = process.WaitForExitAsync();
            var completed = await Task.WhenAny(waitTask, Task.Delay(timeout)) == waitTask;
            return completed;
        }
        catch
        {
            return process.HasExited;
        }
    }

    /// <summary>
    /// Captures the existing crash reports before a new session starts.
    /// </summary>
    private HashSet<string> SnapshotCrashReports(string gamePath)
    {
        var crashDirectory = Path.Combine(gamePath, "crash-reports");
        if (!Directory.Exists(crashDirectory))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var crashReports = Directory.EnumerateFiles(crashDirectory).ToHashSet(StringComparer.OrdinalIgnoreCase);
        _logger.LogDebug(
            "Captured {CrashReportCount} existing crash report(s) for game path {GamePath}.",
            crashReports.Count,
            gamePath);
        return crashReports;
    }

    /// <summary>
    /// Finds crash reports created after the current session began.
    /// </summary>
    private List<string> FindNewCrashReports(ActiveSessionRuntime runtime)
    {
        var crashDirectory = Path.Combine(runtime.Session.GamePath, "crash-reports");
        if (!Directory.Exists(crashDirectory))
        {
            return [];
        }

        return Directory.EnumerateFiles(crashDirectory)
            .Where(path => !runtime.ExistingCrashReports.Contains(path))
            .OrderByDescending(path => File.GetCreationTimeUtc(path))
            .ToList();
    }

    /// <summary>
    /// Determines the final session state from the exit code, stop mode, and crash-report outcome.
    /// </summary>
    private GameRunState DetermineFinalState(ActiveSessionRuntime runtime, int exitCode, bool hasCrashReport)
    {
        if (hasCrashReport)
        {
            return GameRunState.Failed;
        }

        if (runtime.RequestedStopMode != null)
        {
            return GameRunState.Exited;
        }

        return exitCode == 0 ? GameRunState.Exited : GameRunState.Failed;
    }

    /// <summary>
    /// Executes the supplied delegate on the UI dispatcher and returns its result.
    /// </summary>
    private T RunOnUI<T>(Func<T> action)
    {
        if (_dispatcher.HasThreadAccess)
        {
            return action();
        }

        var tcs = new TaskCompletionSource<T>();
        _dispatcher.TryEnqueue(() =>
        {
            try
            {
                tcs.SetResult(action());
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task.GetAwaiter().GetResult();
    }

    /// <summary>
    /// Executes the supplied delegate on the UI dispatcher without a return value.
    /// </summary>
    private void RunOnUI(Action action)
        => RunOnUI(() =>
        {
            action();
            return true;
        });

    private static string GetPathKey(string path)
    {
        try
        {
            return Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            return path;
        }
    }
}
