using Emerald.CoreX.Helpers;

namespace Emerald.CoreX.Store;

public interface IStoreSharedContentService
{
    bool IsSharingEnabled(Game game, StoreContentType contentType);

    bool IsSharingEnabled(Game game, StoreContentType contentType, string? sharedBasePathOverride);

    Task<StoreSharedInstallResult> InstallAsync(StoreSharedInstallRequest request);

    StoreSharedContentHealth GetHealth(StoreInstallRecord record);

    Task RemoveReferenceAsync(StoreInstallRecord record, bool deleteInstanceFile, CancellationToken cancellationToken = default);

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

    private readonly record struct ExpectedHashes(string? Sha1, string? Sha512);

    private readonly IStoreInstallRecordRepository _records;
    private readonly IStoreFileLinkService _linkService;
    private readonly IStoreSharedContentSettingsService _settingsService;

    public StoreSharedContentService(
        IStoreInstallRecordRepository records,
        IStoreFileLinkService linkService,
        IStoreSharedContentSettingsService settingsService)
    {
        _records = records;
        _linkService = linkService;
        _settingsService = settingsService;
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
        var hashes = new ExpectedHashes(
            FileHash.Normalize(request.File.Hashes?.Sha1),
            FileHash.Normalize(request.File.Hashes?.Sha512));

        if (string.IsNullOrWhiteSpace(sharedBasePath) || !settings.IsSharedStoreContentEnabled(request.ContentType))
        {
            return await InstallDirectAsync(request, hashes);
        }

        var cachedInstall = await TryInstallFromSharedCacheAsync(request, sharedBasePath, hashes);
        if (cachedInstall != null)
        {
            return cachedInstall;
        }

        return await DownloadInstallToSharedCacheAsync(request, sharedBasePath, hashes);
    }

    private async Task<StoreSharedInstallResult> InstallDirectAsync(
        StoreSharedInstallRequest request,
        ExpectedHashes hashes)
    {
        await request.DownloadToPathAsync(request.TargetPath, request.Progress, request.CancellationToken);
        return new StoreSharedInstallResult
        {
            FilePath = request.TargetPath,
            Sha1 = hashes.Sha1,
            Sha512 = hashes.Sha512,
            LinkKind = StoreLinkKind.None,
            FileSizeBytes = StorePath.GetFileSize(request.TargetPath)
        };
    }

    private async Task<StoreSharedInstallResult?> TryInstallFromSharedCacheAsync(
        StoreSharedInstallRequest request,
        string sharedBasePath,
        ExpectedHashes hashes)
    {
        if (string.IsNullOrWhiteSpace(hashes.Sha1))
        {
            return null;
        }

        var existingSharedPath = GetSharedFilePath(
            sharedBasePath,
            request.InstallFolderName,
            hashes.Sha1,
            request.File.Filename);

        if (!File.Exists(existingSharedPath)
            || !await FileHash.VerifyAsync(existingSharedPath, hashes.Sha1, hashes.Sha512, request.CancellationToken))
        {
            return null;
        }

        return CreateLinkedInstallResult(request, existingSharedPath, hashes);
    }

    private async Task<StoreSharedInstallResult> DownloadInstallToSharedCacheAsync(
        StoreSharedInstallRequest request,
        string sharedBasePath,
        ExpectedHashes hashes)
    {
        var tempPath = Path.Combine(
            Path.GetTempPath(),
            "Emerald",
            "Store",
            $"{Guid.NewGuid():N}{Path.GetExtension(request.File.Filename)}");
        Directory.CreateDirectory(Path.GetDirectoryName(tempPath)!);

        try
        {
            await request.DownloadToPathAsync(tempPath, request.Progress, request.CancellationToken);

            var actualSha1 = string.IsNullOrWhiteSpace(hashes.Sha1)
                ? await FileHash.ComputeSha1Async(tempPath, request.CancellationToken)
                : hashes.Sha1;

            if (!await FileHash.VerifyAsync(tempPath, actualSha1, hashes.Sha512, request.CancellationToken))
            {
                throw new InvalidOperationException("Downloaded store item failed hash verification.");
            }

            var sharedFilePath = GetSharedFilePath(
                sharedBasePath,
                request.InstallFolderName,
                actualSha1,
                request.File.Filename);

            await EnsureSharedFileAsync(tempPath, sharedFilePath, actualSha1, hashes.Sha512, request.CancellationToken);

            return CreateLinkedInstallResult(request, sharedFilePath, hashes with { Sha1 = actualSha1 });
        }
        finally
        {
            StorePath.TryDeleteFile(tempPath);
        }
    }

    private StoreSharedInstallResult CreateLinkedInstallResult(
        StoreSharedInstallRequest request,
        string sharedFilePath,
        ExpectedHashes hashes)
    {
        var link = _linkService.CreateLinkOrCopy(
            sharedFilePath,
            request.TargetPath,
            _settingsService.GetPreferredLinkMode());

        return new StoreSharedInstallResult
        {
            FilePath = request.TargetPath,
            Sha1 = hashes.Sha1,
            Sha512 = hashes.Sha512,
            SharedFilePath = sharedFilePath,
            LinkKind = link.LinkKind,
            FileSizeBytes = StorePath.GetFileSize(sharedFilePath)
        };
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
        var targetIsSymlink = StorePath.IsReparsePoint(record.FilePath);
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
                var actual = FileHash.ComputeSha1(record.FilePath);
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
            StorePath.TryDeleteFile(record.FilePath);
        }

