using System.Collections.Concurrent;
using System.Diagnostics;
using Emerald.CoreX.CrashHandling;
using Xunit;

namespace Emerald.CoreX.Tests.CrashHandling;

// Opt in with EMERALD_PROCESS_TEST_APP pointing to the built Debug apphost.
// These tests launch real windows and deliberately terminate their own processes.
[CollectionDefinition("Crash app processes", DisableParallelization = true)]
public sealed class CrashProcessCollection;

[Collection("Crash app processes")]
public sealed class CrashProfileProcessTests
{
    [AppProcessTheory]
    [InlineData("DesktopHost", "Desktop host")]
    [InlineData("WinAppStartup", "OnLaunched")]
    [InlineData("MainPage_Loaded", "MainPage.Loaded")]
    [InlineData("MainPage_Loaded_BeforeAwait", "MainPage.Loaded")]
    [InlineData("MainPage_Loaded_AfterAwait", "MainPage.Loaded")]
    [InlineData("DispatcherCallback", "Uno.NativeDispatcher")]
    [InlineData("AsyncVoidBeforeAwait", "Uno.NativeDispatcher")]
    [InlineData("AsyncVoidAfterAwait", "Uno.NativeDispatcher")]
    [InlineData("WorkerThread", "AppDomain.UnhandledException")]
    [InlineData("CaptureThenTerminate", "Capture-then-terminate test")]
    public async Task FatalProfile_RearmsEveryLaunch_PersistsOnce_ThenOpensRecovery(string point, string source)
    {
        using var directory = new TestDirectory();
        var store = new FileCrashReportStore(directory.Path);

        // Same directory on both launches: catches persistent .once marker bugs,
        // stale-session duplicates and recovery dialogs blocking a crash profile.
        for (var run = 1; run <= 2; run++)
        {
            await using var app = new TestApp(directory.Path, point);
            await app.WaitForExitAsync();
            Assert.NotEqual(0, app.ExitCode);
            Assert.True(app.Saw($"Firing {point}"), app.Output);
            var reports = store.GetAll();
            Assert.Equal(run, reports.Count);
            Assert.All(reports, report =>
            {
                Assert.Equal(CrashRecordKind.ManagedCrash, report.Kind);
                Assert.Equal(source, report.Source);
                Assert.Equal("System.NotImplementedException", report.Exception?.Type);
                Assert.Contains("Intentional", report.Exception?.Message ?? string.Empty);
                Assert.False(string.IsNullOrWhiteSpace(report.Exception?.StackTrace));
                Assert.False(report.IsAcknowledged);
                Assert.True(File.Exists(report.ReportPath));
            });
            Assert.Equal(run, reports.Select(report => report.RunId).Distinct().Count());
        }

        var newest = store.GetAll()[0];
        await using var recovery = new TestApp(directory.Path, string.Empty);
        await recovery.WaitForCheckpointAsync($"Recovery dialog opened: {newest.Id}");
        Assert.False(recovery.HasExited, recovery.Output);
        Assert.False(recovery.Saw("ShellReady"), recovery.Output);
        Assert.Equal(2, store.GetAll().Count);
        Assert.False(store.Get(newest.Id)!.IsAcknowledged);
    }

    [AppProcessFact]
    public async Task UnoApplicationUnhandled_CallbackCapturesOnce_ThenOpensRecovery()
    {
        using var directory = new TestDirectory();
        await using var app = new TestApp(directory.Path, "UnoApplicationUnhandled");

        await app.WaitForExitAsync();

        Assert.NotEqual(0, app.ExitCode);
        Assert.True(app.Saw("Firing UnoApplicationUnhandled"), app.Output);
        Assert.True(app.Saw("Uno Application.UnhandledException observed"), app.Output);

        var store = new FileCrashReportStore(directory.Path);
        var report = Assert.Single(store.GetAll());
        Assert.Equal(CrashRecordKind.ManagedCrash, report.Kind);
        Assert.Equal("Uno.Application.UnhandledException", report.Source);
        Assert.Equal("System.NotImplementedException", report.Exception?.Type);
        Assert.Contains("Intentional", report.Exception?.Message ?? string.Empty);
        Assert.False(report.IsAcknowledged);

        await using var recovery = new TestApp(directory.Path, string.Empty);
        await recovery.WaitForCheckpointAsync($"Recovery dialog opened: {report.Id}");
        Assert.False(recovery.HasExited, recovery.Output);
        Assert.False(recovery.Saw("ShellReady"), recovery.Output);
        Assert.False(store.Get(report.Id)!.IsAcknowledged);
    }

