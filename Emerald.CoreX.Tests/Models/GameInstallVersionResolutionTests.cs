using Emerald.CoreX;
using Xunit;

namespace Emerald.CoreX.Tests.Models;

public sealed class GameInstallVersionResolutionTests
{
    [Fact]
    public void ResolveOfflineInstalledVersionName_PrefersSavedRealVersion()
    {
        var version = new Versions.Version
        {
            Type = Versions.Type.Fabric,
            BasedOn = "1.21.4",
            ModVersion = "0.16.10",
            RealVersion = "fabric-loader-0.16.10-1.21.4",
            DisplayName = "Fabric"
        };

        var resolved = Game.ResolveOfflineInstalledVersionName(
            ["fabric-loader-0.16.10-1.21.4", "fabric-loader-0.16.11-1.21.4"],
            version,
            "fabric-loader-0.16.11-1.21.4");

        Assert.Equal("fabric-loader-0.16.10-1.21.4", resolved);
    }

    [Fact]
    public void ResolveOfflineInstalledVersionName_UsesRoutedVersion_WhenSavedRealVersionIsMissing()
    {
        var version = new Versions.Version
        {
            Type = Versions.Type.Quilt,
            BasedOn = "1.20.1",
            ModVersion = "0.22.0",
            DisplayName = "Quilt"
        };

        var resolved = Game.ResolveOfflineInstalledVersionName(
            ["quilt-loader-0.22.0-1.20.1"],
            version,
            "quilt-loader-0.22.0-1.20.1");

        Assert.Equal("quilt-loader-0.22.0-1.20.1", resolved);
    }

    [Fact]
    public void ResolveOfflineInstalledVersionName_InfersSingleLoaderVersion_WhenExactCandidateIsMissing()
    {
        var version = new Versions.Version
        {
            Type = Versions.Type.Forge,
            BasedOn = "1.20.1",
            ModVersion = "47.4.0",
            DisplayName = "Forge"
        };

        var resolved = Game.ResolveOfflineInstalledVersionName(
            ["1.20.1-forge-47.4.0", "1.20.1"],
            version,
            null);

        Assert.Equal("1.20.1-forge-47.4.0", resolved);
    }

    [Fact]
    public void ResolveOfflineInstalledVersionName_IgnoresEmptyCandidates_AndReturnsNullWhenNoInstalledMatchExists()
    {
        var version = new Versions.Version
        {
            Type = Versions.Type.Fabric,
            BasedOn = "1.21.4",
            ModVersion = "0.16.10",
            RealVersion = "",
            DisplayName = "Fabric"
        };

        var resolved = Game.ResolveOfflineInstalledVersionName(
            ["1.21.4"],
            version,
            "");

        Assert.Null(resolved);
    }
}
