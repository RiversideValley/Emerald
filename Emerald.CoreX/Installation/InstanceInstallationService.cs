using System.Collections.Concurrent;
using System.Security.Cryptography;
using Emerald.CoreX.Installers;
using Microsoft.Extensions.Logging;

namespace Emerald.CoreX.Installation;

public interface IInstanceInstallationService
{
    Task<InstanceInstallResult> InstallAsync(Game game, IProgress<InstallationProgress>? progress = null, CancellationToken cancellationToken = default);
    Task<InstanceInstallResult> RepairAsync(Game game, IProgress<InstallationProgress>? progress = null, CancellationToken cancellationToken = default);
    Task<InstanceIntegrityReport> VerifyAsync(Game game, IntegrityCheckLevel level, IProgress<InstallationProgress>? progress = null, CancellationToken cancellationToken = default);
    Task<LaunchReadinessResult> PrepareLaunchAsync(Game game, CancellationToken cancellationToken = default);
}

/// <summary>
/// Owns the lifecycle of an instance installation. Verification is deliberately
/// local-only; network access belongs to install and repair operations.
/// </summary>
public sealed class InstanceInstallationService(
    ILogger<InstanceInstallationService> logger,
    IInstallationStateStore stateStore,
    INetworkCapabilityService network) : IInstanceInstallationService
{
    // A shared gate is keyed by the normalized instance path because callers can
    // reach this service through the UI, API, startup audit, and launch preflight.
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.OrdinalIgnoreCase);

    public Task<InstanceInstallResult> InstallAsync(Game game, IProgress<InstallationProgress>? progress = null, CancellationToken cancellationToken = default)
        => InstallOrRepairAsync(game, false, progress, cancellationToken);

    public Task<InstanceInstallResult> RepairAsync(Game game, IProgress<InstallationProgress>? progress = null, CancellationToken cancellationToken = default)
        => InstallOrRepairAsync(game, true, progress, cancellationToken);

    public async Task<InstanceIntegrityReport> VerifyAsync(Game game, IntegrityCheckLevel level, IProgress<InstallationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var gate = Gates.GetOrAdd(Path.GetFullPath(game.Path.BasePath), _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try { return await VerifyCoreAsync(game, level, progress, cancellationToken); }
        finally { gate.Release(); }
    }

    public async Task<LaunchReadinessResult> PrepareLaunchAsync(Game game, CancellationToken cancellationToken = default)
    {
        // Preflight is intentionally quick and never repairs automatically.
        var report = await VerifyAsync(game, IntegrityCheckLevel.Quick, cancellationToken: cancellationToken);
        return new(report.CanLaunch, report, report.CanLaunch ? null : string.Join(Environment.NewLine, report.Issues.Where(x => x.Severity == IntegritySeverity.Critical).Select(x => x.Message)));
    }

    private async Task<InstanceInstallResult> InstallOrRepairAsync(Game game, bool repair, IProgress<InstallationProgress>? progress, CancellationToken cancellationToken)
    {
        var gate = Gates.GetOrAdd(Path.GetFullPath(game.Path.BasePath), _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        // Keep the old receipt until the entire operation and post-install audit
        // succeed. It remains useful if a repair/download is interrupted.
        var previous = await stateStore.ReadAsync(game, cancellationToken);
        try
        {
            if (game.HasActiveSession)
                return new(false, game.InstallationState, game.Version.RealVersion, null, "Stop the running game before installing or repairing it.");

            game.InstallationState = InstanceInstallationState.Installing;
            progress?.Report(new(repair ? "Repairing" : "Installing", game.Version.DisplayName, 0, 1));

            var capability = await network.ProbeAsync(NetworkCapability.MinecraftMetadata, cancellationToken);
            if (capability.State is NetworkAvailabilityState.Unavailable or NetworkAvailabilityState.Degraded)
                throw new InvalidOperationException("Minecraft metadata service is not currently available.");

            await game.InstallVersionOrThrow(isOffline: false, showFileProgress: true);
            network.ReportSuccess(NetworkCapability.MinecraftFiles);

            // CmlLib has finished producing local metadata. Emerald now derives
            // its own deterministic manifest and proves it is launchable before
            // committing a new receipt.
            var receipt = await LocalInstanceManifest.BuildAsync(game, cancellationToken)
                ?? throw new InvalidOperationException("Installation did not produce readable local version metadata.");
            receipt.SuccessfulInstallAt = previous?.SuccessfulInstallAt ?? DateTimeOffset.UtcNow;
            receipt.SuccessfulRepairAt = repair ? DateTimeOffset.UtcNow : previous?.SuccessfulRepairAt;
            var report = await VerifyReceiptAsync(game, receipt, IntegrityCheckLevel.Full, progress, cancellationToken);
            if (!report.CanLaunch) throw new InvalidOperationException("Post-install verification found launch-critical damage.");
            receipt.FullVerificationAt = report.VerifiedAt;
            await stateStore.WriteAsync(game, receipt, cancellationToken);
            Apply(game, report);
            return new(true, report.State, receipt.ResolvedVersion, report);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "{Operation} failed for {Game}", repair ? "Repair" : "Install", game.Version.DisplayName);
            // Re-evaluate what is actually on disk instead of blindly marking the
            // instance failed; the previous installation may still be healthy.
            var report = await VerifyCoreAsync(game, IntegrityCheckLevel.Quick, null, CancellationToken.None);
            if (previous == null && !report.CanLaunch) game.InstallationState = InstanceInstallationState.Failed;
            return new(false, game.InstallationState, game.Version.RealVersion, report, ex.Message);
        }
        finally { gate.Release(); }
    }

    private async Task<InstanceIntegrityReport> VerifyCoreAsync(Game game, IntegrityCheckLevel level, IProgress<InstallationProgress>? progress, CancellationToken cancellationToken)
    {
        game.InstallationState = InstanceInstallationState.Verifying;
        InstanceInstallReceipt? receipt;
        try { receipt = await stateStore.ReadAsync(game, cancellationToken); }
        catch (Exception ex)
        {
            var invalid = new InstanceIntegrityReport(level, InstanceInstallationState.NeedsRepair,
                [new("receipt-invalid", $"Installation receipt is unreadable: {ex.Message}", IntegritySeverity.Critical)], DateTimeOffset.UtcNow, 0, 0);
            Apply(game, invalid); return invalid;
        }

        // Receipt-less instances predate this subsystem. Build their expected
        // manifest solely from local version JSON and require one full audit.
        var migrating = receipt == null;
        receipt ??= await LocalInstanceManifest.BuildAsync(game, cancellationToken);
        if (receipt == null)
        {
            var missing = new InstanceIntegrityReport(level, InstanceInstallationState.NotInstalled,
                [new("not-installed", "No completed local installation was found.", IntegritySeverity.Critical)], DateTimeOffset.UtcNow, 0, 0);
            Apply(game, missing); return missing;
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
        Apply(game, report);
        return report;
    }

    private static async Task<InstanceIntegrityReport> VerifyReceiptAsync(Game game, InstanceInstallReceipt receipt, IntegrityCheckLevel level,
        IProgress<InstallationProgress>? progress, CancellationToken cancellationToken)
    {
        var issues = new List<IntegrityIssue>();
        var checkedFiles = 0;
        var hashedFiles = 0;
        if (!string.Equals(receipt.PathLayoutFingerprint, LocalInstanceManifest.ComputePathFingerprint(game), StringComparison.Ordinal))
            issues.Add(new("path-layout-changed", "Shared Minecraft path layout changed after this installation.", IntegritySeverity.Critical));

        // Quick checks include launch metadata and the asset index, but skip the
        // potentially very large set of content-addressed asset objects.
        var selected = level == IntegrityCheckLevel.Full
            ? receipt.Files
            : receipt.Files.Where(x => x.Category != ManagedFileCategory.Asset || x.RelativePath.StartsWith("indexes/", StringComparison.Ordinal)).ToList();
        // Shared libraries/assets can appear through several inherited versions.
        // Hash each physical path only once per audit.
        foreach (var file in selected.GroupBy(x => LocalInstanceManifest.Resolve(game, x), StringComparer.OrdinalIgnoreCase).Select(x => x.First()))
        {
            cancellationToken.ThrowIfCancellationRequested();
            checkedFiles++;
            progress?.Report(new("Verifying", file.RelativePath, checkedFiles, selected.Count));
            var path = LocalInstanceManifest.Resolve(game, file);
            if (!File.Exists(path))
            {
                issues.Add(new("missing-file", $"Missing {file.Category}: {file.RelativePath}", file.Severity, file));
                continue;
            }
            var info = new FileInfo(path);
            if (file.Size is long size && info.Length != size)
            {
                issues.Add(new("wrong-size", $"Wrong size for {file.Category}: {file.RelativePath}", file.Severity, file));
                continue;
            }
            // Quick mode stops at existence/size. Full mode proves file content
            // whenever an authoritative hash is available.
            if (level == IntegrityCheckLevel.Full && (!string.IsNullOrWhiteSpace(file.Sha512) || !string.IsNullOrWhiteSpace(file.Sha1)))
            {
                hashedFiles++;
                await using var stream = File.OpenRead(path);
                var actual = file.Sha512 != null
                    ? Convert.ToHexString(await SHA512.HashDataAsync(stream, cancellationToken))
                    : Convert.ToHexString(await SHA1.HashDataAsync(stream, cancellationToken));
                var expected = file.Sha512 ?? file.Sha1!;
                if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                    issues.Add(new("hash-mismatch", $"Corrupt {file.Category}: {file.RelativePath}", file.Severity, file));
            }
        }

        var hasCritical = issues.Any(x => x.Severity == IntegritySeverity.Critical);
        var state = hasCritical ? InstanceInstallationState.NeedsRepair
            : issues.Count > 0 ? InstanceInstallationState.ReadyWithWarnings
            : InstanceInstallationState.Ready;
        return new(level, state, issues, DateTimeOffset.UtcNow, checkedFiles, hashedFiles);
    }

    private static void Apply(Game game, InstanceIntegrityReport report)
    {
        game.InstallationState = report.State;
        game.LastVerifiedAt = report.VerifiedAt;
        game.IntegrityIssues = report.Issues;
    }
}
