using Emerald.CoreX.Helpers;
using Emerald.CoreX.Services;
using Emerald.CoreX.Store;
using Emerald.CoreX.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Emerald.CoreX.Tests.Store;

public sealed class StoreBaseScopedSettingsTests
{
    [Fact]
    public void Repository_LoadForBasePath_MigratesMatchingRecords_AndPrunesCentralRecords()
    {
        using var baseTemp = new TemporaryDirectory();
        using var otherTemp = new TemporaryDirectory();
        var centralSettings = new InMemoryBaseSettingsService();
        var minecraftBaseSettings = new InMemoryMinecraftBaseSettingsService();
        var matchingRecord = CreateRecord(Path.Combine(baseTemp.Path, "Instances", "One"));
        var otherRecord = CreateRecord(Path.Combine(otherTemp.Path, "Instances", "Other"));
        centralSettings.Set(SettingsKeys.StoreInstalledItems, new[] { matchingRecord, otherRecord });
        var repository = new StoreInstallRecordRepository(centralSettings, minecraftBaseSettings);

        repository.LoadForBasePath(baseTemp.Path);

        Assert.Equal(matchingRecord.Id, Assert.Single(minecraftBaseSettings
            .Peek<StoreInstallRecord[]>(baseTemp.Path, SettingsKeys.StoreInstalledItems) ?? []).Id);
        Assert.Equal(otherRecord.Id, Assert.Single(centralSettings
            .Peek<StoreInstallRecord[]>(SettingsKeys.StoreInstalledItems) ?? []).Id);
    }

    [Fact]
    public void SharedContentSettings_LoadForBasePath_MigratesCentralSettings_AndDeletesCentralKey()
    {
        using var baseTemp = new TemporaryDirectory();
        var centralSettings = new InMemoryBaseSettingsService();
        var minecraftBaseSettings = new InMemoryMinecraftBaseSettingsService();
        centralSettings.Set(SettingsKeys.StoreSharedContentSettings, new StoreSharedContentSettings
        {
            UnixLinkMode = StoreLinkMode.Copy,
            WindowsLinkMode = StoreLinkMode.SymbolicLink
        });
        var service = new StoreSharedContentSettingsService(
            centralSettings,
            minecraftBaseSettings,
            NullLogger<StoreSharedContentSettingsService>.Instance);

        service.LoadForBasePath(baseTemp.Path);

        Assert.Equal(StoreLinkMode.Copy, service.Settings.UnixLinkMode);
        Assert.Equal(StoreLinkMode.SymbolicLink, service.Settings.WindowsLinkMode);
        Assert.False(centralSettings.Exists(SettingsKeys.StoreSharedContentSettings));
        Assert.Equal(StoreLinkMode.Copy, minecraftBaseSettings
            .Peek<StoreSharedContentSettings>(baseTemp.Path, SettingsKeys.StoreSharedContentSettings)
            ?.UnixLinkMode);
    }

    private static StoreInstallRecord CreateRecord(string gamePath)
        => new()
        {
            ContentType = StoreContentType.Mod,
            GamePath = gamePath,
            ProjectId = Path.GetFileName(gamePath),
            ProjectTitle = Path.GetFileName(gamePath),
            VersionId = "version",
            VersionName = "Version",
            FileName = "mod.jar",
            FilePath = Path.Combine(gamePath, "mods", "mod.jar")
        };

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
