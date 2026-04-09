using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using Emerald.CoreX.Notifications;
using Emerald.CoreX.Services;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;

namespace Emerald.CoreX.Runtime;

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
        public Task? TailTask { get; set; }
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
    }

    public GameSession? TryGetActiveSession(Game game)
    {
        if (game == null)
        {
            return null;
        }

        lock (_syncRoot)
        {
            return _activeSessions.TryGetValue(GetPathKey(game.Path.BasePath), out var runtime)
                ? runtime.Session
                : null;
        }
    }

    public GameSession? FindLatestSession(string gamePath)
        => RunOnUI(() => Sessions.FirstOrDefault(x => string.Equals(GetPathKey(x.GamePath), GetPathKey(gamePath), StringComparison.OrdinalIgnoreCase)));

    public async Task<GameSession?> LaunchAsync(Game game)
    {
        if (game == null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(game.Version.RealVersion))
        {
            _notificationService.Warning("InstallRequired", $"Install or update {game.Version.DisplayName} before launching.");
            return null;
        }

        var account = _accountService.GetMostRecentlyUsedAccount();
        if (account == null)
        {
            _notificationService.Warning("NoAccount", "Please sign in to an account first");
            return null;
        }

        ActiveSessionRuntime runtime;
        lock (_syncRoot)
        {
            var pathKey = GetPathKey(game.Path.BasePath);
            if (_activeSessions.TryGetValue(pathKey, out var existingRuntime))
            {
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
                    [GameLogSource.StandardError] = new(GameLogSource.StandardError),
                    [GameLogSource.FileTail] = new(GameLogSource.FileTail)
                },
                Deduplicator = new GameLogDeduplicator()
            };

            _activeSessions[pathKey] = runtime;
        }

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
            var mcSession = await _accountService.AuthenticateAccountAsync(account);
            ThrowIfLaunchCancelled(runtime);

            var process = await game.BuildProcess(game.Version.RealVersion, mcSession);
            runtime.Process = process;

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

            runtime.TailTask = runtime.LogCaptureEnabled
                ? Task.Run(() => TailLatestLogAsync(runtime, runtime.Cancellation.Token))
                : Task.CompletedTask;

            RunOnUI(() =>
            {
                runtime.Session.ProcessId = TryGetProcessId(process);
                runtime.Session.State = GameRunState.Running;
                runtime.Session.CaptureMode = runtime.LogCaptureEnabled
                    ? runtime.CanReadStandardStreams ? GameCaptureMode.StandardOutputOnly : GameCaptureMode.FileOnly
                    : GameCaptureMode.LifecycleOnly;

                ApplyActiveState(game, runtime.Session, runtime.Session.ProcessId);
            });

            AppendLifecycle(runtime, GameLogLevel.Info, $"Launched {game.Version.DisplayName}.");
            _notificationService.Info("GameLaunched", $"Launched {game.Version.DisplayName}");
            return runtime.Session;
        }
        catch (OperationCanceledException)
        {
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

    public async Task StopAsync(Game game, GameStopMode mode)
    {
        if (game == null)
        {
            return;
        }

        ActiveSessionRuntime? runtime;
        lock (_syncRoot)
        {
            _activeSessions.TryGetValue(GetPathKey(game.Path.BasePath), out runtime);
        }

        if (runtime == null)
        {
            return;
        }

        runtime.RequestedStopMode = mode;

        RunOnUI(() =>
        {
            runtime.Session.State = GameRunState.Stopping;
            ApplyActiveState(game, runtime.Session, runtime.Session.ProcessId);
        });

        if (!runtime.ProcessStarted || runtime.Process == null)
        {
            AppendLifecycle(runtime, GameLogLevel.Warn, "Stop requested while the launch is still starting.");
            runtime.Cancellation.Cancel();
            return;
        }

        if (runtime.Process.HasExited)
        {
            return;
        }

        if (mode == GameStopMode.Gentle)
        {
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
                AppendLifecycle(runtime, GameLogLevel.Warn, "Graceful shutdown is unavailable for this process. Force Stop is still available.");
                RestoreRunningState(runtime);
                return;
            }

            if (!await WaitForExitAsync(runtime.Process, TimeSpan.FromSeconds(5)))
            {
                AppendLifecycle(runtime, GameLogLevel.Warn, "The game did not exit in time. Force Stop is still available.");
                RestoreRunningState(runtime);
            }

            return;
        }

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

    private void ConfigureProcess(Process process, ActiveSessionRuntime runtime)
    {
        process.EnableRaisingEvents = true;

        if (!runtime.LogCaptureEnabled)
        {
            runtime.CanReadStandardStreams = false;
            return;
        }

        var canRedirect = string.IsNullOrWhiteSpace(process.StartInfo.Verb);
        if (!canRedirect)
        {
            runtime.CanReadStandardStreams = false;
            return;
        }

        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.StandardOutputEncoding = Encoding.UTF8;
        process.StartInfo.StandardErrorEncoding = Encoding.UTF8;
        runtime.CanReadStandardStreams = true;
    }

    private void AttachProcessHandlers(ActiveSessionRuntime runtime)
    {
        if (runtime.Process == null)
        {
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
    }

    private async Task HandleProcessExitAsync(ActiveSessionRuntime runtime)
    {
        if (runtime.Process == null)
        {
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
            await runtime.Process.WaitForExitAsync();
            runtime.Cancellation.Cancel();

            if (runtime.TailTask != null)
            {
                try
                {
                    await runtime.TailTask;
                }
                catch (OperationCanceledException)
                {
                }
            }

            FlushPendingAssemblers(runtime);

            var crashReports = FindNewCrashReports(runtime);
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

    private async Task TailLatestLogAsync(ActiveSessionRuntime runtime, CancellationToken cancellationToken)
    {
        var latestLogPath = Path.Combine(runtime.Session.GamePath, "logs", "latest.log");
        long position = File.Exists(latestLogPath) ? new FileInfo(latestLogPath).Length : 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (!File.Exists(latestLogPath))
                {
                    await Task.Delay(250, cancellationToken);
                    continue;
                }

                using var stream = new FileStream(latestLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                if (stream.Length < position)
                {
                    position = 0;
                }

                stream.Seek(position, SeekOrigin.Begin);
                using var reader = new StreamReader(stream, Encoding.UTF8);
                string? line;
                bool readAnything = false;
                while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
                {
                    readAnything = true;
                    AppendCapturedLine(runtime, line, GameLogSource.FileTail);
                }

                position = stream.Position;

                if (readAnything && runtime.CanReadStandardStreams)
                {
                    RunOnUI(() => runtime.Session.CaptureMode = GameCaptureMode.Hybrid);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to tail latest.log for {GameName}", runtime.Session.DisplayName);
            }

            await Task.Delay(250, cancellationToken);
        }
    }

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

    private void FlushPendingAssemblers(ActiveSessionRuntime runtime)
    {
        List<GameLogEntry> pendingEntries;
        lock (runtime.LogGate)
        {
            pendingEntries = runtime.Assemblers.Values
                .SelectMany(assembler => assembler.FlushPending(DateTimeOffset.Now, includeXmlFallback: true))
                .ToList();
        }

        foreach (var entry in pendingEntries)
        {
            PublishEntry(runtime, entry);
        }
    }

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

    private void AppendEntry(ActiveSessionRuntime runtime, GameLogEntry entry)
    {
        RunOnUI(() =>
        {
            runtime.Session.Entries.Add(entry);
            UpdateSessionLogSummary(runtime.Session);

            if (entry.Source == GameLogSource.FileTail && runtime.LogCaptureEnabled && !runtime.CanReadStandardStreams)
            {
                runtime.Session.CaptureMode = GameCaptureMode.FileOnly;
            }
        });
    }

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

    private static void UpdateSessionLogSummary(GameSession session)
    {
        session.EntryCount = session.Entries.Count;
        session.LastMessagePreview = session.Entries.LastOrDefault()?.Message.Trim();
    }

    private void CompleteFailedLaunch(ActiveSessionRuntime runtime, GameRunState state, Exception? ex = null)
    {
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

    private void RestoreRunningState(ActiveSessionRuntime runtime)
    {
        if (runtime.Process == null || runtime.Process.HasExited)
        {
            return;
        }

        runtime.RequestedStopMode = null;

        RunOnUI(() =>
        {
            runtime.Session.State = GameRunState.Running;
            ApplyActiveState(runtime.Session.Game, runtime.Session, runtime.Session.ProcessId);
        });
    }

    private void ApplyActiveState(Game game, GameSession session, int? processId)
    {
        game.HasActiveSession = true;
        game.ActiveProcessId = processId;
        game.RunState = session.State;
        game.LastExitCode = null;
        game.LastRunEndedAt = null;
    }

    private void ApplyInactiveState(Game game, GameSession session)
    {
        game.HasActiveSession = false;
        game.ActiveProcessId = null;
        game.RunState = session.State;
        game.LastExitCode = session.ExitCode;
        game.LastRunEndedAt = session.EndedAt;
    }

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

    private HashSet<string> SnapshotCrashReports(string gamePath)
    {
        var crashDirectory = Path.Combine(gamePath, "crash-reports");
        if (!Directory.Exists(crashDirectory))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return Directory.EnumerateFiles(crashDirectory).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

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
