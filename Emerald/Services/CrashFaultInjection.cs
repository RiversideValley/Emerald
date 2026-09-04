namespace Emerald.Services;

/// <summary>
/// Development-only fault injection, once per process. A crash profile is rearmed
/// on every launch. Normal Desktop startup does not arm another fault.
/// </summary>
internal static class CrashFaultInjection
{
    private static int _fired;
    private static int _argumentTestMode = -1;
    private static int _argumentDataRootConfigured;
    private static int _argumentDisableStudio;
    private static string? _argumentCrashPoint;
    private static string? _argumentDataRoot;
    private static string? _argumentRecoveryAction;

    public static string? DataRoot
    {
        get
        {
            if (!IsEnabled) return null;
            return Volatile.Read(ref _argumentDataRootConfigured) == 1
                ? Volatile.Read(ref _argumentDataRoot)
                : Environment.GetEnvironmentVariable("EMERALD_TEST_DATA_ROOT");
        }
    }

    public static bool IsArmed => IsEnabled
        && !string.IsNullOrWhiteSpace(GetCrashPoint());

    public static bool DisableStudio => IsEnabled
        && (Volatile.Read(ref _argumentDisableStudio) == 1
            || string.Equals(Environment.GetEnvironmentVariable("EMERALD_TEST_DISABLE_STUDIO"), "1", StringComparison.Ordinal));

    public static bool IsEnabled
    {
        get
        {
#if DEBUG
            var argumentMode = Volatile.Read(ref _argumentTestMode);
            if (argumentMode >= 0)
            {
                return argumentMode == 1;
            }

            return string.Equals(
                Environment.GetEnvironmentVariable("EMERALD_TEST"),
                "1",
                StringComparison.Ordinal);
#else
            return false;
#endif
        }
    }

    /// <summary>
    /// Reads Debug-only test options from process or packaged activation arguments.
    /// Packaged WinAppSDK launch profiles do not reliably receive launchSettings
    /// environment variables, so they use the same explicit gate as the environment
    /// flow via --emerald-test=1.
    /// </summary>
    public static void ConfigureFromArguments(IEnumerable<string> arguments)
    {
#if DEBUG
        foreach (var argument in arguments)
        {
            if (TryReadOption(argument, "--emerald-test", out var testMode))
            {
                Volatile.Write(ref _argumentTestMode, string.Equals(testMode, "1", StringComparison.Ordinal) ? 1 : 0);
            }
            else if (TryReadOption(argument, "--emerald-test-crash", out var crashPoint))
            {
                Volatile.Write(ref _argumentCrashPoint, crashPoint);
            }
            else if (TryReadOption(argument, "--emerald-test-data-root", out var dataRoot))
            {
                Volatile.Write(ref _argumentDataRoot, dataRoot);
                Volatile.Write(ref _argumentDataRootConfigured, 1);
            }
            else if (TryReadOption(argument, "--emerald-test-disable-studio", out var disableStudio))
            {
                Volatile.Write(ref _argumentDisableStudio, string.Equals(disableStudio, "1", StringComparison.Ordinal) ? 1 : 0);
            }
            else if (TryReadOption(argument, "--emerald-test-recovery-action", out var recoveryAction))
            {
                Volatile.Write(ref _argumentRecoveryAction, recoveryAction);
            }
        }
#endif
    }

