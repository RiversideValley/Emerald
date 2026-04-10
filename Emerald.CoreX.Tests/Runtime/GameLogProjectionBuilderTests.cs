using Emerald.CoreX.Runtime;
using Xunit;

namespace Emerald.CoreX.Tests.Runtime;

public sealed class GameLogProjectionBuilderTests
{
    [Fact]
    public void Build_SearchMatchesMessageDetailsThreadLoggerAndLevel()
    {
        var entries = new[]
        {
            CreateEntry("Backend initialized", level: GameLogLevel.Info),
            CreateEntry("Error headline", detailsText: "SignedJWT parsing failed", level: GameLogLevel.Error),
            CreateEntry("Thread sample", threadName: "IO-Worker-1", level: GameLogLevel.Warn),
            CreateEntry("Logger sample", loggerName: "com.mojang.auth", level: GameLogLevel.Debug)
        };

        Assert.Single(Build(entries, searchQuery: "backend").VisibleEntries);
        Assert.Single(Build(entries, searchQuery: "signedjwt").VisibleEntries);
        Assert.Single(Build(entries, searchQuery: "io-worker-1").VisibleEntries);
        Assert.Single(Build(entries, searchQuery: "com.mojang.auth").VisibleEntries);
        Assert.Single(Build(entries, searchQuery: "warn").VisibleEntries);
    }

    [Fact]
    public void Build_LevelFilterReturnsOnlyMatchingSeverity()
    {
        var entries = new[]
        {
            CreateEntry("Info one", level: GameLogLevel.Info),
            CreateEntry("Warn one", level: GameLogLevel.Warn),
            CreateEntry("Error one", level: GameLogLevel.Error)
        };

        var projection = Build(entries, selectedLevelFilter: "Warn");

        Assert.Equal(1, projection.FilteredEntryCount);
        Assert.Single(projection.VisibleEntries);
        Assert.Equal("WARN", projection.VisibleEntries[0].LevelText);
    }

    [Fact]
    public void Build_PaginationComputesPagesAndVisibleEntries()
    {
        var projection = Build(CreateSequencedEntries(205), pageSize: 100, autoScroll: false);

        Assert.Equal(3, projection.TotalPages);
        Assert.Equal(1, projection.CurrentPageNumber);
        Assert.Equal(100, projection.VisibleEntries.Count);
        Assert.Equal("Entry 001", projection.VisibleEntries[0].Message);
        Assert.Equal("Entry 100", projection.VisibleEntries[^1].Message);
    }

    [Fact]
    public void Build_LiveEntriesWithAutoScrollOn_JumpsToNewestPage()
    {
        var entries = CreateSequencedEntries(205);
        var projection = Build(
            entries,
            pageSize: 100,
            currentPageNumber: 2,
            autoScroll: true,
            reason: GameLogProjectionRefreshReason.LiveEntriesChanged,
            previousFilteredCount: 150);

        Assert.Equal(3, projection.TotalPages);
        Assert.Equal(3, projection.CurrentPageNumber);
        Assert.Equal("Entry 201", projection.VisibleEntries[0].Message);
        Assert.Equal("Entry 205", projection.VisibleEntries[^1].Message);
    }

    [Fact]
    public void Build_LiveEntriesWithAutoScrollOff_StaysOnCurrentPage()
    {
        var entries = CreateSequencedEntries(205);
        var projection = Build(
            entries,
            pageSize: 100,
            currentPageNumber: 1,
            autoScroll: false,
            reason: GameLogProjectionRefreshReason.LiveEntriesChanged,
            previousFilteredCount: 150);

        Assert.Equal(3, projection.TotalPages);
        Assert.Equal(1, projection.CurrentPageNumber);
        Assert.Equal("Entry 001", projection.VisibleEntries[0].Message);
        Assert.Equal("Entry 100", projection.VisibleEntries[^1].Message);
    }

    [Fact]
    public void Build_NonMatchingLiveEntries_DoNotChangeFilteredContents()
    {
        var entries = CreateSequencedEntries(120);
        var projection = Build(
            entries,
            searchQuery: "Match",
            pageSize: 100,
            currentPageNumber: 1,
            autoScroll: false,
            reason: GameLogProjectionRefreshReason.LiveEntriesChanged,
            previousFilteredCount: 0);

        Assert.Equal(0, projection.FilteredEntryCount);
        Assert.Empty(projection.VisibleEntries);
        Assert.Equal(1, projection.CurrentPageNumber);
    }

    private static GameLogProjectionResult Build(
        IEnumerable<GameLogEntry> entries,
        string? searchQuery = null,
        string? selectedLevelFilter = "All",
        int pageSize = 100,
        int currentPageNumber = 1,
        bool autoScroll = false,
        GameLogProjectionRefreshReason reason = GameLogProjectionRefreshReason.FilterChanged,
        int previousFilteredCount = 0)
        => GameLogProjectionBuilder.Build(entries, searchQuery, selectedLevelFilter, pageSize, currentPageNumber, autoScroll, reason, previousFilteredCount);

    private static IReadOnlyList<GameLogEntry> CreateSequencedEntries(int count)
        => Enumerable.Range(1, count)
            .Select(index => CreateEntry($"Entry {index:000}"))
            .ToList();

    private static GameLogEntry CreateEntry(
        string message,
        GameLogLevel level = GameLogLevel.Info,
        string? detailsText = null,
        string? threadName = "Render thread",
        string? loggerName = "net.minecraft.client.Minecraft") => new()
        {
            Timestamp = DateTimeOffset.UtcNow,
            OriginalTimeText = "11:14:57",
            Level = level,
            Message = message,
            DetailsText = detailsText,
            ThreadName = threadName,
            LoggerName = loggerName,
            Source = GameLogSource.StandardOutput,
            RawPayload = message
        };
}
