using Emerald.CoreX.Runtime;
using Xunit;

namespace Emerald.CoreX.Tests.Runtime;

public sealed class GameLogDeduplicatorTests
{
    [Fact]
    public void Register_MissingLogger_MergesIntoRicherCopy()
    {
        var deduplicator = new GameLogDeduplicator();
        var weakerEntry = CreateEntry(GameLogSource.StandardError, threadName: "Datafixer Bootstrap");
        var richerEntry = CreateEntry(GameLogSource.StandardOutput, threadName: "Datafixer Bootstrap", loggerName: "com.mojang.datafixers.DataFixerBuilder");

        deduplicator.Register(weakerEntry, DateTimeOffset.UtcNow);
        var result = deduplicator.Register(richerEntry, DateTimeOffset.UtcNow.AddMilliseconds(250));

        Assert.True(result.ShouldAppend);
        Assert.Same(weakerEntry, result.EntryToRemove);
    }

    [Fact]
    public void Register_MissingThread_MergesIntoRicherCopy()
    {
        var deduplicator = new GameLogDeduplicator();
        var weakerEntry = CreateEntry(GameLogSource.StandardError, loggerName: "net.minecraft.client.Minecraft");
        var richerEntry = CreateEntry(GameLogSource.StandardOutput, threadName: "Render thread", loggerName: "net.minecraft.client.Minecraft");

        deduplicator.Register(weakerEntry, DateTimeOffset.UtcNow);
        var result = deduplicator.Register(richerEntry, DateTimeOffset.UtcNow.AddMilliseconds(250));

        Assert.True(result.ShouldAppend);
        Assert.Same(weakerEntry, result.EntryToRemove);
    }

    [Fact]
    public void Register_ConflictingThreadNames_StaysDistinct()
    {
        var deduplicator = new GameLogDeduplicator();
        var first = CreateEntry(GameLogSource.StandardOutput, threadName: "Render thread");
        var second = CreateEntry(GameLogSource.StandardError, threadName: "IO-Worker-1");

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
        var first = CreateEntry(GameLogSource.StandardOutput, loggerName: "logger.one");
        var second = CreateEntry(GameLogSource.StandardError, loggerName: "logger.two");

        deduplicator.Register(first, DateTimeOffset.UtcNow);
        var result = deduplicator.Register(second, DateTimeOffset.UtcNow.AddMilliseconds(250));

        Assert.True(result.ShouldAppend);
        Assert.Null(result.EntryToRemove);
    }

    [Fact]
    public void Register_StandardOutputStillWinsWhenCopiesAreCompatible()
    {
        var deduplicator = new GameLogDeduplicator();
        var standardErrorEntry = CreateEntry(GameLogSource.StandardError, threadName: "Render thread", loggerName: "net.minecraft.client.Minecraft");
        var standardOutputEntry = CreateEntry(GameLogSource.StandardOutput, threadName: "Render thread", loggerName: "net.minecraft.client.Minecraft");

        deduplicator.Register(standardErrorEntry, DateTimeOffset.UtcNow);
        var result = deduplicator.Register(standardOutputEntry, DateTimeOffset.UtcNow.AddMilliseconds(250));

        Assert.True(result.ShouldAppend);
        Assert.Same(standardErrorEntry, result.EntryToRemove);
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
