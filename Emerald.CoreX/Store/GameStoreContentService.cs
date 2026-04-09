using System.Collections.ObjectModel;
using Emerald.CoreX.Helpers;
using Emerald.CoreX.Runtime;
using Emerald.CoreX.Store.Modrinth;
using Emerald.CoreX.Store.Modrinth.JSON;
using Emerald.Services;
using Microsoft.Extensions.Logging;
using GameVersionType = Emerald.CoreX.Versions.Type;

namespace Emerald.CoreX.Store;

public sealed class GameStoreContentService : IGameStoreContentService
{
    private static readonly string[] PluginStrictLoaders =
    [
        "paper",
        "spigot",
        "bukkit",
        "purpur",
        "folia",
        "sponge",
        "velocity",
        "waterfall",
        "bungeecord",
        "geyser"
    ];

    private readonly IBaseSettingsService _baseSettingsService;
    private readonly IGameRuntimeService _runtimeService;
    private readonly ILogger<GameStoreContentService> _logger;
    private readonly Dictionary<StoreContentType, IModrinthStore> _stores;

    public GameStoreContentService(
        IBaseSettingsService baseSettingsService,
        IGameRuntimeService runtimeService,
        IEnumerable<IModrinthStore> stores,
        ILogger<GameStoreContentService> logger)
    {
        _baseSettingsService = baseSettingsService;
        _runtimeService = runtimeService;
        _logger = logger;
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
        cancellationToken.ThrowIfCancellationRequested();
        EnsureGameIsNotRunning(game);

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

        await store.DownloadItemAsync(file, progress, cancellationToken);

        var targetPath = Path.Combine(game.Path.BasePath, store.InstallFolderName, file.Filename);
        var records = LoadRecords().ToList();
        records.RemoveAll(existing =>
            existing.ContentType == contentType
            && string.Equals(NormalizePath(existing.GamePath), NormalizePath(game.Path.BasePath), StringComparison.OrdinalIgnoreCase)
            && string.Equals(NormalizePath(existing.FilePath), NormalizePath(targetPath), StringComparison.OrdinalIgnoreCase));

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
            Sha1 = file.Hashes?.Sha1,
            Sha512 = file.Hashes?.Sha512,
            InstalledAtUtc = DateTimeOffset.UtcNow
        };

        records.Add(record);
        SaveRecords(records);

