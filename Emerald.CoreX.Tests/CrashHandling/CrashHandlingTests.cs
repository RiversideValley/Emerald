using Emerald.CoreX.CrashHandling;
using Xunit;

namespace Emerald.CoreX.Tests.CrashHandling;

public sealed class CrashHandlingTests
{
    [Fact]
    public void FileStore_WritesAndReadsOrderedReportWithTextExport()
    {
        using var temp = new TemporaryDirectory();
        var store = new FileCrashReportStore(temp.Path);
        var record = CreateRecord("run-1", DateTimeOffset.UtcNow.AddMinutes(-1));

        Assert.True(store.TryWrite(record));
        Assert.True(File.Exists(record.ReportPath));
        Assert.True(File.Exists(Path.Combine(store.ReportsPath, record.Id, "report.json")));

        var loaded = Assert.Single(store.GetAll());
        Assert.Equal(record.Id, loaded.Id);
        Assert.Contains("=== EMERALD CRASH REPORT ===", File.ReadAllText(record.ReportPath!));
    }

    [Fact]
    public void FileStore_AssignsUniqueIdsWhenRecordsUseDefaultIds()
    {
        using var temp = new TemporaryDirectory();
        var store = new FileCrashReportStore(temp.Path);
        var first = CreateRecord("run-1", DateTimeOffset.UtcNow);
        var second = CreateRecord("run-2", DateTimeOffset.UtcNow.AddSeconds(1));
        first.Id = string.Empty;
        second.Id = string.Empty;

        Assert.True(store.TryWrite(first));
        Assert.True(store.TryWrite(second));
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(2, store.GetAll().Count);
    }

    [Fact]
    public void FileStore_IgnoresDamagedRecords()
    {
        using var temp = new TemporaryDirectory();
        var store = new FileCrashReportStore(temp.Path);
        var damagedDirectory = Path.Combine(store.ReportsPath, "damaged");
        Directory.CreateDirectory(damagedDirectory);
        File.WriteAllText(Path.Combine(damagedDirectory, "report.json"), "{");

        Assert.Empty(store.GetAll());
    }

    [Fact]
    public void FallbackStore_UsesSecondaryLocationWhenPrimaryCannotBeWritten()
    {
        using var temp = new TemporaryDirectory();
        var primaryPath = System.IO.Path.Combine(temp.Path, "primary");
        var fallbackPath = System.IO.Path.Combine(temp.Path, "fallback");
        Directory.CreateDirectory(primaryPath);
        File.WriteAllText(System.IO.Path.Combine(primaryPath, "crashes"), "not a directory");

        var store = new FallbackCrashReportStore(
            new FileCrashReportStore(primaryPath),
            new FileCrashReportStore(fallbackPath));
        var record = CreateRecord("run-fallback", DateTimeOffset.UtcNow);

        Assert.True(store.TryWriteFatal(record));
        Assert.StartsWith(fallbackPath, record.ReportPath, StringComparison.Ordinal);
        Assert.Single(store.GetAll());
    }

    [Fact]
    public void FileStore_AcknowledgesAndDeletesReports()
    {
        using var temp = new TemporaryDirectory();
        var store = new FileCrashReportStore(temp.Path);
        var record = CreateRecord("run-1", DateTimeOffset.UtcNow);
        store.TryWrite(record);

        Assert.True(store.TryAcknowledge(record.Id));
        Assert.True(store.Get(record.Id)!.IsAcknowledged);
        Assert.True(store.TryDelete(record.Id));
        Assert.Null(store.Get(record.Id));
    }

