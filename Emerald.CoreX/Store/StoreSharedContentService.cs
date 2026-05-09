using System.Security.Cryptography;
using Emerald.CoreX.Helpers;
using Emerald.Services;
using Microsoft.Extensions.Logging;

namespace Emerald.CoreX.Store;

public interface IStoreSharedContentService
{
    bool IsSharingEnabled(Game game, StoreContentType contentType);

    bool IsSharingEnabled(Game game, StoreContentType contentType, string? sharedBasePathOverride);

    Task<StoreSharedInstallResult> InstallAsync(StoreSharedInstallRequest request);

    StoreSharedContentHealth GetHealth(StoreInstallRecord record);

    Task RemoveReferenceAsync(StoreInstallRecord record, bool deleteInstanceFile, CancellationToken cancellationToken = default);

    void AddOrUpdateManifestReference(
        string sharedBasePath,
        string installFolderName,
        StoreInstallRecord record);

    Task<StoreSharedContentMigrationPlan> CreateMigrationPlanAsync(
        Game game,
        StoreContentType contentType,
        bool enableSharing,
        string installFolderName,
        CancellationToken cancellationToken = default);

    Task<StoreSharedContentMigrationSummary> ApplyMigrationAsync(
        StoreSharedContentMigrationPlan plan,
        StoreSharedContentMigrationAction action,
        CancellationToken cancellationToken = default);

    StoreSharedContentMigrationSummary SummarizeMigrationPlans(IEnumerable<StoreSharedContentMigrationPlan> plans);
}

public sealed class StoreSharedContentService : IStoreSharedContentService
{
    private const string HashAlgorithmName = "sha1";

    private readonly IBaseSettingsService _baseSettingsService;
    private readonly IStoreFileLinkService _linkService;
    private readonly IStoreSharedContentSettingsService _settingsService;
    private readonly ILogger<StoreSharedContentService> _logger;

    public StoreSharedContentService(
        IBaseSettingsService baseSettingsService,
        IStoreFileLinkService linkService,
        IStoreSharedContentSettingsService settingsService,
        ILogger<StoreSharedContentService> logger)
    {
        _baseSettingsService = baseSettingsService;
        _linkService = linkService;
        _settingsService = settingsService;
        _logger = logger;
    }

    public bool IsSharingEnabled(Game game, StoreContentType contentType)
        => IsSharingEnabled(game, contentType, game.SharedMinecraftBasePath);

    public bool IsSharingEnabled(Game game, StoreContentType contentType, string? sharedBasePathOverride)
    {
        var settings = game.EffectiveSettings;
        return !string.IsNullOrWhiteSpace(sharedBasePathOverride)
               && settings.IsSharedStoreContentEnabled(contentType);
    }

