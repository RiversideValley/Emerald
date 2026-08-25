using CmlLib.Core;
using Emerald.CoreX.Installation;
using Emerald.CoreX.Services;
using Emerald.CoreX.Tests.Support;
using Xunit;

namespace Emerald.CoreX.Tests.Installation;

[Collection(IocCollection.Name)]
public sealed class GameIntegrityIssueProjectionTests
{
    [Fact]
    public void IntegrityIssues_ProjectsCriticalItemsFirst_AndBoundsVisibleItems()
    {
        var game = new Game(
            new MinecraftPath(Path.Combine(Path.GetTempPath(), "emerald-issue-projection", Guid.NewGuid().ToString("N"))),
            new Emerald.CoreX.Versions.Version
            {
                BasedOn = "test",
                DisplayName = "Test",
                ReleaseType = "release"
            },
            globalGameSettingsService: new TestGlobalGameSettingsService());
        var issues = Enumerable.Range(0, 24)
            .Select(index => new IntegrityIssue($"warning-{index}", $"Warning {index}", IntegritySeverity.Warning))
            .Append(new IntegrityIssue("critical", "Critical failure", IntegritySeverity.Critical))
            .ToArray();

        game.IntegrityIssues = issues;

        Assert.True(game.HasIntegrityIssues);
        Assert.Equal(25, game.IntegrityIssueCount);
        Assert.Equal(20, game.VisibleIntegrityIssues.Count);
        Assert.Equal("Critical failure", game.VisibleIntegrityIssues[0].Message);
        Assert.True(game.HasRemainingIntegrityIssues);
        Assert.Equal(5, game.RemainingIntegrityIssueCount);
    }

    private sealed class TestGlobalGameSettingsService : IGlobalGameSettingsService
    {
        public Emerald.CoreX.Models.GameSettings Settings { get; } = new();
        public Emerald.CoreX.Models.GameSettings CloneCurrent() => Settings.Clone();
        public void LoadForBasePath(string basePath) { }
        public void Save() { }
    }
}
