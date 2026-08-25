using Emerald.CoreX.Runtime;
using Emerald.CoreX.Installation;
using Emerald.CoreX.Store.Modrinth;
using Emerald.CoreX.Store.Modrinth.JSON;
using Microsoft.Extensions.Logging;
using GameVersionType = Emerald.CoreX.Versions.Type;

namespace Emerald.CoreX.Store;

public sealed class GameStoreContentService : IGameStoreContentService
{
    private readonly IStoreInstallRecordRepository _records;
    private readonly IGameRuntimeService _runtimeService;
    private readonly IStoreSharedContentService _sharedContentService;
    private readonly ILogger<GameStoreContentService> _logger;
    private readonly IDownloadActivityService _downloadActivity;
    private readonly Dictionary<StoreContentType, IModrinthStore> _stores;

    public GameStoreContentService(
        IStoreInstallRecordRepository records,
        IGameRuntimeService runtimeService,
        IStoreSharedContentService sharedContentService,
        IEnumerable<IModrinthStore> stores,
        ILogger<GameStoreContentService> logger,
        IDownloadActivityService? downloadActivity = null)
    {
        _records = records;
        _runtimeService = runtimeService;
        _sharedContentService = sharedContentService;
        _logger = logger;
        _downloadActivity = downloadActivity ?? new DownloadActivityService();
        _stores = stores
            .GroupBy(store => store.ContentType)
            .ToDictionary(group => group.Key, group => group.First());
    }

    public async Task<StoreCompatibilityResult> GetCompatibleVersionsAsync(
        Game game,
        StoreContentType contentType,
        string projectId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var store = GetStore(contentType);
        var gameVersion = string.IsNullOrWhiteSpace(game.Version.BasedOn)
            ? null
            : new[] { game.Version.BasedOn };
        var strictLoaders = ResolveStrictLoaders(game, contentType);
        store.MCPath = game.Path;

        var strict = await store.GetVersionsAsync(
            projectId,
            gameVersion,
            strictLoaders.Length == 0 ? null : strictLoaders);

        if (strict is { Count: > 0 })
        {
            return new StoreCompatibilityResult
            {
                Versions = strict
            };
        }

        var fallbackByVersion = await store.GetVersionsAsync(projectId, gameVersion);
        if (fallbackByVersion is { Count: > 0 })
        {
            return new StoreCompatibilityResult
            {
                Versions = fallbackByVersion,
                UsedFallback = true,
                Notice = "No strict compatibility match found. Showing versions filtered by game version only."
            };
        }

        var allVersions = await store.GetVersionsAsync(projectId) ?? [];
        return new StoreCompatibilityResult
        {
            Versions = allVersions,
            UsedFallback = true,
            Notice = "No strict compatibility match found. Showing all available versions."
        };
    }

    public async Task<InstalledStoreItem> InstallAsync(
        Game game,
        StoreContentType contentType,
        StoreItem project,
        ItemVersion version,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        using var downloadLease = await _downloadActivity.AcquireDownloadAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureGameIsNotRunning(game);
        PrepareBaseScopedStore(game);

        var store = GetStore(contentType);
        store.MCPath = game.Path;

        var file = version.Files.FirstOrDefault(file => file.Primary) ?? version.Files.FirstOrDefault();
        if (file == null)
        {
            throw new InvalidOperationException("The selected version does not have a downloadable file.");
        }

        _logger.LogInformation(
            "Installing store item {ProjectTitle} ({ContentType}) for game path {GamePath}.",
            project.Title,
            contentType,
            game.Path.BasePath);

        var targetPath = Path.Combine(game.Path.BasePath, store.InstallFolderName, file.Filename);
        var records = _records.GetAll().ToList();
        var existingRecords = _records
            .FindByFilePath(contentType, targetPath, game.Path.BasePath)
            .ToArray();

        records.RemoveAll(existing => existingRecords.Any(removed => removed.Id == existing.Id));
        if (existingRecords.Length > 0)
        {
            _records.Save(records);
        }

        foreach (var existing in existingRecords)
        {
            await _sharedContentService.RemoveReferenceAsync(existing, deleteInstanceFile: true, cancellationToken);
        }

        var installResult = await _sharedContentService.InstallAsync(new StoreSharedInstallRequest
        {
            Game = game,
            ContentType = contentType,
            InstallFolderName = store.InstallFolderName,
            File = file,
            TargetPath = targetPath,
            DownloadToPathAsync = (path, installProgress, token) =>
                store.DownloadItemToPathAsync(file, path, installProgress, token),
            Progress = progress,
            CancellationToken = cancellationToken
        });

        var godFolderHash = string.IsNullOrWhiteSpace(installResult.SharedFilePath)
            ? null
            : installResult.Sha1;

        var record = new StoreInstallRecord
        {
            ContentType = contentType,
            GamePath = game.Path.BasePath,
            ProjectId = project.ID,
            ProjectTitle = project.Title,
            VersionId = version.ID,
            VersionName = version.Name,
            FileName = file.Filename,
            FilePath = targetPath,
            Sha1 = installResult.Sha1 ?? file.Hashes?.Sha1,
            Sha512 = installResult.Sha512 ?? file.Hashes?.Sha512,
            GodFolderHash = godFolderHash,
            HashAlgorithm = string.IsNullOrWhiteSpace(godFolderHash) ? null : "sha1",
            SharedFilePath = installResult.SharedFilePath,
            LinkKind = installResult.LinkKind,
            DownloadUrl = file.Url,
            InstalledAtUtc = DateTimeOffset.UtcNow
        };

        records.Add(record);
        _records.Save(records);

        return ToInstalledItem(
            record,
            isDirectory: false,
            fileSizeBytes: installResult.FileSizeBytes ?? StorePath.GetFileSize(targetPath),
            health: _sharedContentService.GetHealth(record),
            existsOnDisk: File.Exists(targetPath));
    }

