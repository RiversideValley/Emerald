using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Emerald.CoreX.CrashHandling;
using Microsoft.Extensions.Logging;

namespace Emerald.Services;

public sealed class CrashCoordinator
{
    private readonly ICrashReportStore _store;
    private readonly IAppLifecycleTracker _lifecycle;
    private readonly IPlatformDiagnosticsProvider _platformDiagnostics;
    private readonly CrashEnvironment _environment;
    private readonly string _applicationLogPath;
    private readonly IProcessTerminator _processTerminator;
    private readonly object _startGate = new();
    private int _fatalCaptureStarted;
    private int _fatalCaptureCompleted;
    private int _fatalTerminationRequested;
    private int _fatalTerminationStarted;
    private CrashRecord? _capturedRecord;
    private int _processHandlersRegistered;
    private LifecycleStartResult? _startResult;
    private ILogger<CrashCoordinator>? _logger;

    public CrashCoordinator(
        ICrashReportStore store,
        IAppLifecycleTracker lifecycle,
        IPlatformDiagnosticsProvider platformDiagnostics,
        CrashEnvironment environment,
        string applicationLogPath,
        IProcessTerminator? processTerminator = null)
    {
        _store = store;
        _lifecycle = lifecycle;
        _platformDiagnostics = platformDiagnostics;
        _environment = environment;
        _applicationLogPath = applicationLogPath;
        _processTerminator = processTerminator ?? new EnvironmentProcessTerminator();
    }

    public ICrashReportStore Store => _store;
    public string ReportsPath => _store.ReportsPath;
    public string ApplicationLogPath => _applicationLogPath;
    public string CurrentRunId => _lifecycle.CurrentRunId;
    public bool IsRecoveryMode => _startResult?.IsRecoveryMode == true;
    public bool HasPreviousUnexpectedRun => _startResult?.PreviousRun is not null;

    public void SetLogger(ILogger<CrashCoordinator> logger)
        => _logger = logger;

    public void RegisterProcessHandlers()
    {
        if (Interlocked.Exchange(ref _processHandlersRegistered, 1) != 0)
        {
            return;
        }

        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
    }

    public void BeginRun()
    {
        lock (_startGate)
        {
            if (_startResult is not null)
            {
                return;
            }

            using var startResult = _lifecycle.BeginRun();
            _startResult = startResult;
            foreach (var previousRun in startResult.PreviousRuns)
            {
                if (previousRun.CleanShutdown)
                {
                    _lifecycle.MarkRunReconciled(previousRun.RunId);
                    continue;
                }

                if (_store.HasReportForRun(previousRun.RunId))
                {
                    _lifecycle.MarkRunReconciled(previousRun.RunId);
                    continue;
                }

                var record = CrashRecord.CreateUnexpectedShutdown(
                    previousRun,
                    _environment,
                    CrashLogTailReader.Read(_applicationLogPath));
                if (_store.TryWrite(record))
                {
                    _lifecycle.MarkRunReconciled(previousRun.RunId);
                }
            }
        }
    }

    public CrashRecord? CaptureManaged(Exception? exception, string source)
    {
        if (Interlocked.Exchange(ref _fatalCaptureStarted, 1) != 0)
        {
            return _capturedRecord;
        }

        try
        {
            _lifecycle.MarkFatalFailure();
            var occurredUtc = DateTimeOffset.UtcNow;
            var record = new CrashRecord
            {
                RunId = CurrentRunId,
                OccurredUtc = occurredUtc,
                Kind = CrashRecordKind.ManagedCrash,
                Source = source,
                AppVersion = _environment.AppVersion,
                PackageVersion = _environment.PackageVersion,
                BuildChannel = _environment.BuildChannel,
                ReleaseTag = _environment.ReleaseTag,
                CommitSha = _environment.CommitSha,
                BuildTimestampUtc = _environment.BuildTimestampUtc,
                Platform = _environment.Platform,
                OperatingSystem = _environment.OperatingSystem,
                Architecture = _environment.Architecture,
                Runtime = _environment.Runtime,
                Exception = exception is null ? null : CrashExceptionInfo.FromException(exception),
                ApplicationLogTail = CrashLogTailReader.Read(_applicationLogPath),
                NativeDiagnosticsStatus = "Pending next launch"
            };

            if (!_store.TryWriteFatal(record))
            {
                Debug.WriteLine($"[CRASH WRITE FAILED] {source}: {exception}");
            }

            try
            {
                _logger?.LogCritical(exception, "Captured fatal Emerald exception from {Source}.", source);
            }
            catch
            {
                // The report is already persisted; logging must not interfere with termination.
            }

            _capturedRecord = record;
            return record;
        }
        catch (Exception captureException)
        {
            Debug.WriteLine($"[CRASH CAPTURE FAILED] {source}: {captureException}");
            return null;
        }
        finally
        {
            Volatile.Write(ref _fatalCaptureCompleted, 1);
            TerminateAfterCaptureIfRequested();
        }
    }