        if (!string.IsNullOrWhiteSpace(record.GodFolderHash))
        {
            DeleteSharedFileIfUnused(record);
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

        var records = _records.GetForGameAndType(game.Path.BasePath, contentType).ToList();
        var recordsByPath = records.ToDictionary(
            record => StorePath.Normalize(record.FilePath),
            record => record,
            StringComparer.OrdinalIgnoreCase);

        await AddTrackedMigrationCountsAsync(plan, records, enableSharing, cancellationToken);
        AddUntrackedMigrationCount(plan, recordsByPath.Keys, cancellationToken);

        return plan;
    }

    private async Task AddTrackedMigrationCountsAsync(
        StoreSharedContentMigrationPlan plan,
        IEnumerable<StoreInstallRecord> records,
        bool enableSharing,
        CancellationToken cancellationToken)
    {
        foreach (var record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await AddTrackedMigrationCountAsync(plan, record, enableSharing, cancellationToken);
        }
    }

    private async Task AddTrackedMigrationCountAsync(
        StoreSharedContentMigrationPlan plan,
        StoreInstallRecord record,
        bool enableSharing,
        CancellationToken cancellationToken)
    {
        var health = GetHealth(record);
        if (health is StoreSharedContentHealth.MissingInstanceFile
            or StoreSharedContentHealth.MissingSharedFile
            or StoreSharedContentHealth.BrokenLink)
        {
            plan.BrokenOrMissingCount++;
            return;
        }

        if (health == StoreSharedContentHealth.HashMismatch)
        {
            plan.HashMismatchCount++;
            return;
        }

        if (enableSharing)
        {
            await AddEnableMigrationCountAsync(plan, record, cancellationToken);
            return;
        }

        if (record.LinkKind != StoreLinkKind.None)
        {
            plan.SharedInstallCount++;
        }
    }

    private async Task AddEnableMigrationCountAsync(
        StoreSharedContentMigrationPlan plan,
        StoreInstallRecord record,
        CancellationToken cancellationToken)
    {
        if (record.LinkKind != StoreLinkKind.None || !File.Exists(record.FilePath))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(record.Sha1)
            && await FileHash.VerifyAsync(record.FilePath, record.Sha1, record.Sha512, cancellationToken))
        {
            plan.TrackedConvertibleCount++;
            return;
        }