    public async Task<StoreSharedInstallResult> InstallAsync(StoreSharedInstallRequest request)
    {
        request.CancellationToken.ThrowIfCancellationRequested();

        var sharedBasePath = ResolveSharedBasePath(request);
        var settings = request.EffectiveSettingsOverride ?? request.Game.EffectiveSettings;
        if (string.IsNullOrWhiteSpace(sharedBasePath) || !settings.IsSharedStoreContentEnabled(request.ContentType))
        {
            await request.DownloadToPathAsync(request.TargetPath, request.Progress, request.CancellationToken);
            return new StoreSharedInstallResult
            {
                FilePath = request.TargetPath,
                Sha1 = NormalizeHash(request.File.Hashes?.Sha1),
                Sha512 = NormalizeHash(request.File.Hashes?.Sha512),
                LinkKind = StoreLinkKind.None,
                FileSizeBytes = GetFileSize(request.TargetPath)
            };
        }

        var expectedSha1 = NormalizeHash(request.File.Hashes?.Sha1);
        var expectedSha512 = NormalizeHash(request.File.Hashes?.Sha512);

        if (!string.IsNullOrWhiteSpace(expectedSha1))
        {
            var existingSharedPath = GetSharedFilePath(
                sharedBasePath,
                request.InstallFolderName,
                expectedSha1,
                request.File.Filename);

            if (File.Exists(existingSharedPath)
                && await VerifyHashesAsync(existingSharedPath, expectedSha1, expectedSha512, request.CancellationToken))
            {
                var linked = _linkService.CreateLinkOrCopy(
                    existingSharedPath,
                    request.TargetPath,
                    _settingsService.GetPreferredLinkMode());

                return new StoreSharedInstallResult
                {
                    FilePath = request.TargetPath,
                    Sha1 = expectedSha1,
                    Sha512 = expectedSha512,
                    SharedFilePath = existingSharedPath,
                    LinkKind = linked.LinkKind,
                    FileSizeBytes = GetFileSize(existingSharedPath)
                };
            }
        }

        var tempPath = Path.Combine(
            Path.GetTempPath(),
            "Emerald",
            "Store",
            $"{Guid.NewGuid():N}{Path.GetExtension(request.File.Filename)}");
        Directory.CreateDirectory(Path.GetDirectoryName(tempPath)!);

        try
        {
            await request.DownloadToPathAsync(tempPath, request.Progress, request.CancellationToken);

            var actualSha1 = string.IsNullOrWhiteSpace(expectedSha1)
                ? await ComputeSha1Async(tempPath, request.CancellationToken)
                : expectedSha1;

            if (!await VerifyHashesAsync(tempPath, actualSha1, expectedSha512, request.CancellationToken))
            {
                throw new InvalidOperationException("Downloaded store item failed hash verification.");
            }

            var sharedFilePath = GetSharedFilePath(
                sharedBasePath,
                request.InstallFolderName,
                actualSha1,
                request.File.Filename);

            await EnsureSharedFileAsync(tempPath, sharedFilePath, actualSha1, expectedSha512, request.CancellationToken);

            var link = _linkService.CreateLinkOrCopy(
                sharedFilePath,
                request.TargetPath,
                _settingsService.GetPreferredLinkMode());

            return new StoreSharedInstallResult
            {
                FilePath = request.TargetPath,
                Sha1 = actualSha1,
                Sha512 = expectedSha512,
                SharedFilePath = sharedFilePath,
                LinkKind = link.LinkKind,
                FileSizeBytes = GetFileSize(sharedFilePath)
            };
        }
        finally
        {
            TryDeleteFile(tempPath);
        }
    }

    public StoreSharedContentHealth GetHealth(StoreInstallRecord record)
    {
        if (record.LinkKind == StoreLinkKind.None || string.IsNullOrWhiteSpace(record.GodFolderHash))
        {
            return File.Exists(record.FilePath) || Directory.Exists(record.FilePath)
                ? StoreSharedContentHealth.Ok
                : StoreSharedContentHealth.MissingInstanceFile;
        }

        var targetExists = File.Exists(record.FilePath);
        var targetIsSymlink = IsSymbolicLinkOrReparsePoint(record.FilePath);
        if (!targetExists && targetIsSymlink)
        {
            return StoreSharedContentHealth.BrokenLink;
        }

        if (!targetExists)
        {
            return StoreSharedContentHealth.MissingInstanceFile;
        }

        if (string.IsNullOrWhiteSpace(record.SharedFilePath) || !File.Exists(record.SharedFilePath))
        {
            return StoreSharedContentHealth.MissingSharedFile;
        }

        if (!string.IsNullOrWhiteSpace(record.Sha1))
        {
            try
            {
                var actual = ComputeSha1(record.FilePath);
                if (!actual.Equals(record.Sha1, StringComparison.OrdinalIgnoreCase))
                {
                    return StoreSharedContentHealth.HashMismatch;
                }
            }
            catch
            {
                return StoreSharedContentHealth.HashMismatch;
            }
        }

        return StoreSharedContentHealth.Ok;
    }

    public Task RemoveReferenceAsync(StoreInstallRecord record, bool deleteInstanceFile, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (deleteInstanceFile)
        {
            TryDeleteFile(record.FilePath);
        }

        if (!string.IsNullOrWhiteSpace(record.GodFolderHash))
        {
            RemoveManifestReference(record);
        }

        return Task.CompletedTask;
    }

