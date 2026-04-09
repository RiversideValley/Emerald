namespace Emerald.CoreX.Runtime;

internal enum GameLogProjectionRefreshReason
{
    SessionChanged,
    FilterChanged,
    PageChanged,
    LiveEntriesChanged,
    EntriesChanged
}

internal sealed class GameLogProjectionResult
{
    public required IReadOnlyList<GameLogEntry> VisibleEntries { get; init; }

    public required int FilteredEntryCount { get; init; }

    public required int TotalPages { get; init; }

    public required int CurrentPageNumber { get; init; }
}

internal static class GameLogProjectionBuilder
{
    public static GameLogProjectionResult Build(
        IEnumerable<GameLogEntry> entries,
        string? searchQuery,
        string? selectedLevelFilter,
        int pageSize,
        int currentPageNumber,
        bool autoScroll,
        GameLogProjectionRefreshReason reason,
        int previousFilteredCount)
    {
        var safePageSize = Math.Max(1, pageSize);
        var filteredEntries = entries.Where(entry => MatchesEntry(entry, searchQuery, selectedLevelFilter)).ToList();
        var totalPages = Math.Max(1, (int)Math.Ceiling(filteredEntries.Count / (double)safePageSize));
        var targetPage = DetermineTargetPage(reason, filteredEntries.Count, previousFilteredCount, totalPages, currentPageNumber, autoScroll);
        var pageEntries = filteredEntries
            .Skip((targetPage - 1) * safePageSize)
            .Take(safePageSize)
            .ToList();

        return new GameLogProjectionResult
        {
            VisibleEntries = pageEntries,
            FilteredEntryCount = filteredEntries.Count,
            TotalPages = totalPages,
            CurrentPageNumber = targetPage
        };
    }

    internal static bool MatchesEntry(GameLogEntry entry, string? searchQuery, string? selectedLevelFilter)
        => MatchesLevelFilter(entry, selectedLevelFilter)
            && MatchesSearch(entry, searchQuery);

    private static int DetermineTargetPage(
        GameLogProjectionRefreshReason reason,
        int filteredCount,
        int previousFilteredCount,
        int totalPages,
        int currentPageNumber,
        bool autoScroll)
    {
        var clampedCurrent = Math.Clamp(currentPageNumber, 1, totalPages);

        return reason switch
        {
            GameLogProjectionRefreshReason.SessionChanged => autoScroll ? totalPages : 1,
            GameLogProjectionRefreshReason.FilterChanged => autoScroll ? totalPages : 1,
            GameLogProjectionRefreshReason.LiveEntriesChanged when autoScroll && filteredCount > previousFilteredCount => totalPages,
            _ => clampedCurrent
        };
    }

    private static bool MatchesLevelFilter(GameLogEntry entry, string? selectedLevelFilter)
        => string.IsNullOrWhiteSpace(selectedLevelFilter)
            || string.Equals(selectedLevelFilter, "All", StringComparison.OrdinalIgnoreCase)
            || string.Equals(entry.LevelText, selectedLevelFilter, StringComparison.OrdinalIgnoreCase);

    private static bool MatchesSearch(GameLogEntry entry, string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return Contains(entry.Message, query)
            || Contains(entry.DetailsText, query)
            || Contains(entry.ThreadName, query)
            || Contains(entry.LoggerName, query)
            || Contains(entry.LevelText, query);
    }

    private static bool Contains(string? value, string query)
        => !string.IsNullOrWhiteSpace(value)
            && value.Contains(query, StringComparison.OrdinalIgnoreCase);
}
