using CmlLib.Core;
using Emerald.CoreX.Models;
using Emerald.CoreX.Services;
using Emerald.CoreX.Tests.Support;
using Xunit;

namespace Emerald.CoreX.Tests.Models;

[Collection(IocCollection.Name)]
public sealed class GameOverrideResolutionTests
{
    [Fact]
    public void OverrideDisabled_FollowsLiveGlobalSettings()
    {
        var globalSettingsService = new TestGlobalGameSettingsService(new GameSettings
        {
            MaximumRamMb = 2048,
            UseCustomJava = true,
            JavaPath = "/global/java"
        });
        var game = CreateGame(globalSettingsService);

        Assert.Equal(2048, game.EffectiveSettings.MaximumRamMb);
        Assert.Equal("/global/java", game.EffectiveSettings.JavaPath);

        globalSettingsService.Settings.MaximumRamMb = 4096;
        globalSettingsService.Settings.JavaPath = "/global/java-2";

        Assert.Equal(4096, game.EffectiveSettings.MaximumRamMb);
        Assert.Equal("/global/java-2", game.EffectiveSettings.JavaPath);
    }

    [Fact]
    public void OverrideEnabled_UsesClonedCustomSettings()
    {
        var globalSettingsService = new TestGlobalGameSettingsService(new GameSettings
        {
            MaximumRamMb = 2048,
            UseCustomJava = true,
            JavaPath = "/global/java"
        });
        var game = CreateGame(globalSettingsService);

        game.UsesCustomGameSettings = true;
        game.CustomGameSettings!.MaximumRamMb = 6144;
        game.CustomGameSettings.JavaPath = "/custom/java";
        globalSettingsService.Settings.MaximumRamMb = 8192;
        globalSettingsService.Settings.JavaPath = "/global/java-2";

        Assert.Equal(6144, game.EffectiveSettings.MaximumRamMb);
        Assert.Equal("/custom/java", game.EffectiveSettings.JavaPath);
        Assert.NotSame(globalSettingsService.Settings, game.CustomGameSettings);
    }

    [Fact]
    public void SeparateGames_GetDistinctCustomSettingsCopies()
    {
        var globalSettingsService = new TestGlobalGameSettingsService(new GameSettings
        {
            MaximumRamMb = 2048,
            UseCustomJava = true,
            JavaPath = "/global/java"
        });
        var firstGame = CreateGame(globalSettingsService);
        var secondGame = CreateGame(globalSettingsService, "Second");

        firstGame.UsesCustomGameSettings = true;
        secondGame.UsesCustomGameSettings = true;
        firstGame.CustomGameSettings!.MaximumRamMb = 3072;
        firstGame.CustomGameSettings.JavaPath = "/custom/java";

        Assert.NotSame(firstGame.CustomGameSettings, secondGame.CustomGameSettings);
        Assert.Equal(2048, secondGame.CustomGameSettings!.MaximumRamMb);
        Assert.Equal("/global/java", secondGame.CustomGameSettings.JavaPath);
    }

    private static CoreX.Game CreateGame(IGlobalGameSettingsService globalGameSettingsService, string displayName = "Test")
        => new(
            new MinecraftPath($"/tmp/{displayName.ToLowerInvariant()}"),
            new Emerald.CoreX.Versions.Version
            {
                DisplayName = displayName,
                BasedOn = "1.21.4",
                ReleaseType = "release"
            },
            globalGameSettingsService: globalGameSettingsService);

    private sealed class TestGlobalGameSettingsService(GameSettings settings) : IGlobalGameSettingsService
    {
        public GameSettings Settings { get; } = settings;

        public GameSettings CloneCurrent()
            => Settings.Clone();

        public void Save()
        {
        }
    }
}
