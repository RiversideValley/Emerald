using System.Text.Json;

namespace Emerald.CoreX.CrashHandling;

public interface IAppLifecycleTracker : IDisposable
{
    string CurrentRunId { get; }
    LifecycleStartResult BeginRun();
    void MarkRunReconciled(string runId);
    void MarkNormalStartupAttempted();
    void MarkStartupComplete();
    void MarkFatalFailure();
    void MarkCleanExit();
}

public sealed class LifecycleStartResult : IDisposable
{
    private IDisposable? _reconciliationLease;

    public LifecycleRunState? PreviousRun { get; init; }
    public IReadOnlyList<LifecycleRunState> PreviousRuns { get; init; } = [];
    public int ConsecutiveEarlyFailures { get; init; }
    public bool IsRecoveryMode => ConsecutiveEarlyFailures >= 3;

    internal void AttachReconciliationLease(IDisposable? lease)
        => _reconciliationLease = lease;

    public void Dispose()
        => Interlocked.Exchange(ref _reconciliationLease, null)?.Dispose();
}

public sealed class LifecycleRunState
{
    public string RunId { get; set; } = string.Empty;
    public DateTimeOffset StartedUtc { get; set; }
    public DateTimeOffset LastHeartbeatUtc { get; set; }
    public bool NormalStartupAttempted { get; set; }
    public bool StartupCompleted { get; set; }
    public bool FatalFailure { get; set; }
    public bool CleanShutdown { get; set; }
    public bool Reconciled { get; set; }
    public bool RecoveryOnlySession { get; set; }
    public int ConsecutiveEarlyFailures { get; set; }
    public int OwnerProcessId { get; set; }
    public string AppVersion { get; set; } = string.Empty;
    public string PackageVersion { get; set; } = string.Empty;
    public string BuildChannel { get; set; } = string.Empty;
    public string ReleaseTag { get; set; } = string.Empty;
    public string CommitSha { get; set; } = string.Empty;
    public string BuildTimestampUtc { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string OperatingSystem { get; set; } = string.Empty;
    public string Architecture { get; set; } = string.Empty;
    public string Runtime { get; set; } = string.Empty;
}

/// <summary>
/// Uses one marker per run. This prevents a second Emerald instance from overwriting
/// the first instance's state and keeps stale-run reconciliation idempotent.
/// </summary>
public sealed class FileAppLifecycleTracker : IAppLifecycleTracker
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly object _gate = new();
    private readonly string _sessionsPath;
    private readonly string _legacyMarkerPath;
    private readonly CrashEnvironment? _environment;
    private Timer? _heartbeatTimer;
    private FileStream? _ownershipStream;
    private FileStream? _reconciliationLease;
    private LifecycleRunState? _currentRun;

