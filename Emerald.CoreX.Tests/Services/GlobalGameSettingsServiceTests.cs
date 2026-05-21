using Emerald.CoreX.Helpers;
using Emerald.CoreX.Models;
using Emerald.CoreX.Services;
using Emerald.CoreX.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Emerald.CoreX.Tests.Services;

[Collection(IocCollection.Name)]
public sealed class GlobalGameSettingsServiceTests
{
    [Fact]
    public async Task Settings_PropertyChanges_ArePersistedAfterDebounce()
    {
        var baseSettingsService = new InMemoryBaseSettingsService();
        var minecraftBaseSettings = new InMemoryMinecraftBaseSettingsService();
        minecraftBaseSettings.UseBasePath("/tmp/emerald-base");
        var service = new GlobalGameSettingsService(
            baseSettingsService,
            minecraftBaseSettings,
            NullLogger<GlobalGameSettingsService>.Instance);
        service.LoadForBasePath("/tmp/emerald-base");

        service.Settings.MaximumRamMb = 4096;

        await AsyncAssert.EventuallyAsync(() =>
            minecraftBaseSettings.Peek<GameSettings>("/tmp/emerald-base", SettingsKeys.BaseGameOptions)?.MaximumRamMb == 4096);
    }

    [Fact]
    public async Task Settings_JvmArgumentCollectionChanges_ArePersistedAfterDebounce()
    {
        var baseSettingsService = new InMemoryBaseSettingsService();
        var minecraftBaseSettings = new InMemoryMinecraftBaseSettingsService();
        minecraftBaseSettings.UseBasePath("/tmp/emerald-base");
        var service = new GlobalGameSettingsService(
            baseSettingsService,
            minecraftBaseSettings,
            NullLogger<GlobalGameSettingsService>.Instance);
        service.LoadForBasePath("/tmp/emerald-base");

        service.Settings.JVMArgs.Add("-Xmx4G");

        await AsyncAssert.EventuallyAsync(() =>
            minecraftBaseSettings.Peek<GameSettings>("/tmp/emerald-base", SettingsKeys.BaseGameOptions)?.JVMArgs.Contains("-Xmx4G") == true);
    }

    [Fact]
    public void LoadForBasePath_MigratesCentralSettings_AndDeletesCentralKey()
    {
        var baseSettingsService = new InMemoryBaseSettingsService();
        var minecraftBaseSettings = new InMemoryMinecraftBaseSettingsService();
        baseSettingsService.Set(SettingsKeys.BaseGameOptions, new GameSettings { MaximumRamMb = 5120 });
        var service = new GlobalGameSettingsService(
            baseSettingsService,
            minecraftBaseSettings,
            NullLogger<GlobalGameSettingsService>.Instance);

        service.LoadForBasePath("/tmp/emerald-base");

        Assert.Equal(5120, service.Settings.MaximumRamMb);
        Assert.False(baseSettingsService.Exists(SettingsKeys.BaseGameOptions));
        Assert.Equal(5120, minecraftBaseSettings
            .Peek<GameSettings>("/tmp/emerald-base", SettingsKeys.BaseGameOptions)
            ?.MaximumRamMb);
    }
}
