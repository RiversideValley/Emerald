using CmlLib.Core;
using Emerald.CoreX.Installers;
using Xunit;
using LauncherVersion = Emerald.CoreX.Versions.Version;
using LauncherVersionType = Emerald.CoreX.Versions.Type;

namespace Emerald.CoreX.Tests.Installers;

public sealed class ModLoaderRouterTests
{
    [Fact]
    public async Task RouteAndInitializeAsync_OfflineWithInstalledVersion_ReturnsInstalledVersionWithoutInstaller()
    {
        var installer = new RecordingInstaller(LauncherVersionType.Fabric);
        var router = new ModLoaderRouter([installer]);
        var version = new LauncherVersion
        {
            Type = LauncherVersionType.Fabric,
            BasedOn = "1.21.4",
            ModVersion = "0.16.10",
            RealVersion = "fabric-loader-0.16.10-1.21.4"
        };

        var resolved = await router.RouteAndInitializeAsync(
            new MinecraftPath("/tmp/emerald-router-test"),
            version,
            online: false,
            installedVersion: version.RealVersion);

        Assert.Equal(version.RealVersion, resolved);
        Assert.Empty(installer.Calls);
    }

    [Fact]
    public async Task RouteAndInitializeAsync_OfflineWithoutInstalledVersion_PassesOfflineFlagToInstaller()
    {
        var installer = new RecordingInstaller(LauncherVersionType.Fabric)
        {
            ResultVersion = "fabric-loader-0.16.10-1.21.4"
        };
        var router = new ModLoaderRouter([installer]);

        var resolved = await router.RouteAndInitializeAsync(
            new MinecraftPath("/tmp/emerald-router-test"),
            new LauncherVersion
            {
                Type = LauncherVersionType.Fabric,
                BasedOn = "1.21.4",
                ModVersion = "0.16.10"
            },
            online: false);

        Assert.Equal("fabric-loader-0.16.10-1.21.4", resolved);
        var call = Assert.Single(installer.Calls);
        Assert.False(call.Online);
        Assert.Equal("1.21.4", call.MinecraftVersion);
        Assert.Equal("0.16.10", call.ModVersion);
    }

    private sealed class RecordingInstaller(LauncherVersionType type) : IModLoaderInstaller
    {
        public List<InstallCall> Calls { get; } = [];
        public string ResultVersion { get; set; } = "resolved-version";

        public LauncherVersionType Type { get; } = type;

        public Task<List<LoaderInfo>> GetVersionsAsync(string mcVersion)
            => Task.FromResult(new List<LoaderInfo>());

        public Task<string> InstallAsync(
            MinecraftPath path,
            string mcversion,
            string? modversion = null,
            bool online = true)
        {
            Calls.Add(new InstallCall(mcversion, modversion, online));
            return Task.FromResult(ResultVersion);
        }
    }

    private sealed record InstallCall(string MinecraftVersion, string? ModVersion, bool Online);
}
