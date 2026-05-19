using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using CmlLib.Core;
using Emerald.CoreX;
using Emerald.CoreX.Helpers;
using Emerald.CoreX.Models;
using Emerald.CoreX.Runtime;
using Emerald.CoreX.Services;
using Emerald.CoreX.Store;
using Emerald.CoreX.Store.Modrinth;
using Emerald.CoreX.Store.Modrinth.JSON;
using Emerald.CoreX.Tests.Support;
using Emerald.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using GameVersion = Emerald.CoreX.Versions.Version;
using GameVersionType = Emerald.CoreX.Versions.Type;

namespace Emerald.CoreX.Tests.Store;

[Collection(IocCollection.Name)]
public sealed class GameStoreContentServiceTests
{
    [Fact]
    public void StoreFolderMappings_AreCorrect_ForSupportedContentTypes()
    {
        using var temp = new TemporaryDirectory();
        var path = new MinecraftPath(temp.Path);

        Assert.Equal("mods", new ModStore(path, NullLogger<ModStore>.Instance).InstallFolderName);
        Assert.Equal("resourcepacks", new ResourcePackStore(path, NullLogger<ResourcePackStore>.Instance).InstallFolderName);
        Assert.Equal("datapacks", new DataPackStore(path, NullLogger<DataPackStore>.Instance).InstallFolderName);
        Assert.Equal("shaderpacks", new ShaderStore(path, NullLogger<ShaderStore>.Instance).InstallFolderName);
        Assert.Equal("plugins", new PluginStore(path, NullLogger<PluginStore>.Instance).InstallFolderName);
        Assert.Equal("modpacks", new ModPackStore(path, NullLogger<ModPackStore>.Instance).InstallFolderName);
        Assert.Equal(StoreContentType.ModPack, new ModPackStore(path, NullLogger<ModPackStore>.Instance).ContentType);
    }

    [Fact]
    public async Task GetCompatibleVersionsAsync_UsesFallback_WhenStrictLoaderMatchFails()
    {
        var baseSettings = new InMemoryBaseSettingsService();
        var runtime = new FakeRuntimeService();
        var fakeStore = new FakeModrinthStore(StoreContentType.Mod, "mods")
        {
            OnGetVersionsAsync = (_, _, loaders) =>
            {
                if (loaders is { Length: > 0 })
                {
                    return Task.FromResult<List<ItemVersion>?>([]);
                }

                return Task.FromResult<List<ItemVersion>?>([CreateVersion("fallback")]);
            }
        };

        var service = CreateService(baseSettings, runtime, fakeStore);
        using var temp = new TemporaryDirectory();
        var game = CreateGame(temp.Path, GameVersionType.Fabric, "1.21.4");

        var result = await service.GetCompatibleVersionsAsync(game, StoreContentType.Mod, "project-id");

        Assert.True(result.UsedFallback);
        Assert.NotEmpty(result.Versions);
        Assert.Equal(2, fakeStore.VersionCalls.Count);
        Assert.Equal("fabric", fakeStore.VersionCalls[0].Loaders?[0]);
        Assert.Null(fakeStore.VersionCalls[1].Loaders);
    }

    [Fact]
    public async Task InstallAndGetInstalledItems_PersistsTrackedInstall_AndMarksMissingFiles()
    {
        var baseSettings = new InMemoryBaseSettingsService();
        var runtime = new FakeRuntimeService();
        var fakeStore = new FakeModrinthStore(StoreContentType.Mod, "mods");
        var service = CreateService(baseSettings, runtime, fakeStore);

        using var temp = new TemporaryDirectory();
        var game = CreateGame(temp.Path, GameVersionType.Fabric, "1.21.4");
        var project = CreateProject("abc", "Sodium");
        var version = CreateVersion("v1", "sodium.jar");

        var installed = await service.InstallAsync(game, StoreContentType.Mod, project, version);
        Assert.True(File.Exists(installed.FilePath));

        var firstRead = await service.GetInstalledItemsAsync(game, StoreContentType.Mod);
        Assert.Single(firstRead);
        Assert.True(firstRead[0].IsTracked);

        File.Delete(installed.FilePath);

        var secondRead = await service.GetInstalledItemsAsync(game, StoreContentType.Mod);
        var missing = Assert.Single(secondRead);
        Assert.True(missing.IsTracked);
        Assert.Equal(StoreSharedContentHealth.MissingInstanceFile, missing.Health);
        Assert.NotEmpty(baseSettings.Peek<StoreInstallRecord[]>(SettingsKeys.StoreInstalledItems) ?? []);
    }