    public FileAppLifecycleTracker(string localDataPath, CrashEnvironment? environment = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localDataPath);
        _sessionsPath = Path.Combine(localDataPath, "crashes", "sessions");
        _legacyMarkerPath = Path.Combine(localDataPath, "crashes", "lifecycle.json");
        _environment = environment;
    }

    public string CurrentRunId => _currentRun?.RunId ?? string.Empty;

    public LifecycleStartResult BeginRun()
    {
        lock (_gate)
        {
            if (_currentRun is not null)
            {
                return BuildStartResult(null, [], _currentRun.ConsecutiveEarlyFailures);
            }

            FileStream? reconciliationLock = AcquireReconciliationLock();
            var previousRuns = ReadUnreconciledRuns();
            var previous = previousRuns
                .OrderByDescending(run => run.LastHeartbeatUtc)
                .FirstOrDefault();
            var earlyFailures = CalculateEarlyFailures(previous);

            var runId = Guid.NewGuid().ToString("N");
            _currentRun = new LifecycleRunState
            {
                RunId = runId,
                StartedUtc = DateTimeOffset.UtcNow,
                LastHeartbeatUtc = DateTimeOffset.UtcNow,
                ConsecutiveEarlyFailures = earlyFailures,
                RecoveryOnlySession = earlyFailures >= 3,
                OwnerProcessId = Environment.ProcessId,
                AppVersion = _environment?.AppVersion ?? string.Empty,
                PackageVersion = _environment?.PackageVersion ?? string.Empty,
                BuildChannel = _environment?.BuildChannel ?? string.Empty,
                ReleaseTag = _environment?.ReleaseTag ?? string.Empty,
                CommitSha = _environment?.CommitSha ?? string.Empty,
                BuildTimestampUtc = _environment?.BuildTimestampUtc ?? string.Empty,
                Platform = _environment?.Platform ?? string.Empty,
                OperatingSystem = _environment?.OperatingSystem ?? string.Empty,
                Architecture = _environment?.Architecture ?? string.Empty,
                Runtime = _environment?.Runtime ?? string.Empty
            };

            AcquireOwnership(runId);
            TryWrite(_currentRun);

            _heartbeatTimer = new Timer(_ => Heartbeat(), null, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15));

            if (previousRuns.Count == 0)
            {
                reconciliationLock?.Dispose();
                reconciliationLock = null;
            }
            else
            {
                _reconciliationLease = reconciliationLock;
            }

            var result = BuildStartResult(previous, previousRuns, earlyFailures);
            result.AttachReconciliationLease(reconciliationLock);
            return result;
        }
    }

    /// <summary>
    /// Retires a previous run only after the caller has persisted any recovery
    /// evidence for it. Keeping this separate from BeginRun prevents a failed
    /// report write from silently losing the stale-session signal.
    /// </summary>
    public void MarkRunReconciled(string runId)
    {
        if (string.IsNullOrWhiteSpace(runId))
        {
            return;
        }

        lock (_gate)
        {
            var sessionPath = Path.Combine(_sessionsPath, $"{runId}.json");
            var state = TryRead(sessionPath);
            if (state is not null)
            {
                state.Reconciled = true;
                TryWriteAt(sessionPath, state);
            }

            var legacy = TryRead(_legacyMarkerPath);
            if (legacy is not null && string.Equals(legacy.RunId, runId, StringComparison.Ordinal))
            {
                legacy.Reconciled = true;
                TryWriteAt(_legacyMarkerPath, legacy);
            }
        }
    }

    public void MarkNormalStartupAttempted()
    {
        lock (_gate)
        {
            if (_currentRun is null || _currentRun.NormalStartupAttempted)
            {
                return;
            }

            _currentRun.NormalStartupAttempted = true;
            Heartbeat();
        }
    }

    public void MarkStartupComplete()
    {
        lock (_gate)
        {
            if (_currentRun is null || _currentRun.FatalFailure)
            {
                return;
            }

            _currentRun.NormalStartupAttempted = true;
            _currentRun.StartupCompleted = true;
            _currentRun.ConsecutiveEarlyFailures = 0;
            _currentRun.RecoveryOnlySession = false;
            Heartbeat();
        }
    }

    public void MarkFatalFailure()
    {
        lock (_gate)
        {
            if (_currentRun is null || _currentRun.FatalFailure)
            {
                return;
            }

            _currentRun.FatalFailure = true;
            _currentRun.CleanShutdown = false;
            TryWrite(_currentRun);
        }
    }

    public void MarkCleanExit()
    {
        lock (_gate)
        {
            if (_currentRun is null || _currentRun.FatalFailure)
            {
                return;
            }

            _currentRun.CleanShutdown = true;
            _currentRun.LastHeartbeatUtc = DateTimeOffset.UtcNow;
            if (TryWrite(_currentRun))
            {
                _heartbeatTimer?.Dispose();
                _heartbeatTimer = null;
                ReleaseOwnership();
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _heartbeatTimer?.Dispose();
            _heartbeatTimer = null;
            _reconciliationLease?.Dispose();
            _reconciliationLease = null;
            ReleaseOwnership();
        }
    }

    private static int CalculateEarlyFailures(LifecycleRunState? previous)
    {
        if (previous is null)
        {
            return 0;
        }

        if (previous.RecoveryOnlySession && !previous.NormalStartupAttempted)
        {
            return previous.ConsecutiveEarlyFailures;
        }

        return previous.NormalStartupAttempted && !previous.StartupCompleted
            ? previous.ConsecutiveEarlyFailures + 1
            : 0;
    }

    private LifecycleStartResult BuildStartResult(
        LifecycleRunState? previous,
        IReadOnlyList<LifecycleRunState> previousRuns,
        int earlyFailures)
        => new()
        {
            PreviousRun = previous,
            PreviousRuns = previousRuns,
            ConsecutiveEarlyFailures = earlyFailures
        };

    private List<LifecycleRunState> ReadUnreconciledRuns()
    {
        var runs = new List<LifecycleRunState>();
        try
        {
            if (Directory.Exists(_sessionsPath))
            {
                foreach (var path in Directory.EnumerateFiles(_sessionsPath, "*.json"))
                {
                    var state = TryRead(path);
                    if (state is not null
                        && !state.Reconciled
                        && (!state.CleanShutdown || (state.RecoveryOnlySession && !state.StartupCompleted))
                        && CanAcquireStaleOwnership(state.RunId))
                    {
                        runs.Add(state);
                    }
                }
            }

            // One-time compatibility read for the previous single-marker format.
            var legacy = TryRead(_legacyMarkerPath);
            if (legacy is not null
                && !legacy.Reconciled
                && (!legacy.CleanShutdown || (legacy.RecoveryOnlySession && !legacy.StartupCompleted))
                && !runs.Any(run => run.RunId == legacy.RunId))
            {
                runs.Add(legacy);
            }
        }
        catch
        {
        }

        return runs;
    }

    private void AcquireOwnership(string runId)
    {
        try
        {
            Directory.CreateDirectory(_sessionsPath);
            var ownershipPath = Path.Combine(_sessionsPath, $"{runId}.lock");
            _ownershipStream = new FileStream(ownershipPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        }
        catch
        {
            _ownershipStream = null;
        }
    }

    private FileStream? AcquireReconciliationLock()
    {
        try
        {
            Directory.CreateDirectory(_sessionsPath);
            var lockPath = Path.Combine(_sessionsPath, "reconcile.lock");
            for (var attempt = 0; attempt < 100; attempt++)
            {
                try
                {
                    return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                }
                catch (IOException)
                {
                    Thread.Sleep(20);
                }
            }
        }
        catch
        {
        }

        return null;
    }

    private bool CanAcquireStaleOwnership(string runId)
    {
        try
        {
            var lockPath = Path.Combine(_sessionsPath, $"{runId}.lock");
            if (!File.Exists(lockPath))
            {
                return true;
            }

            using var probe = new FileStream(lockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void ReleaseOwnership()
    {
        _ownershipStream?.Dispose();
        _ownershipStream = null;
    }

    private void Heartbeat()
    {
        lock (_gate)
        {
            if (_currentRun is null || _currentRun.CleanShutdown || _currentRun.FatalFailure)
            {
                return;
            }

            _currentRun.LastHeartbeatUtc = DateTimeOffset.UtcNow;
            TryWrite(_currentRun);
        }
    }

    private LifecycleRunState? TryRead(string path)
    {
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<LifecycleRunState>(File.ReadAllText(path), JsonOptions)
                : null;
        }
        catch
        {
            return null;
        }
    }

    private bool TryWrite(LifecycleRunState state)
        => TryWriteAt(Path.Combine(_sessionsPath, $"{state.RunId}.json"), state);

    private static bool TryWriteAt(string path, LifecycleRunState state)
    {
        try
        {
            AtomicFile.WriteText(path, JsonSerializer.Serialize(state, JsonOptions));
            return true;
        }
        catch
        {
            return false;
        }
    }
}