    public static void ConfigureFromActivationArguments(string? arguments)
    {
#if DEBUG
        if (string.IsNullOrWhiteSpace(arguments)) return;
        ConfigureFromArguments(arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
#endif
    }

    public static bool IsRequested(string point)
    {
#if DEBUG
        if (!IsArmed
            || !string.Equals(GetCrashPoint(), point, StringComparison.Ordinal))
        {
            return false;
        }

        if (Interlocked.Exchange(ref _fired, 1) != 0)
        {
            return false;
        }

        WriteCheckpoint($"Firing {point}");
        return true;
#else
        return false;
#endif
    }

    [System.Diagnostics.Conditional("DEBUG")]
    public static void ExerciseAdditionalPaths(Microsoft.UI.Dispatching.DispatcherQueue queue,
        Microsoft.Extensions.Logging.ILogger logger)
    {
#if DEBUG
        if (IsRequested("AsyncVoidBeforeAwait"))
        {
            queue.TryEnqueue(() => ThrowAsyncVoid(afterAwait: false));
        }
        else if (IsRequested("AsyncVoidAfterAwait"))
        {
            queue.TryEnqueue(() => ThrowAsyncVoid(afterAwait: true));
        }
        else if (IsRequested("WorkerThread"))
        {
            new Thread(() => throw new NotImplementedException("Intentional worker-thread crash test.")).Start();
        }
        else if (IsRequested("CaptureThenTerminate"))
        {
            try
            {
                throw new NotImplementedException("Intentional capture-then-terminate crash test.");
            }
            catch (Exception exception)
            {
                CrashBootstrap.Current.CaptureManaged(exception, "Capture-then-terminate test");
                CrashBootstrap.Current.CaptureAndTerminate(exception, "Capture-then-terminate test");
            }
        }
        else if (IsRequested("OrdinaryError"))
        {
            logger.LogError(new NotImplementedException("Intentional recoverable test error."), "Ordinary error test");
            WriteCheckpoint("Ordinary error logged");
        }
        else if (IsRequested("UnobservedTask"))
        {
            _ = CollectUnobservedTaskAsync();
        }
#endif
    }

#if DEBUG
    private static async void ThrowAsyncVoid(bool afterAwait)
    {
        if (!afterAwait)
        {
            throw new NotImplementedException("Intentional async-void before-await crash test.");
        }

        await Task.Delay(20);
        throw new NotImplementedException("Intentional async-void after-await crash test.");
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference CreateUnobservedTask()
    {
        var task = Task.Run(() => throw new NotImplementedException("Intentional unobserved task test."));
        SpinWait.SpinUntil(() => task.IsCompleted);
        return new WeakReference(task);
    }

    private static async Task CollectUnobservedTaskAsync()
    {
        var task = CreateUnobservedTask();
        for (var attempt = 0; attempt < 20 && task.IsAlive; attempt++)
        {
            await Task.Delay(50);
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }
#endif

    [System.Diagnostics.Conditional("DEBUG")]
    public static void ExerciseRecoveryActions(Microsoft.UI.Dispatching.DispatcherQueue queue,
        Action viewDetails, Action continueStartup)
    {
#if DEBUG
        // Automated UI actions are allowed only in an explicitly isolated test run.
        if (string.IsNullOrWhiteSpace(DataRoot)) return;
        var action = Volatile.Read(ref _argumentRecoveryAction)
            ?? Environment.GetEnvironmentVariable("EMERALD_TEST_RECOVERY_ACTION");
        if (action != "view-continue" && action != "continue") return;
        queue.TryEnqueue(() =>
        {
            if (action == "view-continue") viewDetails();
            queue.TryEnqueue(() => continueStartup());
        });
#endif
    }

    [System.Diagnostics.Conditional("DEBUG")]
    public static void WriteCheckpoint(string checkpoint)
    {
        if (IsEnabled)
        {
            try { Console.Error.WriteLine($"[EMERALD TEST] {checkpoint}"); } catch { }
        }
    }

#if DEBUG
    private static string? GetCrashPoint() => Volatile.Read(ref _argumentCrashPoint)
        ?? Environment.GetEnvironmentVariable("EMERALD_TEST_CRASH");

    private static bool TryReadOption(string argument, string name, out string value)
    {
        var prefix = name + "=";
        if (argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            value = argument[prefix.Length..].Trim('"');
            return true;
        }

        value = string.Empty;
        return false;
    }
#else
    private static string? GetCrashPoint() => null;
#endif
}