    [Fact]
    public async Task InstallAsync_UsesSharedCache_AndSkipsSecondDownload_WhenHashExists()
    {
        var baseSettings = new InMemoryBaseSettingsService();
        var runtime = new FakeRuntimeService();
        var fakeStore = new FakeModrinthStore(StoreContentType.Mod, "mods");
        var service = CreateService(baseSettings, runtime, fakeStore, out InMemoryMinecraftBaseSettingsService minecraftBaseSettings);

        using var temp = new TemporaryDirectory();
        var firstGame = CreateGame(
            Path.Combine(temp.Path, "Instances", "One"),
            GameVersionType.Fabric,
            "1.21.4",
            sharedBasePath: temp.Path,
            settings => settings.UseSharedStoreModsPath = true);
        var secondGame = CreateGame(
            Path.Combine(temp.Path, "Instances", "Two"),
            GameVersionType.Fabric,
            "1.21.4",
            sharedBasePath: temp.Path,
            settings => settings.UseSharedStoreModsPath = true);
        var version = CreateVersion("v1", "sodium.jar", FakeModrinthStore.FileBytes);
        var project = CreateProject("abc", "Sodium");

        var first = await service.InstallAsync(firstGame, StoreContentType.Mod, project, version);
        var second = await service.InstallAsync(secondGame, StoreContentType.Mod, project, version);

        Assert.Equal(1, fakeStore.DownloadCount);
        Assert.Equal(StoreLinkKind.Copy, first.LinkKind);
        Assert.Equal(StoreLinkKind.Copy, second.LinkKind);
        Assert.True(File.Exists(first.SharedFilePath));
        Assert.Equal(first.SharedFilePath, second.SharedFilePath);

        var records = minecraftBaseSettings.Peek<StoreInstallRecord[]>(temp.Path, SettingsKeys.StoreInstalledItems) ?? [];
        Assert.Equal(2, records.Count(record => record.SharedFilePath == first.SharedFilePath));
    }

    [Fact]
    public async Task RemoveAsync_CleansSharedCacheOnlyAfterLastReference()
    {
        var baseSettings = new InMemoryBaseSettingsService();
        var runtime = new FakeRuntimeService();
        var fakeStore = new FakeModrinthStore(StoreContentType.Mod, "mods");
        var service = CreateService(baseSettings, runtime, fakeStore, out InMemoryMinecraftBaseSettingsService minecraftBaseSettings);

        using var temp = new TemporaryDirectory();
        var firstGame = CreateGame(
            Path.Combine(temp.Path, "Instances", "One"),
            GameVersionType.Fabric,
            "1.21.4",
            temp.Path,
            settings => settings.UseSharedStoreModsPath = true);
        var secondGame = CreateGame(
            Path.Combine(temp.Path, "Instances", "Two"),
            GameVersionType.Fabric,
            "1.21.4",
            temp.Path,
            settings => settings.UseSharedStoreModsPath = true);
        var version = CreateVersion("v1", "sodium.jar", FakeModrinthStore.FileBytes);
        var project = CreateProject("abc", "Sodium");

        var first = await service.InstallAsync(firstGame, StoreContentType.Mod, project, version);
        var second = await service.InstallAsync(secondGame, StoreContentType.Mod, project, version);
        var sharedPath = first.SharedFilePath!;

        Assert.True(await service.RemoveAsync(firstGame, StoreContentType.Mod, first));
        Assert.True(File.Exists(sharedPath));
        Assert.Single(
            minecraftBaseSettings.Peek<StoreInstallRecord[]>(temp.Path, SettingsKeys.StoreInstalledItems) ?? [],
            record => record.SharedFilePath == sharedPath);

        Assert.True(await service.RemoveAsync(secondGame, StoreContentType.Mod, second));
        Assert.False(File.Exists(sharedPath));
        Assert.Empty(minecraftBaseSettings.Peek<StoreInstallRecord[]>(temp.Path, SettingsKeys.StoreInstalledItems) ?? []);
    }

