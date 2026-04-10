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
        var service = new GlobalGameSettingsService(baseSettingsService, NullLogger<GlobalGameSettingsService>.Instance);

        service.Settings.MaximumRamMb = 4096;

        await AsyncAssert.EventuallyAsync(() =>
            baseSettingsService.Peek<GameSettings>(SettingsKeys.BaseGameOptions)?.MaximumRamMb == 4096);
    }

    [Fact]
    public async Task Settings_JvmArgumentCollectionChanges_ArePersistedAfterDebounce()
    {
        var baseSettingsService = new InMemoryBaseSettingsService();
        var service = new GlobalGameSettingsService(baseSettingsService, NullLogger<GlobalGameSettingsService>.Instance);

        service.Settings.JVMArgs.Add("-Xmx4G");

        await AsyncAssert.EventuallyAsync(() =>
            baseSettingsService.Peek<GameSettings>(SettingsKeys.BaseGameOptions)?.JVMArgs.Contains("-Xmx4G") == true);
    }
}
