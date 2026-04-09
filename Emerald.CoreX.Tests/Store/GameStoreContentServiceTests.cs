using System.Collections.ObjectModel;
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
    public async Task InstallAndGetInstalledItems_PersistsTrackedInstall_AndPrunesStaleRecords()
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
        Assert.DoesNotContain(secondRead, item => item.IsTracked);
        Assert.Empty(baseSettings.Peek<StoreInstallRecord[]>(SettingsKeys.StoreInstalledItems) ?? []);
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
    {
        return new GameStoreContentService(
            settings,
            runtime,
            stores,
            NullLogger<GameStoreContentService>.Instance);
    }

    private static Game CreateGame(string path, GameVersionType type, string basedOn)
    {
        var globalSettings = new GlobalGameSettingsService(
            new InMemoryBaseSettingsService(),
            NullLogger<GlobalGameSettingsService>.Instance);

        return new Game(
            new MinecraftPath(path),
            new GameVersion
            {
                DisplayName = "Test Game",
                BasedOn = basedOn,
                Type = type,
                ReleaseType = "release"
            },
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

    private static ItemVersion CreateVersion(string id, string fileName = "file.jar")
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
                    Hashes = new Hashes
                    {
                        Sha1 = string.Empty,
                        Sha512 = string.Empty
                    }
                }
            ],
            Dependencies = []
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
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            await File.WriteAllTextAsync(filePath, "test-content", cancellationToken);
            progress?.Report(100);
        }
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
