using System.Collections.Concurrent;
using System.Security.Cryptography;
using CmlLib.Core;
using CmlLib.Core.Installers;
using Emerald.CoreX.Notifications;
using Emerald.CoreX.Services;
using Emerald.CoreX.Installers;
using Microsoft.Extensions.Logging;

namespace Emerald.CoreX.Installation;

public interface IInstanceInstallationService
{
    Task<InstanceInstallResult> InstallAsync(Game game, IProgress<InstallationProgress>? progress = null, CancellationToken cancellationToken = default);
    Task<InstanceInstallResult> RepairAsync(Game game, IProgress<InstallationProgress>? progress = null, CancellationToken cancellationToken = default);
    Task<InstanceIntegrityReport> VerifyAsync(Game game, IntegrityCheckLevel level, IProgress<InstallationProgress>? progress = null, CancellationToken cancellationToken = default);
    Task<InstanceIntegrityReport?> VerifyWhenIdleAsync(Game game, IntegrityCheckLevel level, IProgress<InstallationProgress>? progress = null, CancellationToken cancellationToken = default);
    Task<LaunchReadinessResult> PrepareLaunchAsync(Game game, CancellationToken cancellationToken = default);
}

/// <summary>
/// Owns the lifecycle of an instance installation. Verification is deliberately
/// local-only; network access belongs to install and repair operations.
/// </summary>
public sealed class InstanceInstallationService(
    ILogger<InstanceInstallationService> logger,
    IInstallationStateStore stateStore,
    INetworkCapabilityService network,
    IUiDispatcher? uiDispatcher = null,
    INotificationService? notifications = null,
    IDownloadActivityService? downloadActivity = null) : IInstanceInstallationService
{
    // A shared gate is keyed by the normalized instance path because callers can
    // reach this service through the UI, API, startup audit, and launch preflight.
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.OrdinalIgnoreCase);

    private IUiDispatcher UiDispatcher => uiDispatcher ?? new InlineUiDispatcher();
    private IDownloadActivityService DownloadActivity => downloadActivity ??= new DownloadActivityService();

    private sealed record InstallationSnapshot(
        InstanceInstallationState State,
        DateTimeOffset? LastVerifiedAt,
        IReadOnlyList<IntegrityIssue> Issues,
        string? RealVersion);

    public Task<InstanceInstallResult> InstallAsync(Game game, IProgress<InstallationProgress>? progress = null, CancellationToken cancellationToken = default)
        => Task.Run(() => InstallOrRepairAsync(game, false, progress, cancellationToken), cancellationToken);

    public Task<InstanceInstallResult> RepairAsync(Game game, IProgress<InstallationProgress>? progress = null, CancellationToken cancellationToken = default)
        => Task.Run(() => InstallOrRepairAsync(game, true, progress, cancellationToken), cancellationToken);

    public async Task<InstanceIntegrityReport> VerifyAsync(Game game, IntegrityCheckLevel level, IProgress<InstallationProgress>? progress = null, CancellationToken cancellationToken = default)
        => await Task.Run(() => VerifyWithGateAsync(game, level, progress, cancellationToken), cancellationToken);

    public async Task<InstanceIntegrityReport?> VerifyWhenIdleAsync(Game game, IntegrityCheckLevel level, IProgress<InstallationProgress>? progress = null, CancellationToken cancellationToken = default)
        => await Task.Run(() => TryVerifyWhenIdleAsync(game, level, progress, cancellationToken), cancellationToken);

    private async Task<InstanceIntegrityReport> VerifyWithGateAsync(Game game, IntegrityCheckLevel level, IProgress<InstallationProgress>? progress, CancellationToken cancellationToken)
    {
        var gate = Gates.GetOrAdd(Path.GetFullPath(game.Path.BasePath), _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        var snapshot = Capture(game);
        try { return await VerifyCoreAsync(game, level, progress, cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await RestoreAsync(game, snapshot);
            throw;
        }
        catch (Exception ex)
        {
            var failed = FailedVerificationReport(level, ex);
            await ApplyAsync(game, failed);
            return failed;
        }
        finally { gate.Release(); }
    }

    private async Task<InstanceIntegrityReport?> TryVerifyWhenIdleAsync(Game game, IntegrityCheckLevel level, IProgress<InstallationProgress>? progress, CancellationToken cancellationToken)
    {
        if (game.HasActiveSession) return null;
        var gate = Gates.GetOrAdd(Path.GetFullPath(game.Path.BasePath), _ => new SemaphoreSlim(1, 1));
        if (!gate.Wait(0)) return null;
        try
        {
            var snapshot = Capture(game);
            try { return await VerifyCoreAsync(game, level, progress, cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                await RestoreAsync(game, snapshot);
                throw;
            }
            catch (Exception ex)
            {
                var failed = FailedVerificationReport(level, ex);
                await ApplyAsync(game, failed);
                return failed;
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<LaunchReadinessResult> PrepareLaunchAsync(Game game, CancellationToken cancellationToken = default)
    {
        // Preflight is intentionally quick and never repairs automatically.
        var report = await VerifyAsync(game, IntegrityCheckLevel.Quick, cancellationToken: cancellationToken);
        return new(report.CanLaunch, report, report.CanLaunch ? null : string.Join(Environment.NewLine, report.Issues.Where(x => x.Severity == IntegritySeverity.Critical).Select(x => x.Message)));
    }

    private async Task<InstanceInstallResult> InstallOrRepairAsync(Game game, bool repair, IProgress<InstallationProgress>? progress, CancellationToken cancellationToken)
    {
        using var downloadLease = await DownloadActivity.AcquireDownloadAsync(cancellationToken);
        var gate = Gates.GetOrAdd(Path.GetFullPath(game.Path.BasePath), _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        InstanceInstallReceipt? previous = null;
        InstallationProgressReporter? reporter = null;
        InstallationProgress? terminalProgress = null;
        var terminalSuccess = false;
        string? terminalMessage = null;
        Exception? terminalException = null;
        InstallationSnapshot? snapshot = null;
        try
        {
            previous = await stateStore.ReadAsync(game, cancellationToken);
            if (game.HasActiveSession)
                return new(false, game.InstallationState, game.Version.RealVersion, null, "Stop the running game before installing or repairing it.");

            var operationName = repair ? "Repairing" : "Installing";
            snapshot = Capture(game);
            await SetStateAsync(game, InstanceInstallationState.Installing);
            reporter = CreateReporter(game, operationName, progress);
            using var operationCancellation = CreateOperationCancellation(reporter, cancellationToken);
            var token = operationCancellation.Token;
            reporter.Report(new(operationName, game.Version.DisplayName, 0, 1));
            var result = await ExecuteInstallAsync(game, repair, previous, reporter, token);
            terminalProgress = new("Complete", game.Version.DisplayName, result.Integrity?.CheckedFiles ?? 0, result.Integrity?.CheckedFiles ?? 0);
            terminalSuccess = true;
            terminalMessage = $"Finished {operationName.ToLowerInvariant()} {game.Version.DisplayName}";
            return result;
        }
        catch (OperationCanceledException)
        {
            if (snapshot != null) await RestoreAsync(game, snapshot);
            terminalProgress = new("Canceled", game.Version.DisplayName, 0, 0);
            terminalMessage = $"Canceled installation/repair of {game.Version.DisplayName}";
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "{Operation} failed for {Game}", repair ? "Repair" : "Install", game.Version.DisplayName);
            var failure = await HandleInstallFailureAsync(game, previous, ex);
            var report = failure.Integrity!;
            terminalProgress = new("Failed", game.Version.DisplayName, report.CheckedFiles, report.CheckedFiles);
            terminalMessage = ex.Message;
            terminalException = ex;
            return failure;
        }
        finally
        {
            if (reporter != null)
            {
                await reporter.CompleteAsync(
                    terminalProgress ?? new("Failed", game.Version.DisplayName, 0, 0),
                    terminalSuccess,
                    terminalMessage ?? $"{(repair ? "Repair" : "Installation")} did not complete.",
                    terminalException);
            }

            gate.Release();
        }
    }

    private InstallationProgressReporter CreateReporter(Game game, string operationName, IProgress<InstallationProgress>? progress)
        => new(UiDispatcher, notifications, progress, operationName, $"{operationName} {game.Version.DisplayName}");

    private static CancellationTokenSource CreateOperationCancellation(InstallationProgressReporter reporter, CancellationToken cancellationToken)
        => reporter.NotificationCancellationToken is { } notificationToken
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, notificationToken)
            : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

    private async Task<InstanceInstallResult> ExecuteInstallAsync(Game game, bool repair, InstanceInstallReceipt? previous,
        InstallationProgressReporter reporter, CancellationToken cancellationToken)
    {
        var capability = await network.ProbeAsync(NetworkCapability.MinecraftMetadata, cancellationToken);
        if (capability.State is NetworkAvailabilityState.Unavailable or NetworkAvailabilityState.Degraded)
            throw new InvalidOperationException("Minecraft metadata service is not currently available.");

        var fileProgress = new ActionProgress<InstallerProgressChangedEventArgs>(eventArgs =>
            reporter.Report(new("Downloading", eventArgs.Name, eventArgs.ProgressedTasks, eventArgs.TotalTasks)));
        var byteProgress = new ActionProgress<ByteProgress>(eventArgs =>
            reporter.Report(new("Downloading", null, 0, 0, eventArgs.ProgressedBytes, eventArgs.TotalBytes)));
        await game.InstallVersionOrThrow(false, true, fileProgress, byteProgress, cancellationToken);
        network.ReportSuccess(NetworkCapability.MinecraftFiles);

        var receipt = await LocalInstanceManifest.BuildAsync(game, cancellationToken)
            ?? throw new InvalidOperationException("Installation did not produce readable local version metadata.");
        receipt.SuccessfulInstallAt = previous?.SuccessfulInstallAt ?? DateTimeOffset.UtcNow;
        receipt.SuccessfulRepairAt = repair ? DateTimeOffset.UtcNow : previous?.SuccessfulRepairAt;
        var report = await VerifyReceiptAsync(game, receipt, IntegrityCheckLevel.Full, reporter, cancellationToken);
        if (!report.CanLaunch) throw new InvalidOperationException("Post-install verification found launch-critical damage.");
        receipt.FullVerificationAt = report.VerifiedAt;
        await stateStore.WriteAsync(game, receipt, cancellationToken);
        await ApplyAsync(game, report);
        return new(true, report.State, receipt.ResolvedVersion, report);
    }

    private async Task<InstanceInstallResult> HandleInstallFailureAsync(Game game, InstanceInstallReceipt? previous, Exception exception)
    {
        var report = await ReevaluateAfterFailureAsync(game);
        if (previous == null && !report.CanLaunch && report.State != InstanceInstallationState.Failed)
            await SetStateAsync(game, InstanceInstallationState.Failed);
        return new(false, game.InstallationState, game.Version.RealVersion, report, exception.Message);
    }

    private async Task<InstanceIntegrityReport> ReevaluateAfterFailureAsync(Game game)
    {
        try
        {
            return await VerifyCoreAsync(game, IntegrityCheckLevel.Quick, null, CancellationToken.None);
        }
        catch (Exception verificationException)
        {
            logger.LogError(verificationException, "Local fallback verification failed for {Game}", game.Version.DisplayName);
            var report = FailedInstallationReport(verificationException);
            await ApplyAsync(game, report);
            return report;
        }
    }

    private static InstanceIntegrityReport FailedInstallationReport(Exception exception)
        => new(
            IntegrityCheckLevel.Quick,
            InstanceInstallationState.Failed,
            [new IntegrityIssue("post-failure-verification-failed", $"Installation failed and local verification could not complete: {exception.Message}", IntegritySeverity.Critical)],
            DateTimeOffset.UtcNow,
            0,
            0);

    private async Task<InstanceIntegrityReport> VerifyCoreAsync(Game game, IntegrityCheckLevel level, IProgress<InstallationProgress>? progress, CancellationToken cancellationToken)
    {
        await SetStateAsync(game, InstanceInstallationState.Verifying);
        InstanceInstallReceipt? receipt;
        try { receipt = await stateStore.ReadAsync(game, cancellationToken); }
        catch (Exception ex)
        {
            var invalid = new InstanceIntegrityReport(level, InstanceInstallationState.NeedsRepair,
                [new("receipt-invalid", $"Installation receipt is unreadable: {ex.Message}", IntegritySeverity.Critical)], DateTimeOffset.UtcNow, 0, 0);
            await ApplyAsync(game, invalid); return invalid;
        }

        // Receipt-less instances predate this subsystem. Build their expected
        // manifest solely from local version JSON and require one full audit.
        var migrating = receipt == null;
        receipt ??= await LocalInstanceManifest.BuildAsync(game, cancellationToken);
        if (receipt == null)
        {
            var missing = new InstanceIntegrityReport(level, InstanceInstallationState.NotInstalled,
                [new("not-installed", "No completed local installation was found.", IntegritySeverity.Critical)], DateTimeOffset.UtcNow, 0, 0);
            await ApplyAsync(game, missing); return missing;
        }

        var effectiveLevel = migrating ? IntegrityCheckLevel.Full : level;
        var report = await VerifyReceiptAsync(game, receipt, effectiveLevel, progress, cancellationToken);
        if (migrating && report.CanLaunch)
        {
            receipt.FullVerificationAt = report.VerifiedAt;
            receipt.SuccessfulInstallAt ??= report.VerifiedAt;
            await stateStore.WriteAsync(game, receipt, cancellationToken);
        }
        else if (effectiveLevel == IntegrityCheckLevel.Full && report.CanLaunch)
        {
            receipt.FullVerificationAt = report.VerifiedAt;
            await stateStore.WriteAsync(game, receipt, cancellationToken);
        }
        await ApplyAsync(game, report);
        return report;
    }

    private static async Task<InstanceIntegrityReport> VerifyReceiptAsync(Game game, InstanceInstallReceipt receipt, IntegrityCheckLevel level,
        IProgress<InstallationProgress>? progress, CancellationToken cancellationToken)
    {
        var issues = new List<IntegrityIssue>();
        var checkedFiles = 0;
        var hashedFiles = 0;
        if (HasPathLayoutChanged(game, receipt))
            issues.Add(new("path-layout-changed", "Shared Minecraft path layout changed after this installation.", IntegritySeverity.Critical));

        var selected = SelectFiles(receipt, level);
        foreach (var file in DeduplicateFiles(game, selected))
        {
            cancellationToken.ThrowIfCancellationRequested();
            checkedFiles++;
            progress?.Report(new("Verifying", file.RelativePath, checkedFiles, selected.Count));
            var result = await CheckFileAsync(game, file, level, cancellationToken);
            if (result.Issue != null) issues.Add(result.Issue);
            if (result.Hashed) hashedFiles++;
        }

        return CreateIntegrityReport(level, issues, checkedFiles, hashedFiles);
    }

    private static bool HasPathLayoutChanged(Game game, InstanceInstallReceipt receipt)
        => !string.Equals(receipt.PathLayoutFingerprint, LocalInstanceManifest.ComputePathFingerprint(game), StringComparison.Ordinal);

    private static List<ExpectedManagedFile> SelectFiles(InstanceInstallReceipt receipt, IntegrityCheckLevel level)
        => level == IntegrityCheckLevel.Full
            ? receipt.Files
            : receipt.Files.Where(x => x.Category != ManagedFileCategory.Asset || x.RelativePath.StartsWith("indexes/", StringComparison.Ordinal)).ToList();

    private static IEnumerable<ExpectedManagedFile> DeduplicateFiles(Game game, IEnumerable<ExpectedManagedFile> files)
        => files.GroupBy(x => LocalInstanceManifest.Resolve(game, x), StringComparer.OrdinalIgnoreCase).Select(x => x.First());

    private static async Task<FileCheckResult> CheckFileAsync(Game game, ExpectedManagedFile file, IntegrityCheckLevel level,
        CancellationToken cancellationToken)
    {
        var path = LocalInstanceManifest.Resolve(game, file);
        if (!File.Exists(path))
            return new(new("missing-file", $"Missing {file.Category}: {file.RelativePath}", file.Severity, file), false);

        var info = new FileInfo(path);
        if (file.Size is long size && info.Length != size)
            return new(new("wrong-size", $"Wrong size for {file.Category}: {file.RelativePath}", file.Severity, file), false);

        if (level != IntegrityCheckLevel.Full || (!HasHash(file))) return new(null, false);
        await using var stream = File.OpenRead(path);
        var actual = file.Sha512 != null
            ? Convert.ToHexString(await SHA512.HashDataAsync(stream, cancellationToken))
            : Convert.ToHexString(await SHA1.HashDataAsync(stream, cancellationToken));
        var expected = file.Sha512 ?? file.Sha1!;
        var issue = string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase)
            ? null
            : new IntegrityIssue("hash-mismatch", $"Corrupt {file.Category}: {file.RelativePath}", file.Severity, file);
        return new(issue, true);
    }

    private static bool HasHash(ExpectedManagedFile file)
        => !string.IsNullOrWhiteSpace(file.Sha512) || !string.IsNullOrWhiteSpace(file.Sha1);

    private static InstanceIntegrityReport CreateIntegrityReport(IntegrityCheckLevel level, List<IntegrityIssue> issues,
        int checkedFiles, int hashedFiles)
    {
        var state = issues.Any(x => x.Severity == IntegritySeverity.Critical)
            ? InstanceInstallationState.NeedsRepair
            : issues.Count > 0 ? InstanceInstallationState.ReadyWithWarnings : InstanceInstallationState.Ready;
        return new(level, state, issues, DateTimeOffset.UtcNow, checkedFiles, hashedFiles);
    }

    private sealed record FileCheckResult(IntegrityIssue? Issue, bool Hashed);

    private Task SetStateAsync(Game game, InstanceInstallationState state)
        => UiDispatcher.InvokeAsync(() => game.InstallationState = state);

    private Task ApplyAsync(Game game, InstanceIntegrityReport report)
        => UiDispatcher.InvokeAsync(() =>
        {
            game.InstallationState = report.State;
            game.LastVerifiedAt = report.VerifiedAt;
            game.IntegrityIssues = report.Issues;
        });

    private static InstallationSnapshot Capture(Game game)
        => new(game.InstallationState, game.LastVerifiedAt, game.IntegrityIssues, game.Version.RealVersion);

    private Task RestoreAsync(Game game, InstallationSnapshot snapshot)
        => UiDispatcher.InvokeAsync(() =>
        {
            game.Version.RealVersion = snapshot.RealVersion;
            game.InstallationState = snapshot.State;
            game.LastVerifiedAt = snapshot.LastVerifiedAt;
            game.IntegrityIssues = snapshot.Issues;
        });

    private static InstanceIntegrityReport FailedVerificationReport(IntegrityCheckLevel level, Exception exception)
        => new(
            level,
            InstanceInstallationState.Failed,
            [new IntegrityIssue("verification-failed", $"Local verification could not complete: {exception.Message}", IntegritySeverity.Critical)],
            DateTimeOffset.UtcNow,
            0,
            0);

    private sealed class ActionProgress<T>(Action<T> action) : IProgress<T>
    {
        public void Report(T value) => action(value);
    }
}