    public async Task<IReadOnlyList<InstalledStoreItem>> GetInstalledItemsAsync(
        Game game,
        StoreContentType contentType,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PrepareBaseScopedStore(game);

        var store = GetStore(contentType);
        var contentRoot = Path.Combine(game.Path.BasePath, store.InstallFolderName);
        var normalizedRoot = StorePath.Normalize(contentRoot);
        var records = _records.GetAll().ToList();
        await RemoveStaleRecordsAsync(game, contentType, normalizedRoot, records, cancellationToken);

        var trackedRecords = GetTrackedRecords(game, contentType, normalizedRoot, records);
        var trackedByPath = trackedRecords.ToDictionary(
            record => StorePath.Normalize(record.FilePath),
            record => record,
            StringComparer.OrdinalIgnoreCase);
        var installed = new List<InstalledStoreItem>();
        AddTrackedItems(installed, trackedRecords);
        AddUntrackedItems(installed, game, contentType, contentRoot, trackedByPath);

        var ordered = installed
            .OrderByDescending(item => item.IsTracked)
            .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return ordered;
    }

    private async Task RemoveStaleRecordsAsync(
        Game game,
        StoreContentType contentType,
        string normalizedRoot,
        List<StoreInstallRecord> records,
        CancellationToken cancellationToken)
    {
        var staleRecords = records
            .Where(record =>
                _records.IsForGameAndType(record, game.Path.BasePath, contentType)
                && !StorePath.IsInsideRoot(record.FilePath, normalizedRoot))
            .ToList();

        if (staleRecords.Count == 0)
        {
            return;
        }

        records.RemoveAll(record => staleRecords.Any(stale => stale.Id == record.Id));
        _records.Save(records);

        foreach (var staleRecord in staleRecords)
        {
            await _sharedContentService.RemoveReferenceAsync(staleRecord, deleteInstanceFile: false, cancellationToken);
        }
    }

    private IReadOnlyList<StoreInstallRecord> GetTrackedRecords(
        Game game,
        StoreContentType contentType,
        string normalizedRoot,
        IEnumerable<StoreInstallRecord> records)
        => records
            .Where(record =>
                _records.IsForGameAndType(record, game.Path.BasePath, contentType)
                && StorePath.IsInsideRoot(record.FilePath, normalizedRoot))
            .ToArray();

    private void AddTrackedItems(
        ICollection<InstalledStoreItem> installed,
        IEnumerable<StoreInstallRecord> trackedRecords)
    {
        foreach (var record in trackedRecords)
        {
            installed.Add(ToTrackedInstalledItem(record));
        }
    }

    private InstalledStoreItem ToTrackedInstalledItem(StoreInstallRecord record)
    {
        var isDirectory = Directory.Exists(record.FilePath);
        var existsOnDisk = File.Exists(record.FilePath) || Directory.Exists(record.FilePath);
        var size = !isDirectory && File.Exists(record.FilePath)
            ? new FileInfo(record.FilePath).Length
            : (long?)null;

        return ToInstalledItem(
            record,
            isDirectory,
            size,
            _sharedContentService.GetHealth(record),
            existsOnDisk);
    }

    private static void AddUntrackedItems(
        ICollection<InstalledStoreItem> installed,
        Game game,
        StoreContentType contentType,
        string contentRoot,
        IReadOnlyDictionary<string, StoreInstallRecord> trackedByPath)
    {
        if (!Directory.Exists(contentRoot))
        {
            return;
        }

        foreach (var entryPath in Directory.EnumerateFileSystemEntries(contentRoot, "*", SearchOption.TopDirectoryOnly))
        {
            if (!trackedByPath.ContainsKey(StorePath.Normalize(entryPath)))
            {
                installed.Add(CreateUntrackedItem(game, contentType, entryPath));
            }
        }
    }

    private static InstalledStoreItem CreateUntrackedItem(
        Game game,
        StoreContentType contentType,
        string entryPath)
    {
        var isDirectory = Directory.Exists(entryPath);
        var size = !isDirectory && File.Exists(entryPath)
            ? new FileInfo(entryPath).Length
            : (long?)null;

        return new InstalledStoreItem
        {
            ContentType = contentType,
            GamePath = game.Path.BasePath,
            DisplayName = Path.GetFileName(entryPath),
            FileName = Path.GetFileName(entryPath),
            FilePath = entryPath,
            IsTracked = false,
            IsDirectory = isDirectory,
            FileSizeBytes = size,
            Health = StoreSharedContentHealth.Untracked
        };
    }