    [AppProcessTheory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task Recovery_ViewAndContinue_UsesOneDialog_ThenReachesShell(int failedLaunches)
    {
        using var directory = new TestDirectory();
        for (var run = 0; run < failedLaunches; run++)
        {
            await using var app = new TestApp(directory.Path, "MainPage_Loaded");
            await app.WaitForExitAsync();
            Assert.NotEqual(0, app.ExitCode);
        }

        var store = new FileCrashReportStore(directory.Path);
        var report = store.GetAll()[0];
        await using var recovery = new TestApp(directory.Path, string.Empty, "view-continue");
        await recovery.WaitForCheckpointAsync("ShellReady");
        Assert.True(recovery.Saw("Recovery details viewed"), recovery.Output);
        Assert.Equal(1, recovery.CheckpointCount($"Recovery dialog opened: {report.Id}"));
        Assert.True(store.Get(report.Id)!.IsAcknowledged);
        Assert.Equal(failedLaunches, store.GetAll().Count);
        Assert.False(recovery.HasExited, recovery.Output);
    }

    [AppProcessTheory]
    [InlineData("", "ShellReady")]
    [InlineData("OrdinaryError", "Ordinary error logged")]
    [InlineData("UnobservedTask", "Unobserved task observed")]
    public async Task NonFatalStartup_ReachesShellWithoutCrashReport(string point, string checkpoint)
    {
        using var directory = new TestDirectory();
        await using var app = new TestApp(directory.Path, point);
        await app.WaitForCheckpointAsync(checkpoint);
        await app.WaitForCheckpointAsync("ShellReady");
        await Task.Delay(250);
        Assert.False(app.HasExited, app.Output);
        Assert.Empty(new FileCrashReportStore(directory.Path).GetAll());
    }

    [AppProcessFact]
    public async Task PackagedStyleCommandLineArguments_RequireGate_AndTriggerStartupCrash()
    {
        using var directory = new TestDirectory();
        await using var app = new TestApp(directory.Path, "WinAppStartup", useArguments: true);
        await app.WaitForExitAsync();

        Assert.NotEqual(0, app.ExitCode);
        Assert.True(app.Saw("Firing WinAppStartup"), app.Output);
        var report = Assert.Single(new FileCrashReportStore(directory.Path).GetAll());
        Assert.Equal(CrashRecordKind.ManagedCrash, report.Kind);
        Assert.Equal("OnLaunched", report.Source);
        Assert.Equal("System.NotImplementedException", report.Exception?.Type);
    }

    private sealed class TestApp : IAsyncDisposable
    {
        private readonly Process _process;
        private readonly ConcurrentQueue<string> _output = new();
        private readonly ConcurrentDictionary<string, int> _checkpoints = new();

        public TestApp(string root, string point, string recoveryAction = "", bool useArguments = false)
        {
            var executable = Environment.GetEnvironmentVariable("EMERALD_PROCESS_TEST_APP")!;
            Assert.True(File.Exists(executable), $"Build the Debug desktop application first: {executable}");
            var start = new ProcessStartInfo(executable)
            {
                WorkingDirectory = System.IO.Path.GetDirectoryName(executable)!,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            if (useArguments)
            {
                // Mirrors the WinAppSDK packaged launch profiles. Keep the
                // environment disabled to prove the explicit argument gate wins.
                start.Environment["EMERALD_TEST"] = "0";
                start.ArgumentList.Add("--emerald-test=1");
                start.ArgumentList.Add($"--emerald-test-data-root={root}");
                start.ArgumentList.Add($"--emerald-test-crash={point}");
                start.ArgumentList.Add("--emerald-test-disable-studio=1");
            }
            else
            {
                start.Environment["EMERALD_TEST"] = "1";
                start.Environment["EMERALD_TEST_DATA_ROOT"] = root;
                start.Environment["EMERALD_TEST_CRASH"] = point;
                start.Environment["EMERALD_TEST_DISABLE_STUDIO"] = "1";
                start.Environment["EMERALD_TEST_RECOVERY_ACTION"] = recoveryAction;
            }
            _process = new Process { StartInfo = start };
            _process.OutputDataReceived += ReceiveLine;
            _process.ErrorDataReceived += ReceiveLine;
            Assert.True(_process.Start());
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
        }

        public bool HasExited => _process.HasExited;
        public int ExitCode => _process.ExitCode;
        public string Output => string.Join(Environment.NewLine, _output);
        public bool Saw(string checkpoint) => _checkpoints.ContainsKey(checkpoint);
        public int CheckpointCount(string checkpoint) => _checkpoints.GetValueOrDefault(checkpoint);

        private void ReceiveLine(object sender, DataReceivedEventArgs args)
        {
            if (args.Data is not { } line) return;
            const string prefix = "[EMERALD TEST] ";
            if (line.StartsWith(prefix, StringComparison.Ordinal))
            {
                _checkpoints.AddOrUpdate(line[prefix.Length..], 1, (_, count) => count + 1);
            }
            _output.Enqueue(line);
            while (_output.Count > 80) _output.TryDequeue(out _);
        }

        public async Task WaitForExitAsync()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try { await _process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException)
            {
                Assert.Fail($"Fault did not terminate the app within 30 seconds.\n{Output}");
            }
        }

        public async Task WaitForCheckpointAsync(string checkpoint)
        {
            var elapsed = Stopwatch.StartNew();
            while (!Saw(checkpoint) && !HasExited && elapsed.Elapsed < TimeSpan.FromSeconds(30))
            {
                await Task.Delay(50);
            }
            Assert.True(Saw(checkpoint), $"Did not reach '{checkpoint}'.\n{Output}");
        }

        public async ValueTask DisposeAsync()
        {
            if (!HasExited)
            {
                // Only the owned, isolated test process is terminated for cleanup.
                _process.Kill(entireProcessTree: true);
            }
            await _process.WaitForExitAsync();
            _process.Dispose();
        }
    }

    private sealed class TestDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "emerald-process-" + Guid.NewGuid().ToString("N"));
        public TestDirectory() => Directory.CreateDirectory(Path);
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}

public sealed class AppProcessTheoryAttribute : TheoryAttribute
{
    public AppProcessTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("EMERALD_PROCESS_TEST_APP")))
        {
            Skip = "Set EMERALD_PROCESS_TEST_APP to the Debug desktop apphost to run real application crash tests.";
        }
    }
}

public sealed class AppProcessFactAttribute : FactAttribute
{
    public AppProcessFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("EMERALD_PROCESS_TEST_APP")))
        {
            Skip = "Set EMERALD_PROCESS_TEST_APP to the Debug desktop apphost to run real application crash tests.";
        }
    }
}