    [Fact]
    public void LifecycleTracker_ReportsPreviousUncleanRunAndEntersRecoveryAfterThreeEarlyFailures()
    {
        using var temp = new TemporaryDirectory();

        using (var first = new FileAppLifecycleTracker(temp.Path))
        {
            using var start = first.BeginRun();
            first.MarkNormalStartupAttempted();
        }

        using (var second = new FileAppLifecycleTracker(temp.Path))
        {
            using var result = second.BeginRun();
            Assert.NotNull(result.PreviousRun);
            Assert.Equal(1, result.ConsecutiveEarlyFailures);
            second.MarkRunReconciled(result.PreviousRun!.RunId);
            second.MarkNormalStartupAttempted();
        }

        using (var third = new FileAppLifecycleTracker(temp.Path))
        {
            using var result = third.BeginRun();
            Assert.Equal(2, result.ConsecutiveEarlyFailures);
            third.MarkRunReconciled(result.PreviousRun!.RunId);
            third.MarkNormalStartupAttempted();
        }

        using (var fourth = new FileAppLifecycleTracker(temp.Path))
        {
            using var result = fourth.BeginRun();
            Assert.Equal(3, result.ConsecutiveEarlyFailures);
            Assert.True(result.IsRecoveryMode);
            fourth.MarkRunReconciled(result.PreviousRun!.RunId);
            fourth.MarkNormalStartupAttempted();
        }

        using (var clean = new FileAppLifecycleTracker(temp.Path))
        {
            using var result = clean.BeginRun();
            clean.MarkRunReconciled(result.PreviousRun!.RunId);
            clean.MarkStartupComplete();
            clean.MarkCleanExit();
        }

        using var next = new FileAppLifecycleTracker(temp.Path);
        using var afterClean = next.BeginRun();
        Assert.Equal(0, afterClean.ConsecutiveEarlyFailures);
        Assert.Null(afterClean.PreviousRun);
    }

    [Fact]
    public void LifecycleTracker_PreservesRecoveryModeWhenRecoveryOnlyRunExitsCleanly()
    {
        using var temp = new TemporaryDirectory();

        using (var first = new FileAppLifecycleTracker(temp.Path))
        {
            using var start = first.BeginRun();
            first.MarkNormalStartupAttempted();
        }

        using (var second = new FileAppLifecycleTracker(temp.Path))
        {
            using var result = second.BeginRun();
            second.MarkRunReconciled(result.PreviousRun!.RunId);
            second.MarkNormalStartupAttempted();
        }

        using (var third = new FileAppLifecycleTracker(temp.Path))
        {
            using var result = third.BeginRun();
            third.MarkRunReconciled(result.PreviousRun!.RunId);
            third.MarkNormalStartupAttempted();
        }

        using (var recovery = new FileAppLifecycleTracker(temp.Path))
        {
            using var result = recovery.BeginRun();
            Assert.True(result.IsRecoveryMode);
            recovery.MarkRunReconciled(result.PreviousRun!.RunId);
            recovery.MarkCleanExit();
        }

        using (var stillInRecovery = new FileAppLifecycleTracker(temp.Path))
        {
            using var result = stillInRecovery.BeginRun();
            Assert.True(result.IsRecoveryMode);
            Assert.Equal(3, result.ConsecutiveEarlyFailures);
            stillInRecovery.MarkRunReconciled(result.PreviousRun!.RunId);
            stillInRecovery.MarkStartupComplete();
            stillInRecovery.MarkCleanExit();
        }

        using var afterRecovery = new FileAppLifecycleTracker(temp.Path);
        using var finalStart = afterRecovery.BeginRun();
        Assert.False(finalStart.IsRecoveryMode);
    }

    [Fact]
    public void LifecycleTracker_DoesNotRetireStaleMarkerBeforeCallerReconcilesIt()
    {
        using var temp = new TemporaryDirectory();
        using (var first = new FileAppLifecycleTracker(temp.Path))
        {
            using var start = first.BeginRun();
            first.MarkNormalStartupAttempted();
        }

        using var second = new FileAppLifecycleTracker(temp.Path);
        using var result = second.BeginRun();

        Assert.NotNull(result.PreviousRun);
        Assert.Contains(result.PreviousRuns, run => !run.Reconciled && run.RunId == result.PreviousRun!.RunId);
    }

    [Fact]
    public void LifecycleTracker_CleanShutdownDoesNotCreateAnUnexpectedRun()
    {
        using var temp = new TemporaryDirectory();
        using (var first = new FileAppLifecycleTracker(temp.Path))
        {
            using var start = first.BeginRun();
            first.MarkNormalStartupAttempted();
            first.MarkStartupComplete();
            first.MarkCleanExit();
        }

        using var next = new FileAppLifecycleTracker(temp.Path);
        using var result = next.BeginRun();

        Assert.Null(result.PreviousRun);
        Assert.Equal(0, result.ConsecutiveEarlyFailures);
    }

