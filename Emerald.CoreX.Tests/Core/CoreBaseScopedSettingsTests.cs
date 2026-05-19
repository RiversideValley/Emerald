using System.Collections.ObjectModel;
using System.Reflection;
using CmlLib.Core;
using Emerald.CoreX.Helpers;
using Emerald.CoreX.Models;
using Emerald.CoreX.Notifications;
using Emerald.CoreX.Runtime;
using Emerald.CoreX.Services;
using Emerald.CoreX.Store;
using Emerald.CoreX.Tests.Support;
using Emerald.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using LauncherCore = Emerald.CoreX.Core;

namespace Emerald.CoreX.Tests.Core;

[Collection(IocCollection.Name)]
public sealed class CoreBaseScopedSettingsTests
{
    [Fact]
    public void LoadGames_MigratesMatchingCentralCollection_ToBaseScopedSavedGames()
    {
        using var baseTemp = new TemporaryDirectory();
        using var otherTemp = new TemporaryDirectory();
        var centralSettings = new InMemoryBaseSettingsService();
        var minecraftBaseSettings = new InMemoryMinecraftBaseSettingsService();
        var gamePath = Path.Combine(baseTemp.Path, LauncherCore.GamesFolderName, "One");
        Directory.CreateDirectory(gamePath);
        var otherGame = new SavedGame { Path = Path.Combine(otherTemp.Path, LauncherCore.GamesFolderName, "Other") };
        var baseGame = new SavedGame { Path = gamePath };
        centralSettings.Set(SettingsKeys.SavedGames, new[]
        {
            new SavedGameCollection { BasePath = baseTemp.Path, Games = [baseGame] },
            new SavedGameCollection { BasePath = otherTemp.Path, Games = [otherGame] }
        });
        var core = CreateCore(baseTemp.Path, centralSettings, minecraftBaseSettings);

        core.LoadGames();

        var migrated = Assert.Single(minecraftBaseSettings.Peek<SavedGame[]>(baseTemp.Path, SettingsKeys.SavedGames) ?? []);
        Assert.Equal(gamePath, migrated.Path);
        var remaining = Assert.Single(centralSettings.Peek<SavedGameCollection[]>(SettingsKeys.SavedGames) ?? []);
        Assert.Equal(otherTemp.Path, remaining.BasePath);
    }

    [Fact]
    public void LoadGames_UsesExistingBaseScopedSavedGames_WithoutPruningCentral()
    {
        using var baseTemp = new TemporaryDirectory();
        using var otherTemp = new TemporaryDirectory();
        var centralSettings = new InMemoryBaseSettingsService();
        var minecraftBaseSettings = new InMemoryMinecraftBaseSettingsService();
        var existingGamePath = Path.Combine(baseTemp.Path, LauncherCore.GamesFolderName, "Existing");
        Directory.CreateDirectory(existingGamePath);
        minecraftBaseSettings.UseBasePath(baseTemp.Path);
        minecraftBaseSettings.Set(SettingsKeys.SavedGames, new[] { new SavedGame { Path = existingGamePath } });
        centralSettings.Set(SettingsKeys.SavedGames, new[]
        {
            new SavedGameCollection
            {
                BasePath = otherTemp.Path,
                Games = [new SavedGame { Path = Path.Combine(otherTemp.Path, LauncherCore.GamesFolderName, "Other") }]
            }
        });
        var core = CreateCore(baseTemp.Path, centralSettings, minecraftBaseSettings);

        core.LoadGames();

        Assert.Equal(existingGamePath, Assert.Single(minecraftBaseSettings
            .Peek<SavedGame[]>(baseTemp.Path, SettingsKeys.SavedGames) ?? []).Path);
        Assert.True(centralSettings.Exists(SettingsKeys.SavedGames));
    }

    private static LauncherCore CreateCore(
        string basePath,
        InMemoryBaseSettingsService centralSettings,
        InMemoryMinecraftBaseSettingsService minecraftBaseSettings)
    {
        var core = new LauncherCore(
            NullLogger<LauncherCore>.Instance,
            new FakeNotificationService(),
            centralSettings,
            minecraftBaseSettings,
            new TestGameRuntimeService(),
            new TestGlobalGameSettingsService(),
            new StoreInstallRecordRepository(centralSettings, minecraftBaseSettings),
            new StoreSharedContentSettingsService(
                centralSettings,
                minecraftBaseSettings,
                NullLogger<StoreSharedContentSettingsService>.Instance));

        typeof(LauncherCore)
            .GetProperty("BasePath", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .SetValue(core, new MinecraftPath(basePath));

        return core;
    }

    private sealed class TestGlobalGameSettingsService : IGlobalGameSettingsService
    {
        public GameSettings Settings { get; } = GameSettings.FromMLaunchOption(new());

        public GameSettings CloneCurrent()
            => Settings.Clone();

        public void Save()
        {
        }

        public void LoadForBasePath(string basePath)
        {
        }
    }

    private sealed class TestGameRuntimeService : IGameRuntimeService
    {
        public ObservableCollection<GameSession> Sessions { get; } = [];

        public GameSession? FindLatestSession(string gamePath)
            => null;

        public Task<GameSession?> LaunchAsync(Game game, EAccount? account = null)
            => Task.FromResult<GameSession?>(null);

        public Task StopAsync(Game game, GameStopMode mode)
            => Task.CompletedTask;

        public GameSession? TryGetActiveSession(Game game)
            => null;
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"emerald-tests-{Guid.NewGuid():N}");

        public TemporaryDirectory()
            => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