    [Fact]
    public async Task Migration_EnableSharedFolder_ConvertsTrackedFiles()
    {
        var baseSettings = new InMemoryBaseSettingsService();
        var runtime = new FakeRuntimeService();
        var fakeStore = new FakeModrinthStore(StoreContentType.Mod, "mods");
        var service = CreateService(baseSettings, runtime, fakeStore, out var sharedContent, out InMemoryMinecraftBaseSettingsService minecraftBaseSettings);

        using var temp = new TemporaryDirectory();
        var game = CreateGame(
            Path.Combine(temp.Path, "Instances", "One"),
            GameVersionType.Fabric,
            "1.21.4",
            temp.Path);
        var version = CreateVersion("v1", "sodium.jar", FakeModrinthStore.FileBytes);
        var project = CreateProject("abc", "Sodium");

        await service.InstallAsync(game, StoreContentType.Mod, project, version);
        game.EffectiveSettings.UseSharedStoreModsPath = true;

        var plan = await sharedContent.CreateMigrationPlanAsync(game, StoreContentType.Mod, true, "mods");
        Assert.Equal(1, plan.TrackedConvertibleCount);

        var summary = await sharedContent.ApplyMigrationAsync(plan, StoreSharedContentMigrationAction.ConvertTrackedFiles);
        var record = Assert.Single(minecraftBaseSettings.Peek<StoreInstallRecord[]>(temp.Path, SettingsKeys.StoreInstalledItems) ?? []);

        Assert.Equal(1, summary.ChangedCount);
        Assert.Equal(StoreLinkKind.Copy, record.LinkKind);
        Assert.True(File.Exists(record.SharedFilePath));
    }

    [Fact]
    public async Task Migration_DisableSharedFolder_MaterializesFiles()
    {
        var baseSettings = new InMemoryBaseSettingsService();
        var runtime = new FakeRuntimeService();
        var fakeStore = new FakeModrinthStore(StoreContentType.Mod, "mods");
        var service = CreateService(baseSettings, runtime, fakeStore, out var sharedContent, out InMemoryMinecraftBaseSettingsService minecraftBaseSettings);

        using var temp = new TemporaryDirectory();
        var game = CreateGame(
            Path.Combine(temp.Path, "Instances", "One"),
            GameVersionType.Fabric,
            "1.21.4",
            temp.Path,
            settings => settings.UseSharedStoreModsPath = true);
        var version = CreateVersion("v1", "sodium.jar", FakeModrinthStore.FileBytes);
        var project = CreateProject("abc", "Sodium");

        var installed = await service.InstallAsync(game, StoreContentType.Mod, project, version);
        game.EffectiveSettings.UseSharedStoreModsPath = false;

        var plan = await sharedContent.CreateMigrationPlanAsync(game, StoreContentType.Mod, false, "mods");
        Assert.Equal(1, plan.SharedInstallCount);

        await sharedContent.ApplyMigrationAsync(plan, StoreSharedContentMigrationAction.MaterializeFiles);
        var record = Assert.Single(minecraftBaseSettings.Peek<StoreInstallRecord[]>(temp.Path, SettingsKeys.StoreInstalledItems) ?? []);

        Assert.Equal(StoreLinkKind.None, record.LinkKind);
        Assert.Null(record.GodFolderHash);
        Assert.Null(record.SharedFilePath);
        Assert.True(File.Exists(installed.FilePath));
    }