    [Fact]
    public void ExceptionInfo_ContainsNestedAndAggregateExceptions()
    {
        var exception = new AggregateException(
            new InvalidOperationException("outer", new ArgumentException("inner")),
            new ApplicationException("second"));

        var info = CrashExceptionInfo.FromException(exception);

        Assert.Equal(2, info.InnerExceptions.Count);
        Assert.Single(info.InnerExceptions[0].InnerExceptions);
        Assert.Contains("outer", info.InnerExceptions[0].Message);
    }

    [Fact]
    public void Sanitizer_RemovesSecretsAndUserPaths()
    {
        var sanitized = CrashTextSanitizer.Sanitize(
            "Authorization: Bearer super-secret password=hunter2 /home/alice/Emerald/log.txt");

        Assert.DoesNotContain("super-secret", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("/home/alice", sanitized, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", sanitized, StringComparison.Ordinal);
    }

    [Fact]
    public void Sanitizer_RemovesQuotedJsonCredentialsAndCookieHeaders()
    {
        var sanitized = CrashTextSanitizer.Sanitize(
            "{\"access_token\":\"json-secret\", \"password\": \"json-password\"} Cookie: session-secret");

        Assert.DoesNotContain("json-secret", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("json-password", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("session-secret", sanitized, StringComparison.Ordinal);
    }

    [Fact]
    public void LogTailReader_ResolvesRolledLogWhenBasePathDoesNotExist()
    {
        using var temp = new TemporaryDirectory();
        var rolledPath = System.IO.Path.Combine(temp.Path, "app_20260827.log");
        File.WriteAllText(rolledPath, "latest log line");

        var tail = CrashLogTailReader.Read(System.IO.Path.Combine(temp.Path, "app_.log"));

        Assert.Contains("latest log line", tail, StringComparison.Ordinal);
    }

    [Fact]
    public void GitHubComposer_EncodesACompactSummaryAndKeepsFullReportSeparate()
    {
        var record = CreateRecord("run-1", DateTimeOffset.UtcNow);
        var draft = new GitHubCrashIssueComposer("https://github.com/RiversideValley/Emerald").Compose(record);

        Assert.StartsWith("https://github.com/RiversideValley/Emerald/issues/new?", draft.Url, StringComparison.Ordinal);
        Assert.Contains("template=crash_report.md", draft.Url, StringComparison.Ordinal);
        Assert.Contains("full sanitized report", draft.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("=== EMERALD CRASH REPORT ===", draft.FullReport, StringComparison.Ordinal);
        Assert.DoesNotContain("Application log tail", draft.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GitHubComposer_CompactsOversizedDraftUrl()
    {
        var record = CreateRecord("run-1", DateTimeOffset.UtcNow);
        record.Source = new string('s', 10_000);
        record.Exception!.Message = new string('m', 20_000);

        var draft = new GitHubCrashIssueComposer("https://github.com/RiversideValley/Emerald").Compose(record);

        Assert.True(draft.Url.Length <= 2_000);
        Assert.Contains("paste", draft.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("=== EMERALD CRASH REPORT ===", draft.FullReport, StringComparison.Ordinal);
    }

    private static CrashRecord CreateRecord(string runId, DateTimeOffset occurredUtc)
        => new()
        {
            Id = Guid.NewGuid().ToString("N"),
            RunId = runId,
            OccurredUtc = occurredUtc,
            Kind = CrashRecordKind.ManagedCrash,
            Source = "Test",
            AppVersion = "1.2.3",
            PackageVersion = "1.2.3.4",
            BuildChannel = "Release",
            Platform = "Test",
            OperatingSystem = "Test OS",
            Architecture = "X64",
            Runtime = ".NET",
            Exception = CrashExceptionInfo.FromException(new InvalidOperationException("test"))
        };

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"emerald-crash-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