    public async Task<bool> RemoveAsync(
        Game game,
        StoreContentType contentType,
        InstalledStoreItem item,
        bool forceUntracked = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureGameIsNotRunning(game);
        PrepareBaseScopedStore(game);

        if (!item.IsTracked && !forceUntracked)
        {
            _logger.LogInformation(
                "Skipping untracked remove for {FilePath} because force mode is disabled.",
                item.FilePath);
            return false;
        }

        var store = GetStore(contentType);
        var contentRoot = StorePath.Normalize(Path.Combine(game.Path.BasePath, store.InstallFolderName));
        var targetPath = StorePath.Normalize(item.FilePath);
        if (!StorePath.IsInsideRoot(targetPath, contentRoot))
        {
            _logger.LogWarning("Refusing to remove path outside of store content root. Root: {Root}. Path: {Path}", contentRoot, targetPath);
            return false;
        }

        var removedFromDisk = false;
        if (File.Exists(targetPath) || StorePath.IsReparsePoint(targetPath))
        {
            File.Delete(targetPath);
            removedFromDisk = true;
        }
        else if (Directory.Exists(targetPath))
        {
            Directory.Delete(targetPath, true);
            removedFromDisk = true;
        }

        var records = _records.GetAll().ToList();
        var removedRecords = _records
            .FindByFilePath(contentType, targetPath, game.Path.BasePath)
            .ToArray();

        var removedTracked = records.RemoveAll(record => removedRecords.Any(removed => removed.Id == record.Id)) > 0;

        if (removedTracked)
        {
            _records.Save(records);

            foreach (var removedRecord in removedRecords)
            {
                await _sharedContentService.RemoveReferenceAsync(removedRecord, deleteInstanceFile: false, cancellationToken);
            }
        }

        _logger.LogInformation(
            "Remove store item completed. RemovedFromDisk: {RemovedFromDisk}. RemovedTrackedRecord: {RemovedTrackedRecord}. Path: {Path}.",
            removedFromDisk,
            removedTracked,
            targetPath);

        return removedFromDisk || removedTracked;
    }

    private IModrinthStore GetStore(StoreContentType contentType)
    {
        if (_stores.TryGetValue(contentType, out var store))
        {
            return store;
        }

        throw new InvalidOperationException($"No Modrinth store registration exists for content type '{contentType}'.");
    }

    private void EnsureGameIsNotRunning(Game game)
    {
        if (_runtimeService.TryGetActiveSession(game) != null)
        {
            throw new InvalidOperationException("Stop the game before managing store content for this instance.");
        }
    }

    private void PrepareBaseScopedStore(Game game)
    {
        if (!string.IsNullOrWhiteSpace(game.SharedMinecraftBasePath))
        {
            _records.LoadForBasePath(game.SharedMinecraftBasePath);
        }
    }

    private static string[] ResolveStrictLoaders(Game game, StoreContentType contentType)
    {
        return contentType switch
        {
            StoreContentType.Mod => MapModLoader(game.Version.Type),
            StoreContentType.ResourcePack => [],
            StoreContentType.DataPack => ["datapack"],
            StoreContentType.Shader => game.Version.Type == GameVersionType.OptiFine ? ["optifine"] : ["vanilla"],
            StoreContentType.ModPack => [],
            _ => []
        };
    }

    private static string[] MapModLoader(GameVersionType type)
    {
        return type switch
        {
            GameVersionType.Fabric => ["fabric"],
            GameVersionType.Forge => ["forge"],
            GameVersionType.NeoForge => ["neoforge"],
            GameVersionType.Quilt => ["quilt"],
            GameVersionType.LiteLoader => ["liteloader"],
            GameVersionType.OptiFine => ["optifine"],
            _ => ["vanilla"]
        };
    }

    private static InstalledStoreItem ToInstalledItem(
        StoreInstallRecord record,
        bool isDirectory,
        long? fileSizeBytes,
        StoreSharedContentHealth health,
        bool existsOnDisk)
    {
        return new InstalledStoreItem
        {
            Id = record.Id,
            ContentType = record.ContentType,
            GamePath = record.GamePath,
            DisplayName = string.IsNullOrWhiteSpace(record.ProjectTitle) ? record.FileName : record.ProjectTitle,
            FileName = record.FileName,
            FilePath = record.FilePath,
            IsTracked = true,
            IsDirectory = isDirectory,
            FileSizeBytes = fileSizeBytes,
            InstalledAtUtc = record.InstalledAtUtc,
            ProjectId = record.ProjectId,
            VersionId = record.VersionId,
            ProjectTitle = record.ProjectTitle,
            VersionName = record.VersionName,
            Sha1 = record.Sha1,
            Sha512 = record.Sha512,
            GodFolderHash = record.GodFolderHash,
            SharedFilePath = record.SharedFilePath,
            LinkKind = record.LinkKind,
            Health = health,
            ExistsOnDisk = existsOnDisk
        };
    }
}
