using Emerald.CoreX.Runtime;
using Xunit;

namespace Emerald.CoreX.Tests.Runtime;

public sealed class GameLogDeduplicatorTests
{
    [Fact]
    public void Register_MissingLogger_MergesIntoRicherCopy()
    {
        var deduplicator = new GameLogDeduplicator();
        var streamEntry = CreateEntry(GameLogSource.StandardOutput, threadName: "Datafixer Bootstrap");
        var fileEntry = CreateEntry(GameLogSource.FileTail, threadName: "Datafixer Bootstrap", loggerName: "com.mojang.datafixers.DataFixerBuilder");

        deduplicator.Register(streamEntry, DateTimeOffset.UtcNow);
        var result = deduplicator.Register(fileEntry, DateTimeOffset.UtcNow.AddMilliseconds(250));

        Assert.True(result.ShouldAppend);
        Assert.Same(streamEntry, result.EntryToRemove);
    }

    [Fact]
    public void Register_MissingThread_MergesIntoRicherCopy()
    {
        var deduplicator = new GameLogDeduplicator();
        var streamEntry = CreateEntry(GameLogSource.StandardOutput, loggerName: "net.minecraft.client.Minecraft");
        var fileEntry = CreateEntry(GameLogSource.FileTail, threadName: "Render thread", loggerName: "net.minecraft.client.Minecraft");

        deduplicator.Register(streamEntry, DateTimeOffset.UtcNow);
        var result = deduplicator.Register(fileEntry, DateTimeOffset.UtcNow.AddMilliseconds(250));

        Assert.True(result.ShouldAppend);
        Assert.Same(streamEntry, result.EntryToRemove);
    }

    [Fact]
    public void Register_ConflictingThreadNames_StaysDistinct()
    {
        var deduplicator = new GameLogDeduplicator();
        var first = CreateEntry(GameLogSource.FileTail, threadName: "Render thread");
        var second = CreateEntry(GameLogSource.StandardOutput, threadName: "IO-Worker-1");

        var initial = deduplicator.Register(first, DateTimeOffset.UtcNow);
        var result = deduplicator.Register(second, DateTimeOffset.UtcNow.AddMilliseconds(250));

        Assert.True(initial.ShouldAppend);
        Assert.True(result.ShouldAppend);
        Assert.Null(result.EntryToRemove);
    }

    [Fact]
    public void Register_ConflictingLoggerNames_StaysDistinct()
    {
        var deduplicator = new GameLogDeduplicator();
        var first = CreateEntry(GameLogSource.FileTail, loggerName: "logger.one");
        var second = CreateEntry(GameLogSource.StandardOutput, loggerName: "logger.two");

        deduplicator.Register(first, DateTimeOffset.UtcNow);
        var result = deduplicator.Register(second, DateTimeOffset.UtcNow.AddMilliseconds(250));

        Assert.True(result.ShouldAppend);
        Assert.Null(result.EntryToRemove);
    }

    [Fact]
    public void Register_FileTailStillWinsWhenCopiesAreCompatible()
    {
        var deduplicator = new GameLogDeduplicator();
        var streamEntry = CreateEntry(GameLogSource.StandardOutput, threadName: "Render thread", loggerName: "net.minecraft.client.Minecraft");
        var fileEntry = CreateEntry(GameLogSource.FileTail, threadName: "Render thread", loggerName: "net.minecraft.client.Minecraft");

        deduplicator.Register(streamEntry, DateTimeOffset.UtcNow);
        var result = deduplicator.Register(fileEntry, DateTimeOffset.UtcNow.AddMilliseconds(250));

        Assert.True(result.ShouldAppend);
        Assert.Same(streamEntry, result.EntryToRemove);
    }

    private static GameLogEntry CreateEntry(
        GameLogSource source,
        string? threadName = null,
        string? loggerName = null,
        string? detailsText = null) => new()
        {
            Timestamp = DateTimeOffset.UtcNow,
            OriginalTimeText = "11:14:57",
            Level = GameLogLevel.Info,
            Message = "Environment: Environment[sessionHost=https://sessionserver.mojang.com, servicesHost=https://api.minecraftservices.com, profilesHost=https://api.mojang.com, name=PROD]",
            DetailsText = detailsText,
            ThreadName = threadName,
            LoggerName = loggerName,
            Source = source,
            RawPayload = $"{threadName}|{loggerName}|{detailsText}|{source}"
        };
}