    [Fact]
    public async Task Migration_ConvertAllCompatibleFiles_ImportsUntrackedFiles()
    {
        var baseSettings = new InMemoryBaseSettingsService();
        var runtime = new FakeRuntimeService();
        var fakeStore = new FakeModrinthStore(StoreContentType.Mod, "mods");
        _ = CreateService(baseSettings, runtime, fakeStore, out var sharedContent, out InMemoryMinecraftBaseSettingsService minecraftBaseSettings);

        using var temp = new TemporaryDirectory();
        var game = CreateGame(
            Path.Combine(temp.Path, "Instances", "One"),
            GameVersionType.Fabric,
            "1.21.4",
            temp.Path,
            settings => settings.UseSharedStoreModsPath = true);
        var manualFile = Path.Combine(game.Path.BasePath, "mods", "manual.jar");
        Directory.CreateDirectory(Path.GetDirectoryName(manualFile)!);
        await File.WriteAllTextAsync(manualFile, "manual");

        var plan = await sharedContent.CreateMigrationPlanAsync(game, StoreContentType.Mod, true, "mods");
        Assert.Equal(1, plan.UntrackedFileCount);

        await sharedContent.ApplyMigrationAsync(plan, StoreSharedContentMigrationAction.ConvertAllCompatibleFiles);
        var record = Assert.Single(minecraftBaseSettings.Peek<StoreInstallRecord[]>(temp.Path, SettingsKeys.StoreInstalledItems) ?? []);

        Assert.Equal("manual.jar", record.FileName);
        Assert.Equal(StoreLinkKind.Copy, record.LinkKind);
        Assert.True(File.Exists(record.SharedFilePath));
    }

    [Fact]
    public async Task RemoveAsync_RequiresForceForUntrackedItems()
    {
        var baseSettings = new InMemoryBaseSettingsService();
        var runtime = new FakeRuntimeService();
        var fakeStore = new FakeModrinthStore(StoreContentType.Mod, "mods");
        var service = CreateService(baseSettings, runtime, fakeStore);

        using var temp = new TemporaryDirectory();
        var game = CreateGame(temp.Path, GameVersionType.Fabric, "1.21.4");
        var contentRoot = Path.Combine(game.Path.BasePath, "mods");
        Directory.CreateDirectory(contentRoot);
        var manualFile = Path.Combine(contentRoot, "manual.jar");
        await File.WriteAllTextAsync(manualFile, "manual");

        var installed = await service.GetInstalledItemsAsync(game, StoreContentType.Mod);
        var untracked = Assert.Single(installed);
        Assert.False(untracked.IsTracked);

        var removedWithoutForce = await service.RemoveAsync(game, StoreContentType.Mod, untracked, forceUntracked: false);
        Assert.False(removedWithoutForce);
        Assert.True(File.Exists(manualFile));

        var removedWithForce = await service.RemoveAsync(game, StoreContentType.Mod, untracked, forceUntracked: true);
        Assert.True(removedWithForce);
        Assert.False(File.Exists(manualFile));
    }

    [Fact]
    public async Task InstallAndRemove_Throw_WhenGameIsRunning()
    {
        var baseSettings = new InMemoryBaseSettingsService();
        var runtime = new FakeRuntimeService { IsRunning = true };
        var fakeStore = new FakeModrinthStore(StoreContentType.Mod, "mods");
        var service = CreateService(baseSettings, runtime, fakeStore);

        using var temp = new TemporaryDirectory();
        var game = CreateGame(temp.Path, GameVersionType.Fabric, "1.21.4");
        var project = CreateProject("abc", "Sodium");
        var version = CreateVersion("v1", "sodium.jar");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.InstallAsync(game, StoreContentType.Mod, project, version));