    public async Task<StoreSharedContentMigrationPlan> CreateMigrationPlanAsync(
        Game game,
        StoreContentType contentType,
        bool enableSharing,
        string installFolderName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var plan = new StoreSharedContentMigrationPlan
        {
            Game = game,
            ContentType = contentType,
            EnableSharing = enableSharing,
            InstallFolderName = installFolderName
        };

        var records = LoadInstallRecords()
            .Where(record => IsRecordForGameAndType(record, game, contentType))
            .ToList();
        var recordsByPath = records.ToDictionary(
            record => NormalizePath(record.FilePath),
            record => record,
            StringComparer.OrdinalIgnoreCase);

        foreach (var record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var health = GetHealth(record);
            if (health is StoreSharedContentHealth.MissingInstanceFile
                or StoreSharedContentHealth.MissingSharedFile
                or StoreSharedContentHealth.BrokenLink)
            {
                plan.BrokenOrMissingCount++;
                continue;
            }

            if (health == StoreSharedContentHealth.HashMismatch)
            {
                plan.HashMismatchCount++;
                continue;
            }

            if (enableSharing)
            {
                if (record.LinkKind == StoreLinkKind.None
                    && !string.IsNullOrWhiteSpace(record.Sha1)
                    && File.Exists(record.FilePath)
                    && await VerifyHashesAsync(record.FilePath, record.Sha1, record.Sha512, cancellationToken))
                {
                    plan.TrackedConvertibleCount++;
                }
                else if (record.LinkKind == StoreLinkKind.None && File.Exists(record.FilePath))
                {
                    plan.HashMismatchCount++;
                }
            }
            else if (record.LinkKind != StoreLinkKind.None)
            {
                plan.SharedInstallCount++;
            }
        }

        var contentRoot = Path.Combine(game.Path.BasePath, installFolderName);
        if (Directory.Exists(contentRoot))
        {
            foreach (var entryPath in Directory.EnumerateFiles(contentRoot, "*", SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!recordsByPath.ContainsKey(NormalizePath(entryPath)))
                {
                    plan.UntrackedFileCount++;
                }
            }
        }

        return plan;
    }

    public async Task<StoreSharedContentMigrationSummary> ApplyMigrationAsync(
        StoreSharedContentMigrationPlan plan,
        StoreSharedContentMigrationAction action,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var summary = new StoreSharedContentMigrationSummary();
        summary.Add(plan);

        if (action is StoreSharedContentMigrationAction.OnlyFutureInstalls
            or StoreSharedContentMigrationAction.LeaveExistingLinks)
        {
            return summary;
        }

        var records = LoadInstallRecords().ToList();
        var matchingRecords = records
            .Where(record => IsRecordForGameAndType(record, plan.Game, plan.ContentType))
            .ToList();

        if (action is StoreSharedContentMigrationAction.ConvertTrackedFiles
            or StoreSharedContentMigrationAction.ConvertAllCompatibleFiles)
        {
            foreach (var record in matchingRecords.Where(record => record.LinkKind == StoreLinkKind.None).ToArray())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!File.Exists(record.FilePath))
                {
                    continue;
                }

                var sha1 = NormalizeHash(record.Sha1);
                if (string.IsNullOrWhiteSpace(sha1))
                {
                    if (action != StoreSharedContentMigrationAction.ConvertAllCompatibleFiles)
                    {
                        continue;
                    }

                    sha1 = await ComputeSha1Async(record.FilePath, cancellationToken);
                }

                if (!string.IsNullOrWhiteSpace(record.Sha1)
                    && !await VerifyHashesAsync(record.FilePath, sha1, record.Sha512, cancellationToken))
                {
                    continue;
                }

                ConvertRecordToShared(plan.Game, plan.InstallFolderName, record, sha1);
                summary.ChangedCount++;
            }

            if (action == StoreSharedContentMigrationAction.ConvertAllCompatibleFiles)
            {
                summary.ChangedCount += await ImportUntrackedFilesAsync(plan, records, cancellationToken);
            }

