using CmlLib.Core;
using Emerald.CoreX;
using Emerald.CoreX.Models;
using Emerald.CoreX.Services;
using Emerald.CoreX.Tests.Support;
using Xunit;

namespace Emerald.CoreX.Tests.Models;

[Collection(IocCollection.Name)]
public sealed class SplitMinecraftPathTests
{
    [Fact]
    public void SplitMinecraftPath_RedirectsOnlyEnabledFolders()
    {
        var globalBase = Path.Combine(Path.GetTempPath(), "emerald-global");
        var instanceBase = Path.Combine(Path.GetTempPath(), "emerald-instance");

        var path = new SplitMinecraftPath(
            globalBase,
            instanceBase,
            shareAssets: true,
            shareLibraries: false,
            shareRuntime: true,
            shareVersions: false);

        Assert.Equal(instanceBase, path.BasePath);
        Assert.Equal(Path.Combine(globalBase, "assets"), path.Assets);
        Assert.Equal(Path.Combine(globalBase, "resources"), path.Resource);
        Assert.Equal(Path.Combine(instanceBase, "libraries"), path.Library);
        Assert.Equal(Path.Combine(globalBase, "runtime"), path.Runtime);
        Assert.Equal(Path.Combine(instanceBase, "versions"), path.Versions);
    }

    [Fact]
    public void GamePath_FollowsGlobalSharedFolderSettings()
    {
        var globalBase = Path.Combine(Path.GetTempPath(), "emerald-global");
        var instanceBase = Path.Combine(globalBase, Emerald.CoreX.Core.GamesFolderName, "Shared");
        var globalSettingsService = new TestGlobalGameSettingsService(new GameSettings
        {
            UseSharedAssetsPath = true,
            UseSharedLibrariesPath = true,
            UseSharedRuntimePath = true,
            UseSharedVersionsPath = true
        });
        var game = CreateGame(globalSettingsService, instanceBase, globalBase);

        Assert.Equal(Path.Combine(globalBase, "assets"), game.Path.Assets);
        Assert.Equal(Path.Combine(globalBase, "libraries"), game.Path.Library);
        Assert.Equal(Path.Combine(globalBase, "runtime"), game.Path.Runtime);
        Assert.Equal(Path.Combine(globalBase, "versions"), game.Path.Versions);

        globalSettingsService.Settings.UseSharedRuntimePath = false;

        Assert.Equal(Path.Combine(instanceBase, "runtime"), game.Path.Runtime);
        Assert.Equal(Path.Combine(globalBase, "assets"), game.Path.Assets);
    }

    [Fact]
    public void GamePath_CustomSettingsCanDisableGlobalSharedFolders()
    {
        var globalBase = Path.Combine(Path.GetTempPath(), "emerald-global");
        var instanceBase = Path.Combine(globalBase, Emerald.CoreX.Core.GamesFolderName, "Custom");
        var globalSettingsService = new TestGlobalGameSettingsService(new GameSettings
        {
            UseSharedAssetsPath = true,
            UseSharedLibrariesPath = true
        });
        var game = CreateGame(globalSettingsService, instanceBase, globalBase);

        game.UsesCustomGameSettings = true;
        game.CustomGameSettings!.UseSharedAssetsPath = false;
        game.CustomGameSettings.UseSharedLibrariesPath = true;

        Assert.Equal(Path.Combine(instanceBase, "assets"), game.Path.Assets);
        Assert.Equal(Path.Combine(globalBase, "libraries"), game.Path.Library);
    }

    private static Game CreateGame(
        IGlobalGameSettingsService globalGameSettingsService,
        string instanceBase,
        string sharedBase)
        => new(
            new MinecraftPath(instanceBase),
            new Versions.Version
            {
                DisplayName = "Shared",
                BasedOn = "1.21.4",
                ReleaseType = "release"
            },
            sharedMinecraftBasePath: sharedBase,
            globalGameSettingsService: globalGameSettingsService);

    private sealed class TestGlobalGameSettingsService(GameSettings settings) : IGlobalGameSettingsService
    {
        public GameSettings Settings { get; } = settings;

        public GameSettings CloneCurrent()
            => Settings.Clone();

        public void LoadForBasePath(string basePath)
        {
        }

        public void Save()
        {
        }
    }
}
