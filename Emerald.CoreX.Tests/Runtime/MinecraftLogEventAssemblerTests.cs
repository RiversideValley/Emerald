using Emerald.CoreX.Runtime;
using Xunit;

namespace Emerald.CoreX.Tests.Runtime;

public sealed class MinecraftLogEventAssemblerTests
{
    [Fact]
    public void AppendLine_FinalizesXmlEventOnlyWhenClosingTagArrives()
    {
        var assembler = new MinecraftLogEventAssembler(GameLogSource.StandardOutput);
        var startedAt = DateTimeOffset.UtcNow;

        var beforeClose = new List<GameLogEntry>();
        beforeClose.AddRange(assembler.AppendLine("""<log4j:Event logger="com.example.TextureAtlas" timestamp="1775683856298" level="INFO" thread="Render thread">""", startedAt));
        beforeClose.AddRange(assembler.AppendLine("""  <log4j:Message><![CDATA[Created atlas]]></log4j:Message>""", startedAt));

        var finalized = assembler.AppendLine("""</log4j:Event>""", startedAt);
        var entry = Assert.Single(finalized);

        Assert.Empty(beforeClose);
        Assert.Equal(GameLogLevel.Info, entry.Level);
        Assert.Equal("Created atlas", entry.Message);
        Assert.Equal("Render thread", entry.ThreadName);
        Assert.Equal("com.example.TextureAtlas", entry.LoggerName);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1775683856298), entry.Timestamp);
    }

    [Fact]
    public void AppendLine_XmlThrowable_BecomesDetailsText()
    {
        var assembler = new MinecraftLogEventAssembler(GameLogSource.StandardOutput);
        var lines = new[]
        {
            """<log4j:Event logger="com.mojang.realmsclient.RealmsAvailability" timestamp="1775683860934" level="ERROR" thread="IO-Worker-1">""",
            """  <log4j:Message><![CDATA[Couldn't connect to realms]]></log4j:Message>""",
            """  <log4j:Throwable><![CDATA[com.mojang.realmsclient.exception.RealmsServiceException: Realms authentication error""",
            """        at knot//com.mojang.realmsclient.client.RealmsClient.execute(RealmsClient.java:526)""",
            """        at java.base/java.lang.Thread.run(Thread.java:1474)""",
            """]]></log4j:Throwable>""",
            """</log4j:Event>"""
        };

        var entry = FeedAll(assembler, lines);

        Assert.Equal(GameLogLevel.Error, entry.Level);
        Assert.Equal("Couldn't connect to realms", entry.Message);
        Assert.Contains("RealmsServiceException", entry.DetailsText);
        Assert.Contains("RealmsClient.execute", entry.DetailsText);
    }

    [Fact]
    public void AppendLine_ConsecutiveXmlEvents_ProduceSeparateEntries()
    {
        var assembler = new MinecraftLogEventAssembler(GameLogSource.StandardOutput);
        var produced = new List<GameLogEntry>();
        var timestamp = DateTimeOffset.UtcNow;

        produced.AddRange(assembler.AppendLine("""<log4j:Event logger="one" timestamp="1775683856298" level="INFO" thread="Render thread">""", timestamp));
        produced.AddRange(assembler.AppendLine("""  <log4j:Message><![CDATA[First]]></log4j:Message>""", timestamp));
        produced.AddRange(assembler.AppendLine("""</log4j:Event>""", timestamp));
        produced.AddRange(assembler.AppendLine("""<log4j:Event logger="two" timestamp="1775683856358" level="WARN" thread="Render thread">""", timestamp));
        produced.AddRange(assembler.AppendLine("""  <log4j:Message><![CDATA[Second]]></log4j:Message>""", timestamp));
        produced.AddRange(assembler.AppendLine("""</log4j:Event>""", timestamp));

        Assert.Equal(2, produced.Count);
        Assert.Equal("First", produced[0].Message);
        Assert.Equal("Second", produced[1].Message);
        Assert.Equal(GameLogLevel.Warn, produced[1].Level);
    }

    [Fact]
    public void FlushPending_TextFallback_GroupsMultilineThrowable()
    {
        var assembler = new MinecraftLogEventAssembler(GameLogSource.StandardOutput);
        var timestamp = DateTimeOffset.UtcNow;

        Assert.Empty(assembler.AppendLine("[01:39:16] [Render thread/ERROR]: Error starting SoundSystem. Turning off sounds & music", timestamp));
        Assert.Empty(assembler.AppendLine("java.lang.IllegalStateException: Failed to get OpenAL attributes", timestamp));
        Assert.Empty(assembler.AppendLine("\tat foo.Bar(Baz.java:12)", timestamp));

        var entry = Assert.Single(assembler.FlushPending(timestamp.AddMilliseconds(100), includeXmlFallback: true));

        Assert.Equal(GameLogLevel.Error, entry.Level);
        Assert.Equal("Error starting SoundSystem. Turning off sounds & music", entry.Message);
        Assert.Contains("java.lang.IllegalStateException", entry.DetailsText);
        Assert.Contains("foo.Bar", entry.DetailsText);
    }

    [Fact]
    public void FlushPending_UnterminatedXml_FallsBackToRawPayload()
    {
        var assembler = new MinecraftLogEventAssembler(GameLogSource.StandardOutput);
        var timestamp = DateTimeOffset.UtcNow;

        Assert.Empty(assembler.AppendLine("""<log4j:Event logger="broken" timestamp="1775683856298" level="INFO" thread="Render thread">""", timestamp));
        Assert.Empty(assembler.AppendLine("""  <log4j:Message><![CDATA[Broken""", timestamp));

        var entry = Assert.Single(assembler.FlushPending(timestamp.AddSeconds(1), includeXmlFallback: true));

        Assert.Equal(GameLogLevel.Unknown, entry.Level);
        Assert.StartsWith("<log4j:Event", entry.Message, StringComparison.Ordinal);
        Assert.Contains("Broken", entry.RawPayload);
    }

    private static GameLogEntry FeedAll(MinecraftLogEventAssembler assembler, IEnumerable<string> lines)
    {
        var finalized = new List<GameLogEntry>();
        var timestamp = DateTimeOffset.UtcNow;

        foreach (var line in lines)
        {
            finalized.AddRange(assembler.AppendLine(line, timestamp));
        }

        return Assert.Single(finalized);
    }
}