            SaveInstallRecords(records);
            return summary;
        }

        if (action == StoreSharedContentMigrationAction.MaterializeFiles)
        {
            foreach (var record in matchingRecords.Where(record => record.LinkKind != StoreLinkKind.None))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!string.IsNullOrWhiteSpace(record.SharedFilePath) && File.Exists(record.SharedFilePath))
                {
                    MaterializeSharedRecord(record);
                }

                RemoveManifestReference(record);
                record.GodFolderHash = null;
                record.HashAlgorithm = null;
                record.SharedFilePath = null;
                record.LinkKind = StoreLinkKind.None;
                summary.ChangedCount++;
            }

            SaveInstallRecords(records);
            return summary;
        }

        if (action == StoreSharedContentMigrationAction.RemoveSharedInstalls)
        {
            var removeIds = matchingRecords
                .Where(record => record.LinkKind != StoreLinkKind.None)
                .Select(record => record.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var record in matchingRecords.Where(record => removeIds.Contains(record.Id)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                TryDeleteFile(record.FilePath);
                RemoveManifestReference(record);
                summary.ChangedCount++;
            }

            records.RemoveAll(record => removeIds.Contains(record.Id));
            SaveInstallRecords(records);
        }

        return summary;
    }

    public StoreSharedContentMigrationSummary SummarizeMigrationPlans(IEnumerable<StoreSharedContentMigrationPlan> plans)
    {
        var summary = new StoreSharedContentMigrationSummary();
        foreach (var plan in plans)
        {
            summary.Add(plan);
        }

        return summary;
    }

    public void AddOrUpdateManifestReference(
        string sharedBasePath,
        string installFolderName,
        StoreInstallRecord record)
    {
        if (string.IsNullOrWhiteSpace(record.GodFolderHash)
            || string.IsNullOrWhiteSpace(record.SharedFilePath))
        {
            return;
        }

        var manifest = LoadManifest().ToList();
        var normalizedBasePath = NormalizePath(sharedBasePath);
        var entry = manifest.FirstOrDefault(existing =>
            existing.ContentType == record.ContentType
            && string.Equals(NormalizePath(existing.BasePath), normalizedBasePath, StringComparison.OrdinalIgnoreCase)
            && string.Equals(existing.Hash, record.GodFolderHash, StringComparison.OrdinalIgnoreCase));

        if (entry == null)
        {
            entry = new StoreSharedContentManifestEntry
            {
                ContentType = record.ContentType,
                BasePath = sharedBasePath,
                InstallFolderName = installFolderName,
                Hash = record.GodFolderHash,
                FileName = record.FileName,
                SharedFilePath = record.SharedFilePath,
                FileSizeBytes = GetFileSize(record.SharedFilePath)
            };
            manifest.Add(entry);
        }
        else
        {
            entry.SharedFilePath = record.SharedFilePath;
            entry.FileSizeBytes = GetFileSize(record.SharedFilePath);
        }

        entry.References.RemoveAll(reference =>
            string.Equals(reference.InstallRecordId, record.Id, StringComparison.OrdinalIgnoreCase));
        entry.References.Add(new StoreSharedContentReference
        {
            InstallRecordId = record.Id,
            GamePath = record.GamePath,
            FilePath = record.FilePath,
            FileName = record.FileName,
            ProjectId = record.ProjectId,
            VersionId = record.VersionId
        });

        SaveManifest(manifest);
    }

    private void ConvertRecordToShared(Game game, string installFolderName, StoreInstallRecord record, string sha1)
    {
        var sharedBasePath = ResolveSharedBasePath(game, null)
                             ?? throw new InvalidOperationException("Shared Minecraft base path is required.");
        var sharedFilePath = GetSharedFilePath(sharedBasePath, installFolderName, sha1, record.FileName);
        EnsureSharedFileFromExisting(record.FilePath, sharedFilePath, sha1, record.Sha512);

        var link = _linkService.ReplaceWithLinkOrCopy(
            sharedFilePath,
            record.FilePath,
            _settingsService.GetPreferredLinkMode());

        record.Sha1 = sha1;
        record.GodFolderHash = sha1;
        record.HashAlgorithm = HashAlgorithmName;
        record.SharedFilePath = sharedFilePath;
        record.LinkKind = link.LinkKind;
        AddOrUpdateManifestReference(sharedBasePath, installFolderName, record);
    }

    private async Task<int> ImportUntrackedFilesAsync(
        StoreSharedContentMigrationPlan plan,
        List<StoreInstallRecord> records,
        CancellationToken cancellationToken)
    {
        var changed = 0;
        var contentRoot = Path.Combine(plan.Game.Path.BasePath, plan.InstallFolderName);
        if (!Directory.Exists(contentRoot))
        {
            return changed;
        }

        var trackedPaths = records
            .Where(record => IsRecordForGameAndType(record, plan.Game, plan.ContentType))
            .Select(record => NormalizePath(record.FilePath))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var entryPath in Directory.EnumerateFiles(contentRoot, "*", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (trackedPaths.Contains(NormalizePath(entryPath)))
            {
                continue;
            }

            var sha1 = await ComputeSha1Async(entryPath, cancellationToken);
            var record = new StoreInstallRecord
            {
                ContentType = plan.ContentType,
                GamePath = plan.Game.Path.BasePath,
                ProjectTitle = Path.GetFileNameWithoutExtension(entryPath),
                FileName = Path.GetFileName(entryPath),
                FilePath = entryPath,
                Sha1 = sha1,
                InstalledAtUtc = DateTimeOffset.UtcNow
            };

            ConvertRecordToShared(plan.Game, plan.InstallFolderName, record, sha1);
            records.Add(record);
            changed++;
        }

        return changed;
    }

    private void MaterializeSharedRecord(StoreInstallRecord record)
    {
        if (string.IsNullOrWhiteSpace(record.SharedFilePath) || !File.Exists(record.SharedFilePath))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(record.FilePath)!);
        var tempPath = Path.Combine(
            Path.GetDirectoryName(record.FilePath)!,
            $".emerald-materialize-{Guid.NewGuid():N}{Path.GetExtension(record.FilePath)}");
        File.Copy(record.SharedFilePath, tempPath, overwrite: true);
        TryDeleteFile(record.FilePath);
        File.Move(tempPath, record.FilePath);
    }

    private async Task EnsureSharedFileAsync(
        string sourcePath,
        string sharedFilePath,
        string sha1,
        string? sha512,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(sharedFilePath)!);

        if (File.Exists(sharedFilePath)
            && await VerifyHashesAsync(sharedFilePath, sha1, sha512, cancellationToken))
        {
            return;
        }

        var tempSharedPath = $"{sharedFilePath}.emerald-{Guid.NewGuid():N}.tmp";
        File.Copy(sourcePath, tempSharedPath, overwrite: true);
        if (!await VerifyHashesAsync(tempSharedPath, sha1, sha512, cancellationToken))
        {
            TryDeleteFile(tempSharedPath);
            throw new InvalidOperationException("Shared cache file failed hash verification.");
        }

        if (File.Exists(sharedFilePath))
        {
            File.Delete(sharedFilePath);
        }

        File.Move(tempSharedPath, sharedFilePath);
    }

    private void EnsureSharedFileFromExisting(string sourcePath, string sharedFilePath, string sha1, string? sha512)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(sharedFilePath)!);
        if (File.Exists(sharedFilePath) && VerifyHashes(sharedFilePath, sha1, sha512))
        {
            return;
        }

        var tempSharedPath = $"{sharedFilePath}.emerald-{Guid.NewGuid():N}.tmp";
        File.Copy(sourcePath, tempSharedPath, overwrite: true);
        if (!VerifyHashes(tempSharedPath, sha1, sha512))
        {
            TryDeleteFile(tempSharedPath);
            throw new InvalidOperationException("Shared cache file failed hash verification.");
        }

        if (File.Exists(sharedFilePath))
        {
            File.Delete(sharedFilePath);
        }

        File.Move(tempSharedPath, sharedFilePath);
    }

    private void RemoveManifestReference(StoreInstallRecord record)
    {
        var manifest = LoadManifest().ToList();
        var changed = false;
        foreach (var entry in manifest.ToArray())
        {
            var removed = entry.References.RemoveAll(reference =>
                string.Equals(reference.InstallRecordId, record.Id, StringComparison.OrdinalIgnoreCase)
                || string.Equals(NormalizePath(reference.FilePath), NormalizePath(record.FilePath), StringComparison.OrdinalIgnoreCase));

            if (removed > 0)
            {
                changed = true;
            }

            if (entry.References.Count == 0)
            {
                TryDeleteFile(entry.SharedFilePath);
                manifest.Remove(entry);
                changed = true;
            }
        }

        if (changed)
        {
            SaveManifest(manifest);
        }
    }

    private string? ResolveSharedBasePath(StoreSharedInstallRequest request)
        => ResolveSharedBasePath(request.Game, request.SharedBasePathOverride);

    private static string? ResolveSharedBasePath(Game game, string? sharedBasePathOverride)
        => string.IsNullOrWhiteSpace(sharedBasePathOverride)
            ? game.SharedMinecraftBasePath
            : sharedBasePathOverride;

    private static string GetSharedFilePath(string sharedBasePath, string installFolderName, string sha1, string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return Path.Combine(sharedBasePath, installFolderName, $"{sha1}{extension}");
    }

    private StoreInstallRecord[] LoadInstallRecords()
        => _baseSettingsService.Get(SettingsKeys.StoreInstalledItems, Array.Empty<StoreInstallRecord>());

    private void SaveInstallRecords(IEnumerable<StoreInstallRecord> records)
        => _baseSettingsService.Set(SettingsKeys.StoreInstalledItems, records.ToArray());

    private StoreSharedContentManifestEntry[] LoadManifest()
        => _baseSettingsService.Get(SettingsKeys.StoreSharedContentManifest, Array.Empty<StoreSharedContentManifestEntry>());

    private void SaveManifest(IEnumerable<StoreSharedContentManifestEntry> manifest)
        => _baseSettingsService.Set(SettingsKeys.StoreSharedContentManifest, manifest.ToArray());

    private static bool IsRecordForGameAndType(StoreInstallRecord record, Game game, StoreContentType contentType)
        => record.ContentType == contentType
           && string.Equals(NormalizePath(record.GamePath), NormalizePath(game.Path.BasePath), StringComparison.OrdinalIgnoreCase);

    private static async Task<bool> VerifyHashesAsync(
        string filePath,
        string? sha1,
        string? sha512,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(sha1))
        {
            var actualSha1 = await ComputeSha1Async(filePath, cancellationToken);
            if (!actualSha1.Equals(sha1, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(sha512))
        {
            var actualSha512 = await ComputeHashAsync(SHA512.Create(), filePath, cancellationToken);
            if (!actualSha512.Equals(sha512, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static bool VerifyHashes(string filePath, string? sha1, string? sha512)
    {
        if (!File.Exists(filePath))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(sha1)
            && !ComputeSha1(filePath).Equals(sha1, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(sha512)
            && !ComputeHash(SHA512.Create(), filePath).Equals(sha512, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static Task<string> ComputeSha1Async(string filePath, CancellationToken cancellationToken)
        => ComputeHashAsync(SHA1.Create(), filePath, cancellationToken);

    private static string ComputeSha1(string filePath)
        => ComputeHash(SHA1.Create(), filePath);

    private static async Task<string> ComputeHashAsync(
        HashAlgorithm algorithm,
        string filePath,
        CancellationToken cancellationToken)
    {
        using (algorithm)
        {
            await using var stream = File.OpenRead(filePath);
            var hash = await algorithm.ComputeHashAsync(stream, cancellationToken);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }

    private static string ComputeHash(HashAlgorithm algorithm, string filePath)
    {
        using (algorithm)
        {
            using var stream = File.OpenRead(filePath);
            var hash = algorithm.ComputeHash(stream);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }

    private static string? NormalizeHash(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    private static long? GetFileSize(string filePath)
        => File.Exists(filePath) ? new FileInfo(filePath).Length : null;

    private static string NormalizePath(string path)
        => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static bool IsSymbolicLinkOrReparsePoint(string path)
    {
        try
        {
            return File.GetAttributes(path).HasFlag(System.IO.FileAttributes.ReparsePoint);
        }
        catch
        {
            return false;
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path) || IsSymbolicLinkOrReparsePoint(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            _ = ex;
        }
    }
}