        return ToInstalledItem(record, isDirectory: false, fileSizeBytes: new FileInfo(targetPath).Exists ? new FileInfo(targetPath).Length : null);
    }

    public Task<IReadOnlyList<InstalledStoreItem>> GetInstalledItemsAsync(
        Game game,
        StoreContentType contentType,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var store = GetStore(contentType);
        var contentRoot = Path.Combine(game.Path.BasePath, store.InstallFolderName);
        var normalizedGamePath = NormalizePath(game.Path.BasePath);
        var normalizedRoot = NormalizePath(contentRoot);

        var records = LoadRecords().ToList();
        var staleRecords = records
            .Where(record =>
                record.ContentType == contentType
                && string.Equals(NormalizePath(record.GamePath), normalizedGamePath, StringComparison.OrdinalIgnoreCase)
                && (!Path.Exists(record.FilePath) || !IsPathInsideRoot(record.FilePath, normalizedRoot)))
            .ToList();

        if (staleRecords.Count > 0)
        {
            records.RemoveAll(record => staleRecords.Any(stale => stale.Id == record.Id));
            SaveRecords(records);
        }

        var trackedRecords = records
            .Where(record =>
                record.ContentType == contentType
                && string.Equals(NormalizePath(record.GamePath), normalizedGamePath, StringComparison.OrdinalIgnoreCase)
                && Path.Exists(record.FilePath))
            .ToList();

        var trackedByPath = trackedRecords.ToDictionary(
            record => NormalizePath(record.FilePath),
            record => record,
            StringComparer.OrdinalIgnoreCase);

        var installed = new List<InstalledStoreItem>();
        foreach (var record in trackedRecords)
        {
            var isDirectory = Directory.Exists(record.FilePath);
            long? size = null;
            if (!isDirectory && File.Exists(record.FilePath))
            {
                size = new FileInfo(record.FilePath).Length;
            }

            installed.Add(ToInstalledItem(record, isDirectory, size));
        }

        if (Directory.Exists(contentRoot))
        {
            foreach (var entryPath in Directory.EnumerateFileSystemEntries(contentRoot, "*", SearchOption.TopDirectoryOnly))
            {
                var normalizedEntry = NormalizePath(entryPath);
                if (trackedByPath.ContainsKey(normalizedEntry))
                {
                    continue;
                }

                var isDirectory = Directory.Exists(entryPath);
                long? size = null;
                if (!isDirectory && File.Exists(entryPath))
                {
                    size = new FileInfo(entryPath).Length;
                }

                installed.Add(new InstalledStoreItem
                {
                    ContentType = contentType,
                    GamePath = game.Path.BasePath,
                    DisplayName = Path.GetFileName(entryPath),
                    FileName = Path.GetFileName(entryPath),
                    FilePath = entryPath,
                    IsTracked = false,
                    IsDirectory = isDirectory,
                    FileSizeBytes = size
                });
            }
        }

        var ordered = installed
            .OrderByDescending(item => item.IsTracked)
            .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Task.FromResult<IReadOnlyList<InstalledStoreItem>>(ordered);
    }

    public Task<bool> RemoveAsync(
        Game game,
        StoreContentType contentType,
        InstalledStoreItem item,
        bool forceUntracked = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureGameIsNotRunning(game);

        if (!item.IsTracked && !forceUntracked)
        {
            _logger.LogInformation(
                "Skipping untracked remove for {FilePath} because force mode is disabled.",
                item.FilePath);
            return Task.FromResult(false);
        }

        var store = GetStore(contentType);
        var contentRoot = NormalizePath(Path.Combine(game.Path.BasePath, store.InstallFolderName));
        var targetPath = NormalizePath(item.FilePath);
        if (!IsPathInsideRoot(targetPath, contentRoot))
        {
            _logger.LogWarning("Refusing to remove path outside of store content root. Root: {Root}. Path: {Path}", contentRoot, targetPath);
            return Task.FromResult(false);
        }

        var removedFromDisk = false;
        if (File.Exists(targetPath))
        {
            File.Delete(targetPath);
            removedFromDisk = true;
        }
        else if (Directory.Exists(targetPath))
        {
            Directory.Delete(targetPath, true);
            removedFromDisk = true;
        }

        var records = LoadRecords().ToList();
        var removedTracked = records.RemoveAll(record =>
            record.ContentType == contentType
            && string.Equals(NormalizePath(record.GamePath), NormalizePath(game.Path.BasePath), StringComparison.OrdinalIgnoreCase)
            && string.Equals(NormalizePath(record.FilePath), targetPath, StringComparison.OrdinalIgnoreCase)) > 0;

        if (removedTracked)
        {
            SaveRecords(records);
        }

        _logger.LogInformation(
            "Remove store item completed. RemovedFromDisk: {RemovedFromDisk}. RemovedTrackedRecord: {RemovedTrackedRecord}. Path: {Path}.",
            removedFromDisk,
            removedTracked,
            targetPath);

        return Task.FromResult(removedFromDisk || removedTracked);
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

    private static string[] ResolveStrictLoaders(Game game, StoreContentType contentType)
    {
        return contentType switch
        {
            StoreContentType.Mod => MapModLoader(game.Version.Type),
            StoreContentType.ResourcePack => [],
            StoreContentType.DataPack => ["datapack"],
            StoreContentType.Shader => game.Version.Type == GameVersionType.OptiFine ? ["optifine"] : ["vanilla"],
            StoreContentType.Plugin => PluginStrictLoaders,
            _ => []
        };
    }

    private static string[] MapModLoader(GameVersionType type)
    {
        return type switch
        {
            GameVersionType.Fabric => ["fabric"],
            GameVersionType.Forge => ["forge"],
            GameVersionType.Quilt => ["quilt"],
            GameVersionType.LiteLoader => ["liteloader"],
            GameVersionType.OptiFine => ["optifine"],
            _ => ["vanilla"]
        };
    }

    private StoreInstallRecord[] LoadRecords()
        => _baseSettingsService.Get(SettingsKeys.StoreInstalledItems, Array.Empty<StoreInstallRecord>());

    private void SaveRecords(IEnumerable<StoreInstallRecord> records)
        => _baseSettingsService.Set(SettingsKeys.StoreInstalledItems, records.ToArray());

    private static InstalledStoreItem ToInstalledItem(StoreInstallRecord record, bool isDirectory, long? fileSizeBytes)
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
            Sha512 = record.Sha512
        };
    }

    private static bool IsPathInsideRoot(string path, string root)
    {
        var normalizedPath = NormalizePath(path);
        var normalizedRoot = NormalizePath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.Equals(normalizedPath, normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var rootWithSeparator = normalizedRoot + Path.DirectorySeparatorChar;
        return normalizedPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path)
        => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