        plan.HashMismatchCount++;
    }

    private static void AddUntrackedMigrationCount(
        StoreSharedContentMigrationPlan plan,
        IEnumerable<string> trackedPaths,
        CancellationToken cancellationToken)
    {
        var contentRoot = Path.Combine(plan.Game.Path.BasePath, plan.InstallFolderName);
        if (!Directory.Exists(contentRoot))
        {
            return;
        }

        var tracked = trackedPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var entryPath in Directory.EnumerateFiles(contentRoot, "*", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!tracked.Contains(StorePath.Normalize(entryPath)))
            {
                plan.UntrackedFileCount++;
            }
        }
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

        var records = _records.GetAll().ToList();
        var matchingRecords = records
            .Where(record => _records.IsForGameAndType(record, plan.Game.Path.BasePath, plan.ContentType))
            .ToList();

        summary.ChangedCount += action switch
        {
            StoreSharedContentMigrationAction.ConvertTrackedFiles
                => await ConvertTrackedFilesAsync(plan, records, matchingRecords, includeUntracked: false, cancellationToken),
            StoreSharedContentMigrationAction.ConvertAllCompatibleFiles
                => await ConvertTrackedFilesAsync(plan, records, matchingRecords, includeUntracked: true, cancellationToken),
            StoreSharedContentMigrationAction.MaterializeFiles
                => MaterializeSharedFiles(records, matchingRecords, cancellationToken),
            StoreSharedContentMigrationAction.RemoveSharedInstalls
                => RemoveSharedInstalls(records, matchingRecords, cancellationToken),
            _ => 0
        };

        _records.Save(records);

        return summary;
    }

    private async Task<int> ConvertTrackedFilesAsync(
        StoreSharedContentMigrationPlan plan,
        List<StoreInstallRecord> records,
        IEnumerable<StoreInstallRecord> matchingRecords,
        bool includeUntracked,
        CancellationToken cancellationToken)
    {
        var changed = 0;
        foreach (var record in matchingRecords.Where(record => record.LinkKind == StoreLinkKind.None).ToArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sha1 = await GetConvertibleSha1Async(record, includeUntracked, cancellationToken);
            if (string.IsNullOrWhiteSpace(sha1))
            {
                continue;
            }

            ConvertRecordToShared(plan.Game, plan.InstallFolderName, record, sha1);
            changed++;
        }

        return includeUntracked
            ? changed + await ImportUntrackedFilesAsync(plan, records, cancellationToken)
            : changed;
    }

    private async Task<string?> GetConvertibleSha1Async(
        StoreInstallRecord record,
        bool allowMissingRecordHash,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(record.FilePath))
        {
            return null;
        }

        var sha1 = FileHash.Normalize(record.Sha1);
        if (string.IsNullOrWhiteSpace(sha1))
        {
            return allowMissingRecordHash
                ? await FileHash.ComputeSha1Async(record.FilePath, cancellationToken)
                : null;
        }

        return await FileHash.VerifyAsync(record.FilePath, sha1, record.Sha512, cancellationToken)
            ? sha1
            : null;
    }

    private int MaterializeSharedFiles(
        List<StoreInstallRecord> records,
        IEnumerable<StoreInstallRecord> matchingRecords,
        CancellationToken cancellationToken)
    {
        var changed = 0;
        foreach (var record in matchingRecords.Where(record => record.LinkKind != StoreLinkKind.None))
        {
            cancellationToken.ThrowIfCancellationRequested();
            MaterializeSharedRecord(record);
            DeleteSharedFileIfUnused(record, records);
            ClearSharedFields(record);
            changed++;
        }

        return changed;
    }

    private int RemoveSharedInstalls(
        List<StoreInstallRecord> records,
        IEnumerable<StoreInstallRecord> matchingRecords,
        CancellationToken cancellationToken)
    {
        var removedRecords = matchingRecords
            .Where(record => record.LinkKind != StoreLinkKind.None)
            .ToArray();

        foreach (var record in removedRecords)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StorePath.TryDeleteFile(record.FilePath);
        }

        var removeIds = removedRecords
            .Select(record => record.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        records.RemoveAll(record => removeIds.Contains(record.Id));

        foreach (var record in removedRecords)
        {
            DeleteSharedFileIfUnused(record, records);
        }

        return removedRecords.Length;
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
    }

    private static void ClearSharedFields(StoreInstallRecord record)
    {
        record.GodFolderHash = null;
        record.HashAlgorithm = null;
        record.SharedFilePath = null;
        record.LinkKind = StoreLinkKind.None;
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
            .Where(record => _records.IsForGameAndType(record, plan.Game.Path.BasePath, plan.ContentType))
            .Select(record => StorePath.Normalize(record.FilePath))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var entryPath in Directory.EnumerateFiles(contentRoot, "*", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (trackedPaths.Contains(StorePath.Normalize(entryPath)))
            {
                continue;
            }

            var sha1 = await FileHash.ComputeSha1Async(entryPath, cancellationToken);
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
        StorePath.TryDeleteFile(record.FilePath);
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
            && await FileHash.VerifyAsync(sharedFilePath, sha1, sha512, cancellationToken))
        {
            return;
        }

        var tempSharedPath = $"{sharedFilePath}.emerald-{Guid.NewGuid():N}.tmp";
        File.Copy(sourcePath, tempSharedPath, overwrite: true);
        if (!await FileHash.VerifyAsync(tempSharedPath, sha1, sha512, cancellationToken))
        {
            StorePath.TryDeleteFile(tempSharedPath);
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
        if (File.Exists(sharedFilePath) && FileHash.Verify(sharedFilePath, sha1, sha512))
        {
            return;
        }

        var tempSharedPath = $"{sharedFilePath}.emerald-{Guid.NewGuid():N}.tmp";
        File.Copy(sourcePath, tempSharedPath, overwrite: true);
        if (!FileHash.Verify(tempSharedPath, sha1, sha512))
        {
            StorePath.TryDeleteFile(tempSharedPath);
            throw new InvalidOperationException("Shared cache file failed hash verification.");
        }

        if (File.Exists(sharedFilePath))
        {
            File.Delete(sharedFilePath);
        }

        File.Move(tempSharedPath, sharedFilePath);
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

    private void DeleteSharedFileIfUnused(
        StoreInstallRecord removedRecord,
        IEnumerable<StoreInstallRecord>? records = null)
    {
        if (string.IsNullOrWhiteSpace(removedRecord.SharedFilePath))
        {
            return;
        }

        var remainingRecords = records ?? _records.GetAll();
        if (HasOtherSharedReference(remainingRecords, removedRecord))
        {
            return;
        }

        StorePath.TryDeleteFile(removedRecord.SharedFilePath);
    }

    private static bool HasOtherSharedReference(
        IEnumerable<StoreInstallRecord> records,
        StoreInstallRecord removedRecord)
    {
        var sharedPath = StorePath.Normalize(removedRecord.SharedFilePath!);
        var removedFilePath = StorePath.Normalize(removedRecord.FilePath);
        return records.Any(record =>
            !string.Equals(record.Id, removedRecord.Id, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(StorePath.Normalize(record.FilePath), removedFilePath, StringComparison.OrdinalIgnoreCase)
            && record.LinkKind != StoreLinkKind.None
            && !string.IsNullOrWhiteSpace(record.SharedFilePath)
            && string.Equals(StorePath.Normalize(record.SharedFilePath), sharedPath, StringComparison.OrdinalIgnoreCase));
    }
}