    public void ObserveBackgroundFault(Exception exception, string source)
    {
        try
        {
            _logger?.LogError(exception, "Observed background task failure from {Source}.", source);
            Debug.WriteLine($"[BACKGROUND TASK FAILURE] {source}: {exception}");
        }
        catch
        {
            // An ILogger provider is application code too; it must not turn a
            // recoverable background fault into an unhandled exception.
        }
    }

    public IReadOnlyList<CrashRecord> GetReports()
        => _store.GetAll();

    public IReadOnlyList<CrashRecord> GetUnacknowledgedReports()
        => _store.GetAll().Where(record => !record.IsAcknowledged).ToArray();

    public bool Acknowledge(string id)
        => _store.TryAcknowledge(id);

    public bool Delete(string id)
        => _store.TryDelete(id);

    public int DeleteAll()
        => _store.DeleteAll();

    public void EnrichNativeDiagnostics()
    {
        foreach (var record in GetReports().Where(report =>
                     string.Equals(report.NativeDiagnosticsStatus, "Pending next launch", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(report.NativeDiagnosticsStatus, "Unavailable", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                var diagnostics = _platformDiagnostics.FindRecent(record.OccurredUtc);
                record.NativeDiagnosticsStatus = diagnostics.Status;
                record.NativeDiagnosticsPath = diagnostics.Path;
                _store.TryWrite(record);
            }
            catch
            {
            }
        }
    }

    public void MarkStartupComplete()
        => _lifecycle.MarkStartupComplete();

    public void MarkNormalStartupAttempted()
        => _lifecycle.MarkNormalStartupAttempted();

    public void MarkCleanExit()
        => _lifecycle.MarkCleanExit();

    public void CaptureAndTerminate(Exception? exception, string source)
    {
        Interlocked.Exchange(ref _fatalTerminationRequested, 1);
        CaptureManaged(exception, source);
        TerminateAfterCaptureIfRequested();
    }

    private void TerminateAfterCaptureIfRequested()
    {
        // A competing callback requests termination without interrupting the
        // winning writer. That writer terminates in its finally block, including
        // when persistence fails. Capture-only notifications remain capture-only.
        if (Volatile.Read(ref _fatalCaptureCompleted) == 0
            || Volatile.Read(ref _fatalTerminationRequested) == 0
            || Interlocked.Exchange(ref _fatalTerminationStarted, 1) != 0)
        {
            return;
        }

        _processTerminator.TerminateFatal(_capturedRecord?.Id ?? "unavailable");
    }

    private void CurrentDomain_UnhandledException(object? sender, System.UnhandledExceptionEventArgs e)
    {
        Exception exception;
        if (e.ExceptionObject is Exception typedException)
        {
            exception = typedException;
        }
        else
        {
            string description;
            try
            {
                description = Convert.ToString(e.ExceptionObject) ?? "Unknown unhandled exception object.";
            }
            catch
            {
                description = "Unknown unhandled exception object.";
            }

            exception = new Exception(description);
        }

        CaptureManaged(exception, "AppDomain.UnhandledException");
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
        ObserveBackgroundFault(e.Exception, "TaskScheduler.UnobservedTaskException");
        CrashFaultInjection.WriteCheckpoint("Unobserved task observed");
    }
}

public interface IProcessTerminator
{
    void TerminateFatal(string reportId);
}

public sealed class EnvironmentProcessTerminator : IProcessTerminator
{
    public void TerminateFatal(string reportId)
        => Environment.FailFast($"Emerald terminated after an unrecoverable failure. Report: {reportId}");
}

public static class CrashBootstrap
{
    private static readonly object Gate = new();
    private static CrashCoordinator? _current;
    private static Action? _normalShutdown;

    public static CrashCoordinator Current
    {
        get
        {
            lock (Gate)
            {
                return _current ??= Create();
            }
        }
    }

    public static CrashCoordinator Initialize()
    {
        var current = Current;
        try
        {
            NativeDispatcherFatalLoggerProvider.InstallEarly(current);
            current.RegisterProcessHandlers();
            current.BeginRun();
        }
        catch (Exception exception)
        {
            // Bootstrap must not prevent the application from reaching its own
            // fatal policy when a platform event hookup is unavailable.
            Debug.WriteLine($"[CRASH BOOTSTRAP FAILED] {exception}");
        }

        return current;
    }

    public static void RegisterNormalShutdown(Action shutdown)
        => Interlocked.Exchange(ref _normalShutdown, shutdown);

    public static void RequestNormalShutdown()
    {
        if (_normalShutdown is not null)
        {
            _normalShutdown();
            return;
        }

        Environment.Exit(0);
    }

    private static CrashCoordinator Create()
    {
        try
        {
            var localDataPath = ResolveCrashDataPath();
            var environment = new CrashEnvironment(
                DirectResoucres.PublicVersion,
                DirectResoucres.PackageVersion,
                DirectResoucres.ReleaseChannel.ToString(),
                DirectResoucres.ReleaseTag,
                DirectResoucres.CommitSha,
                DirectResoucres.BuildTimestampUtc,
                DirectResoucres.Platform,
                RuntimeInformation.OSDescription,
                RuntimeInformation.ProcessArchitecture.ToString(),
                RuntimeInformation.FrameworkDescription);

            var fallbackDataPath = ResolveFallbackCrashDataPath(localDataPath);
            return new CrashCoordinator(
                new FallbackCrashReportStore(
                    new FileCrashReportStore(localDataPath),
                    new FileCrashReportStore(fallbackDataPath)),
                new FileAppLifecycleTracker(localDataPath, environment),
                new PlatformDiagnosticsProvider(),
                environment,
                Path.Combine(localDataPath, "logs", "app_.log"));
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"[CRASH COORDINATOR CREATE FAILED] {exception}");
            var emergencyPath = Path.Combine(Path.GetTempPath(), "Emerald");
            var environment = new CrashEnvironment(
                "unknown",
                "unknown",
                "unknown",
                string.Empty,
                string.Empty,
                string.Empty,
                "unknown",
                RuntimeInformation.OSDescription,
                RuntimeInformation.ProcessArchitecture.ToString(),
                RuntimeInformation.FrameworkDescription);
            return new CrashCoordinator(
                new FileCrashReportStore(emergencyPath),
                new FileAppLifecycleTracker(emergencyPath, environment),
                new PlatformDiagnosticsProvider(),
                environment,
                Path.Combine(emergencyPath, "logs", "app_.log"));
        }
    }

    private static string ResolveCrashDataPath()
    {
        // Test sessions must never discover or modify the real profile's reports.
        if (!string.IsNullOrWhiteSpace(CrashFaultInjection.DataRoot))
        {
            return Path.GetFullPath(CrashFaultInjection.DataRoot);
        }

        var candidates = new List<string>();
        try { candidates.Add(DirectResoucres.LocalDataPath); } catch { }
        try
        {
            var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrWhiteSpace(localApplicationData))
            {
                candidates.Add(Path.Combine(localApplicationData, "Emerald"));
            }
        }
        catch { }

        candidates.Add(Path.Combine(Path.GetTempPath(), "Emerald"));
        foreach (var candidate in candidates.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                Directory.CreateDirectory(candidate);
                var probe = Path.Combine(candidate, ".crash-write-probe");
                using (var stream = new FileStream(probe, FileMode.Create, FileAccess.Write, FileShare.Read, 1, FileOptions.WriteThrough))
                {
                    stream.WriteByte(1);
                    stream.Flush(true);
                }
                File.Delete(probe);
                return candidate;
            }
            catch { }
        }

        return Path.Combine(Path.GetTempPath(), "Emerald");
    }

    private static string ResolveFallbackCrashDataPath(string primaryPath)
    {
        if (!string.IsNullOrWhiteSpace(CrashFaultInjection.DataRoot))
        {
            return Path.Combine(primaryPath, "diagnostics-fallback");
        }

        try
        {
            var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var fallback = string.IsNullOrWhiteSpace(localApplicationData)
                ? string.Empty
                : Path.Combine(localApplicationData, "Emerald");
            if (!string.IsNullOrWhiteSpace(fallback)
                && !string.Equals(fallback, primaryPath, StringComparison.OrdinalIgnoreCase))
            {
                return fallback;
            }
        }
        catch
        {
        }

        return Path.Combine(Path.GetTempPath(), "Emerald");
    }
}
