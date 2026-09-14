using Emerald.CoreX.Runtime;
using Emerald.CoreX.Services;
using Emerald.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Emerald.CoreX.Tests.Runtime;

public sealed class InstancePlaytimeServiceTests
{
    [Fact]
    public void RecordSession_PersistsAndGroupsSessionsByInstance()
    {
        using var temp = new TemporaryDirectory();
        var baseSettings = new BaseSettingsService(NullLogger<BaseSettingsService>.Instance, temp.Path);
        var service = new InstancePlaytimeService(
            baseSettings,
            NullLogger<InstancePlaytimeService>.Instance);
        var instancePath = Path.Combine(temp.Path, "Instances", "Fabric 1.21.1");
        var startedAt = new DateTimeOffset(2026, 9, 7, 9, 0, 0, TimeSpan.Zero);
        var session = new InstancePlaytimeSession
        {
            InstancePath = instancePath,
            StartedAt = startedAt,
            EndedAt = startedAt.AddMinutes(42)
        };

        service.RecordSession(session);
        service.RecordSession(session);
        service.RecordSession(new InstancePlaytimeSession
        {
            InstancePath = Path.Combine(temp.Path, "Instances", "Vanilla 1.21.1"),
            StartedAt = startedAt,
            EndedAt = startedAt.AddMinutes(10)
        });

        var stored = service.GetSessions(instancePath);
        Assert.Single(stored);
        Assert.Equal(TimeSpan.FromMinutes(42), stored[0].Playtime);
        Assert.Equal(TimeSpan.FromMinutes(42), service.GetTotalPlaytime(instancePath));
        Assert.True(baseSettings.Exists("PlaytimeHistory"));
    }

    [Fact]
    public void RecordSession_RejectsAnEndBeforeTheStart()
    {
        using var temp = new TemporaryDirectory();
        var baseSettings = new BaseSettingsService(NullLogger<BaseSettingsService>.Instance, temp.Path);
        var service = new InstancePlaytimeService(
            baseSettings,
            NullLogger<InstancePlaytimeService>.Instance);
        var startedAt = DateTimeOffset.UtcNow;

        Assert.Throws<ArgumentException>(() => service.RecordSession(new InstancePlaytimeSession
        {
            InstancePath = Path.Combine(temp.Path, "instance"),
            StartedAt = startedAt,
            EndedAt = startedAt.AddSeconds(-1)
        }));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } =
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"emerald-playtime-tests-{Guid.NewGuid():N}");

        public TemporaryDirectory()
        {
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, true);
            }
        }
    }
}
