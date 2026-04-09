using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Emerald.CoreX.Runtime;

/// <summary>
/// Identifies why the visible log projection needs to be recalculated.
/// </summary>
internal enum GameLogProjectionRefreshReason
{
    SessionChanged,
    FilterChanged,
    PageChanged,
    LiveEntriesChanged,
    EntriesChanged
}

/// <summary>
/// Holds the filtered and paged log entries that should be shown in the UI.
/// </summary>
internal sealed class GameLogProjectionResult
{
    public required IReadOnlyList<GameLogEntry> VisibleEntries { get; init; }

    public required int FilteredEntryCount { get; init; }

    public required int TotalPages { get; init; }

    public required int CurrentPageNumber { get; init; }
}

/// <summary>
/// Builds the filtered and paged log projection used by the logs page.
/// </summary>
internal static class GameLogProjectionBuilder
{
    private static ILogger Logger
    {
        get
        {
            try
            {
                return Ioc.Default.GetService<ILoggerFactory>()?.CreateLogger(typeof(GameLogProjectionBuilder).FullName!)
                    ?? NullLogger.Instance;
            }
            catch (InvalidOperationException)
            {
                return NullLogger.Instance;
            }
        }
    }

    /// <summary>
    /// Produces the visible page of log entries based on the current filters and navigation state.
    /// </summary>
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

        Logger.LogDebug(
            "Built log projection. Reason: {Reason}. HasQuery: {HasQuery}. LevelFilter: {SelectedLevelFilter}. PageSize: {PageSize}. RequestedPage: {RequestedPage}. TargetPage: {TargetPage}. FilteredEntries: {FilteredEntryCount}. TotalPages: {TotalPages}. AutoScroll: {AutoScroll}",
            reason,
            !string.IsNullOrWhiteSpace(searchQuery),
            selectedLevelFilter ?? "All",
            safePageSize,
            currentPageNumber,
            targetPage,
            filteredEntries.Count,
            totalPages,
            autoScroll);

        return new GameLogProjectionResult
        {
            VisibleEntries = pageEntries,
            FilteredEntryCount = filteredEntries.Count,
            TotalPages = totalPages,
            CurrentPageNumber = targetPage
        };
    }

    /// <summary>
    /// Determines whether the supplied entry matches the active filter state.
    /// </summary>
    internal static bool MatchesEntry(GameLogEntry entry, string? searchQuery, string? selectedLevelFilter)
        => MatchesLevelFilter(entry, selectedLevelFilter)
            && MatchesSearch(entry, searchQuery);

    /// <summary>
    /// Chooses which page should be shown after a refresh.
    /// </summary>
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
