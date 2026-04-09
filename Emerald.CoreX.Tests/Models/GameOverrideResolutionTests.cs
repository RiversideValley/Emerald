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
        var globalSettingsService = new TestGlobalGameSettingsService(new GameSettings { MaximumRamMb = 2048 });
        var game = CreateGame(globalSettingsService);

        Assert.Equal(2048, game.EffectiveSettings.MaximumRamMb);

        globalSettingsService.Settings.MaximumRamMb = 4096;

        Assert.Equal(4096, game.EffectiveSettings.MaximumRamMb);
    }

    [Fact]
    public void OverrideEnabled_UsesClonedCustomSettings()
    {
        var globalSettingsService = new TestGlobalGameSettingsService(new GameSettings { MaximumRamMb = 2048 });
        var game = CreateGame(globalSettingsService);

        game.UsesCustomGameSettings = true;
        game.CustomGameSettings!.MaximumRamMb = 6144;
        globalSettingsService.Settings.MaximumRamMb = 8192;

        Assert.Equal(6144, game.EffectiveSettings.MaximumRamMb);
        Assert.NotSame(globalSettingsService.Settings, game.CustomGameSettings);
    }

    [Fact]
    public void SeparateGames_GetDistinctCustomSettingsCopies()
    {
        var globalSettingsService = new TestGlobalGameSettingsService(new GameSettings { MaximumRamMb = 2048 });
        var firstGame = CreateGame(globalSettingsService);
        var secondGame = CreateGame(globalSettingsService, "Second");

        firstGame.UsesCustomGameSettings = true;
        secondGame.UsesCustomGameSettings = true;
        firstGame.CustomGameSettings!.MaximumRamMb = 3072;

        Assert.NotSame(firstGame.CustomGameSettings, secondGame.CustomGameSettings);
        Assert.Equal(2048, secondGame.CustomGameSettings!.MaximumRamMb);
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
