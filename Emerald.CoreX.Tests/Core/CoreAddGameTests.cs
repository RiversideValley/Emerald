using System.Collections.ObjectModel;
using System.Reflection;
using CmlLib.Core;
using CommunityToolkit.Mvvm.DependencyInjection;
using Emerald.CoreX.Notifications;
using Emerald.CoreX.Runtime;
using Emerald.CoreX.Services;
using Emerald.CoreX.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Emerald.CoreX.Tests.Core;

[Collection(IocCollection.Name)]
public sealed class CoreAddGameTests
{
    [Fact]
    public void AddGame_UsesDisplayNameAsDefaultFolderName()
    {
        var core = CreateCore("/tmp/emerald-default-folder");
        var version = CreateVersion("Survival 1.21.4");

        core.AddGame(version);

        var game = Assert.Single(core.Games);
        Assert.Equal("/tmp/emerald-default-folder/EmeraldGames/Survival 1.21.4", game.Path.BasePath);
    }

    [Fact]
    public void AddGame_UsesExplicitFolderNameWhenProvided()
    {
        var core = CreateCore("/tmp/emerald-custom-folder");
        var version = CreateVersion("My Fabric Setup");

        core.AddGame(version, "shared-modpack");

        var game = Assert.Single(core.Games);
        Assert.Equal("/tmp/emerald-custom-folder/EmeraldGames/shared-modpack", game.Path.BasePath);
    }

    private static Emerald.CoreX.Core CreateCore(string basePath)
    {
        var notificationService = Ioc.Default.GetService<INotificationService>()
            ?? new NotificationService(NullLogger<NotificationService>.Instance);
        var core = new Emerald.CoreX.Core(
            NullLogger<Emerald.CoreX.Core>.Instance,
            notificationService,
            new InMemoryBaseSettingsService(),
            new TestGameRuntimeService(),
            new TestGlobalGameSettingsService());

        typeof(Emerald.CoreX.Core)
            .GetProperty("BasePath", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .SetValue(core, new MinecraftPath(basePath));

        return core;
    }

    private static Emerald.CoreX.Versions.Version CreateVersion(string displayName)
        => new()
        {
            DisplayName = displayName,
            BasedOn = "1.21.4",
            ReleaseType = "release"
        };

    private sealed class TestGlobalGameSettingsService : IGlobalGameSettingsService
    {
        public Emerald.CoreX.Models.GameSettings Settings { get; } = new();

        public Emerald.CoreX.Models.GameSettings CloneCurrent()
            => Settings.Clone();

        public void Save()
        {
        }
    }

    private sealed class TestGameRuntimeService : IGameRuntimeService
    {
        public ObservableCollection<GameSession> Sessions { get; } = new();

        public GameSession? FindLatestSession(string gamePath)
            => null;

        public Task<GameSession?> LaunchAsync(Emerald.CoreX.Game game, Emerald.CoreX.Models.EAccount? account = null)
            => Task.FromResult<GameSession?>(null);

        public Task StopAsync(Emerald.CoreX.Game game, GameStopMode mode)
            => Task.CompletedTask;

        public GameSession? TryGetActiveSession(Emerald.CoreX.Game game)
            => null;
    }
}
