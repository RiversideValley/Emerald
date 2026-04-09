using Emerald.CoreX.Runtime;
using Xunit;

namespace Emerald.CoreX.Tests.Runtime;

public sealed class GameLogDeduplicatorTests
{
    [Fact]
    public void Register_FileTailDuplicate_ReplacesEarlierStreamEntry()
    {
        var deduplicator = new GameLogDeduplicator();
        var streamEntry = CreateEntry(GameLogSource.StandardOutput, "<log4j:Event id=\"1\"/>");
        var fileEntry = CreateEntry(GameLogSource.FileTail, "<log4j:Event id=\"1\"/>");

        var first = deduplicator.Register(streamEntry, DateTimeOffset.UtcNow);
        var second = deduplicator.Register(fileEntry, DateTimeOffset.UtcNow.AddMilliseconds(250));

        Assert.True(first.ShouldAppend);
        Assert.True(second.ShouldAppend);
        Assert.Same(streamEntry, second.EntryToRemove);
    }

    [Fact]
    public void Register_StreamDuplicateAfterFileTail_IsRejected()
    {
        var deduplicator = new GameLogDeduplicator();
        var fileEntry = CreateEntry(GameLogSource.FileTail, "<log4j:Event id=\"2\"/>");
        var streamEntry = CreateEntry(GameLogSource.StandardOutput, "<log4j:Event id=\"2\"/>");

        deduplicator.Register(fileEntry, DateTimeOffset.UtcNow);
        var result = deduplicator.Register(streamEntry, DateTimeOffset.UtcNow.AddMilliseconds(250));

        Assert.False(result.ShouldAppend);
        Assert.Null(result.EntryToRemove);
    }

    private static GameLogEntry CreateEntry(GameLogSource source, string rawPayload) => new()
    {
        Timestamp = DateTimeOffset.UtcNow,
        Level = GameLogLevel.Info,
        Message = "message",
        Source = source,
        RawPayload = rawPayload
    };
}