        var item = new InstalledStoreItem
        {
            ContentType = StoreContentType.Mod,
            GamePath = game.Path.BasePath,
            FilePath = Path.Combine(game.Path.BasePath, "mods", "file.jar"),
            IsTracked = true
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RemoveAsync(game, StoreContentType.Mod, item, forceUntracked: false));
    }

    private static GameStoreContentService CreateService(
        InMemoryBaseSettingsService settings,
        FakeRuntimeService runtime,
        params IModrinthStore[] stores)
        => CreateService(settings, runtime, stores, out _, out _);

    private static GameStoreContentService CreateService(
        InMemoryBaseSettingsService settings,
        FakeRuntimeService runtime,
        IModrinthStore store,
        out InMemoryMinecraftBaseSettingsService minecraftBaseSettings)
        => CreateService(settings, runtime, [store], out _, out minecraftBaseSettings);

    private static GameStoreContentService CreateService(
        InMemoryBaseSettingsService settings,
        FakeRuntimeService runtime,
        IModrinthStore store,
        out IStoreSharedContentService sharedContentService)
        => CreateService(settings, runtime, [store], out sharedContentService, out _);

    private static GameStoreContentService CreateService(
        InMemoryBaseSettingsService settings,
        FakeRuntimeService runtime,
        IModrinthStore store,
        out IStoreSharedContentService sharedContentService,
        out InMemoryMinecraftBaseSettingsService minecraftBaseSettings)
        => CreateService(settings, runtime, [store], out sharedContentService, out minecraftBaseSettings);

    private static GameStoreContentService CreateService(
        InMemoryBaseSettingsService settings,
        FakeRuntimeService runtime,
        IModrinthStore[] stores,
        out IStoreSharedContentService sharedContentService,
        out InMemoryMinecraftBaseSettingsService minecraftBaseSettings)
    {
        minecraftBaseSettings = new InMemoryMinecraftBaseSettingsService();
        var sharedSettings = new StoreSharedContentSettingsService(
            settings,
            minecraftBaseSettings,
            NullLogger<StoreSharedContentSettingsService>.Instance);
        sharedSettings.Settings.UnixLinkMode = StoreLinkMode.Copy;
        sharedSettings.Settings.WindowsLinkMode = StoreLinkMode.Copy;
        var records = new StoreInstallRecordRepository(settings, minecraftBaseSettings);
        sharedContentService = new StoreSharedContentService(
            records,
            new FakeStoreFileLinkService(),
            sharedSettings);

        return new GameStoreContentService(
            records,
            runtime,
            sharedContentService,
            stores,
            NullLogger<GameStoreContentService>.Instance);
    }

    private static Game CreateGame(
        string path,
        GameVersionType type,
        string basedOn,
        string? sharedBasePath = null,
        Action<GameSettings>? configureSettings = null)
    {
        var globalSettings = new GlobalGameSettingsService(
            new InMemoryBaseSettingsService(),
            new InMemoryMinecraftBaseSettingsService(),
            NullLogger<GlobalGameSettingsService>.Instance);
        configureSettings?.Invoke(globalSettings.Settings);

        return new Game(
            new MinecraftPath(path),
            new GameVersion
            {
                DisplayName = "Test Game",
                BasedOn = basedOn,
                Type = type,
                ReleaseType = "release"
            },
            sharedMinecraftBasePath: sharedBasePath,
            globalGameSettingsService: globalSettings);
    }

    private static StoreItem CreateProject(string id, string title)
        => new()
        {
            ID = id,
            Title = title,
            Description = title,
            Categories = [],
            Versions = []
        };

    private static ItemVersion CreateVersion(string id, string fileName = "file.jar", byte[]? bytes = null)
        => new()
        {
            ID = id,
            Name = id,
            VersionNumber = id,
            VersionType = "release",
            GameVersions = ["1.21.4"],
            Loaders = ["fabric"],
            Files =
            [
                new ItemFile
                {
                    Filename = fileName,
                    Url = "https://example.invalid/file.jar",
                    Primary = true,
                    Hashes = CreateHashes(bytes ?? [])
                }
            ],
            Dependencies = []
        };

    private static Hashes CreateHashes(byte[] bytes)
        => new()
        {
            Sha1 = bytes.Length == 0 ? string.Empty : Convert.ToHexString(SHA1.HashData(bytes)).ToLowerInvariant(),
            Sha512 = bytes.Length == 0 ? string.Empty : Convert.ToHexString(SHA512.HashData(bytes)).ToLowerInvariant()
        };

    private sealed class FakeRuntimeService : IGameRuntimeService
    {
        public ObservableCollection<GameSession> Sessions { get; } = [];
        public bool IsRunning { get; set; }

        public Task<GameSession?> LaunchAsync(Game game, EAccount? account = null)
            => Task.FromResult<GameSession?>(null);

        public Task StopAsync(Game game, GameStopMode mode)
            => Task.CompletedTask;

        public GameSession? TryGetActiveSession(Game game)
            => IsRunning ? new GameSession(game, DateTimeOffset.UtcNow) : null;

        public GameSession? FindLatestSession(string gamePath)
            => null;
    }

    private sealed class FakeModrinthStore : IModrinthStore
    {
        public static readonly byte[] FileBytes = Encoding.UTF8.GetBytes("test-content");

        public FakeModrinthStore(StoreContentType contentType, string installFolderName)
        {
            ContentType = contentType;
            InstallFolderName = installFolderName;
            ProjectType = contentType.ToString().ToLowerInvariant();
            MCPath = new MinecraftPath();
        }

        public StoreContentType ContentType { get; }
        public string ProjectType { get; }
        public string InstallFolderName { get; }
        public MinecraftPath MCPath { get; set; }
        public Category[] Categories { get; private set; } = [];
        public int DownloadCount { get; private set; }

        public List<(string[]? GameVersions, string[]? Loaders)> VersionCalls { get; } = [];

        public Func<string, string[]?, string[]?, Task<List<ItemVersion>?>>? OnGetVersionsAsync { get; set; }

        public Task<SearchResult?> SearchAsync(
            string query,
            int limit = 15,
            SearchSortOptions sortOptions = SearchSortOptions.Relevance,
            string[]? categories = null)
            => Task.FromResult<SearchResult?>(null);

        public Task LoadCategoriesAsync() => Task.CompletedTask;

        public Task<StoreItem?> GetItemAsync(string id) => Task.FromResult<StoreItem?>(null);

        public Task<List<ItemVersion>?> GetVersionsAsync(string id, string[]? gameVersions = null, string[]? loaders = null)
        {
            VersionCalls.Add((gameVersions, loaders));
            if (OnGetVersionsAsync != null)
            {
                return OnGetVersionsAsync(id, gameVersions, loaders);
            }

            return Task.FromResult<List<ItemVersion>?>([]);
        }

        public async Task DownloadItemAsync(ItemFile file, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            var filePath = Path.Combine(MCPath.BasePath, InstallFolderName, file.Filename);
            await DownloadItemToPathAsync(file, filePath, progress, cancellationToken);
        }

        public async Task DownloadItemToPathAsync(
            ItemFile file,
            string filePath,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            DownloadCount++;
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            await File.WriteAllBytesAsync(filePath, FileBytes, cancellationToken);
            progress?.Report(100);
        }
    }

    private sealed class FakeStoreFileLinkService : IStoreFileLinkService
    {
        public StoreLinkCreationResult CreateLinkOrCopy(string sourcePath, string targetPath, StoreLinkMode preferredMode)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            File.Copy(sourcePath, targetPath, overwrite: true);
            return new StoreLinkCreationResult { LinkKind = StoreLinkKind.Copy };
        }

        public StoreLinkCreationResult ReplaceWithLinkOrCopy(string sourcePath, string targetPath, StoreLinkMode preferredMode)
            => CreateLinkOrCopy(sourcePath, targetPath, preferredMode);

        public bool AreOnSameRoot(string sourcePath, string targetPath) => true;

        public bool IsSymbolicLink(string path) => false;

        public string? GetSymbolicLinkTarget(string path) => null;
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"emerald-store-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
            catch
            {
            }
        }
    }
}
