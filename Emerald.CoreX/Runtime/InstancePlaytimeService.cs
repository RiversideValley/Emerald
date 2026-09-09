using Emerald.CoreX.Helpers;
using Emerald.Services;
using Microsoft.Extensions.Logging;

namespace Emerald.CoreX.Runtime;

public enum PlaytimeScopeKind
{
    AllEmerald,
    CurrentBase,
    Instance
}

public enum PlaytimeRange
{
    SevenDays,
    ThirtyDays,
    NinetyDays,
    AllTime
}

public sealed record PlaytimeScope(PlaytimeScopeKind Kind, string? BasePath = null, Guid? InstanceId = null)
{
    public static PlaytimeScope AllEmerald { get; } = new(PlaytimeScopeKind.AllEmerald);

    public static PlaytimeScope ForBase(string basePath)
    {
        return new PlaytimeScope(PlaytimeScopeKind.CurrentBase, Normalize(basePath));
    }

    public static PlaytimeScope ForInstance(string basePath, Guid instanceId)
    {
        return new PlaytimeScope(PlaytimeScopeKind.Instance, Normalize(basePath), instanceId);
    }

    internal static string Normalize(string path)
    {
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }
}

public sealed class InstancePlaytimeSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstanceId { get; set; }
    public string BasePathSnapshot { get; set; } = string.Empty;
    public string InstancePath { get; set; } = string.Empty;
    public string InstanceNameSnapshot { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset EndedAt { get; set; }
    public MinecraftLaunchTargetKind TargetKind { get; set; } = MinecraftLaunchTargetKind.Configured;
    public string? TargetDisplayNameSnapshot { get; set; }
    public Guid? QuickProfileId { get; set; }
    public bool Crashed { get; set; }
    public TimeSpan Playtime => EndedAt - StartedAt;
}

public sealed record PlaytimeDayBucket(DateOnly Date, TimeSpan Duration);

public sealed record PlaytimeHeatmapBucket(DayOfWeek Weekday, int Hour, TimeSpan Duration);

public sealed record InstancePlaytimeRanking(
    Guid InstanceId,
    string BasePath,
    string DisplayName,
    TimeSpan Duration,
    int SessionCount);

public sealed record PlaytimeAnalyticsSnapshot
{
    public TimeSpan TotalPlaytime { get; init; }
    public TimeSpan CurrentWeekPlaytime { get; init; }
    public int CompletedSessionCount { get; init; }
    public int ActiveDayCount { get; init; }
    public TimeSpan AveragePerCalendarDay { get; init; }
    public TimeSpan AveragePerActiveDay { get; init; }
    public TimeSpan AverageCompletedSession { get; init; }
    public TimeSpan LongestCompletedSession { get; init; }
    public DateOnly? MostPlayedDate { get; init; }
    public DayOfWeek? MostPlayedWeekday { get; init; }
    public int? PeakLocalHour { get; init; }
    public int CurrentStreakDays { get; init; }
    public int LongestStreakDays { get; init; }
    public IReadOnlyList<PlaytimeDayBucket> DailyBuckets { get; init; } = [];
    public IReadOnlyList<PlaytimeHeatmapBucket> HeatmapBuckets { get; init; } = [];
    public IReadOnlyList<InstancePlaytimeRanking> InstanceRanking { get; init; } = [];
    public IReadOnlyList<InstancePlaytimeSession> Sessions { get; init; } = [];
}

public interface IInstancePlaytimeService
{
    event EventHandler? HistoryChanged
    {
        add { }
        remove { }
    }

    IReadOnlyList<InstancePlaytimeSession> GetSessions(string instancePath);
    TimeSpan GetTotalPlaytime(string instancePath);
    void RecordSession(InstancePlaytimeSession session);

    IReadOnlyList<InstancePlaytimeSession> GetSessions(PlaytimeScope scope)
    {
        return [];
    }

    PlaytimeAnalyticsSnapshot GetAnalytics(PlaytimeScope scope, PlaytimeRange range, DateTimeOffset? now = null,
        TimeZoneInfo? timeZone = null, IReadOnlyList<InstancePlaytimeSession>? activeSessions = null)
    {
        return new PlaytimeAnalyticsSnapshot();
    }
}

internal sealed class PlaytimeHistoryEnvelope
{
    public int SchemaVersion { get; set; } = 1;
    public List<InstancePlaytimeSession> Sessions { get; set; } = [];
}

public sealed class InstancePlaytimeService : IInstancePlaytimeService
{
    private const int SchemaVersion = 1;
    private readonly IBaseSettingsService _baseSettingsService;
    private readonly ILogger<InstancePlaytimeService> _logger;
    private readonly object _syncRoot = new();
    private bool _isReadOnly;

    public event EventHandler? HistoryChanged;

    public InstancePlaytimeService(IBaseSettingsService baseSettingsService, ILogger<InstancePlaytimeService> logger)
    {
        _baseSettingsService = baseSettingsService;
        _logger = logger;
    }

    public IReadOnlyList<InstancePlaytimeSession> GetSessions(string instancePath)
    {
        var normalizedPath = NormalizePath(instancePath);
        lock (_syncRoot)
        {
            return ReadHistory().Where(x => PathsEqual(x.InstancePath, normalizedPath))
                .OrderByDescending(x => x.StartedAt).ToArray();
        }
    }

    public TimeSpan GetTotalPlaytime(string instancePath)
    {
        return GetSessions(instancePath).Aggregate(TimeSpan.Zero, (total, session) => total + session.Playtime);
    }

    public IReadOnlyList<InstancePlaytimeSession> GetSessions(PlaytimeScope scope)
    {
        lock (_syncRoot)
        {
            IEnumerable<InstancePlaytimeSession> query = ReadHistory();
            if (scope.Kind is PlaytimeScopeKind.CurrentBase or PlaytimeScopeKind.Instance)
            {
                query = query.Where(x =>
                    !string.IsNullOrWhiteSpace(scope.BasePath) && PathsEqual(x.BasePathSnapshot, scope.BasePath));
            }

            if (scope.Kind == PlaytimeScopeKind.Instance)
            {
                query = query.Where(x => x.InstanceId == scope.InstanceId);
            }

            return query.OrderByDescending(x => x.StartedAt).ToArray();
        }
    }

    public void RecordSession(InstancePlaytimeSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.EndedAt < session.StartedAt)
        {
            throw new ArgumentException("A playtime session cannot end before it starts.", nameof(session));
        }

        session.InstancePath = NormalizePath(session.InstancePath);
        if (!string.IsNullOrWhiteSpace(session.BasePathSnapshot))
        {
            session.BasePathSnapshot = NormalizePath(session.BasePathSnapshot);
        }

        lock (_syncRoot)
        {
            var history = ReadEnvelope();
            if (_isReadOnly)
            {
                return;
            }

            if (history.Sessions.Any(existing => existing.Id == session.Id))
            {
                return;
            }

            history.Sessions.Add(session);
            _baseSettingsService.Set(SettingsKeys.PlaytimeHistory, history);
        }

        _logger.LogInformation("Recorded {Playtime} of playtime for instance {InstanceId}.", session.Playtime,
            session.InstanceId);
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    public PlaytimeAnalyticsSnapshot GetAnalytics(PlaytimeScope scope, PlaytimeRange range, DateTimeOffset? now = null,
        TimeZoneInfo? timeZone = null, IReadOnlyList<InstancePlaytimeSession>? activeSessions = null)
    {
        var instant = now ?? DateTimeOffset.Now;
        var zone = timeZone ?? TimeZoneInfo.Local;
        var localNow = TimeZoneInfo.ConvertTime(instant, zone);
        var firstDate = FirstDateFor(range, localNow);
        var sessions = GetSessionsInRange(scope, firstDate, instant, zone);
        var completedIds = sessions.Select(x => x.Id).ToHashSet();
        var active = FilterActiveSessions(scope, activeSessions, completedIds);
        var contributions = sessions.Concat(active).ToArray();
        var (dayDurations, heat) = AllocateContributions(contributions, zone, firstDate, localNow);

        var daily = dayDurations.OrderBy(x => x.Key).Select(x => new PlaytimeDayBucket(x.Key, x.Value)).ToArray();
        var total = daily.Aggregate(TimeSpan.Zero, (sum, x) => sum + x.Duration);
        var weekStart = DateOnly.FromDateTime(localNow.Date).AddDays(-(((int)localNow.DayOfWeek + 6) % 7));
        var currentWeek = daily.Where(x => x.Date >= weekStart).Aggregate(TimeSpan.Zero, (sum, x) => sum + x.Duration);
        var calendarDays = CalendarDayCount(range, localNow, daily);
        var activeDates = dayDurations.Keys.Order().ToArray();
        var (currentStreak, longestStreak) = CalculateStreaks(activeDates, DateOnly.FromDateTime(localNow.Date));
        var weekday = daily.GroupBy(x => x.Date.DayOfWeek).OrderByDescending(g => g.Sum(x => x.Duration.Ticks))
            .FirstOrDefault()?.Key;
        var peak = heat.OrderByDescending(x => x.Value).Select(x => (int?)x.Key.Item2).FirstOrDefault();
        var ranking = BuildRanking(contributions, completedIds, zone, firstDate, localNow);

        return new PlaytimeAnalyticsSnapshot
        {
            TotalPlaytime = total,
            CurrentWeekPlaytime = currentWeek,
            CompletedSessionCount = sessions.Count,
            ActiveDayCount = daily.Length,
            AveragePerCalendarDay = Divide(total, calendarDays),
            AveragePerActiveDay = Divide(total, daily.Length),
            AverageCompletedSession = Divide(TimeSpan.FromTicks(sessions.Sum(x => x.Playtime.Ticks)), sessions.Count),
            LongestCompletedSession = sessions.Count == 0 ? TimeSpan.Zero : sessions.Max(x => x.Playtime),
            MostPlayedDate = daily.OrderByDescending(x => x.Duration).Select(x => (DateOnly?)x.Date).FirstOrDefault(),
            MostPlayedWeekday = weekday,
            PeakLocalHour = peak,
            CurrentStreakDays = currentStreak,
            LongestStreakDays = longestStreak,
            DailyBuckets = daily,
            HeatmapBuckets = heat.Select(x => new PlaytimeHeatmapBucket(x.Key.Item1, x.Key.Item2, x.Value))
                .OrderBy(x => x.Weekday).ThenBy(x => x.Hour).ToArray(),
            InstanceRanking = ranking,
            Sessions = sessions
        };
    }

    private static DateOnly FirstDateFor(PlaytimeRange range, DateTimeOffset localNow)
    {
        return range switch
        {
            PlaytimeRange.SevenDays => DateOnly.FromDateTime(localNow.Date).AddDays(-6),
            PlaytimeRange.ThirtyDays => DateOnly.FromDateTime(localNow.Date).AddDays(-29),
            PlaytimeRange.NinetyDays => DateOnly.FromDateTime(localNow.Date).AddDays(-89),
            _ => DateOnly.MinValue
        };
    }

    private IReadOnlyList<InstancePlaytimeSession> GetSessionsInRange(PlaytimeScope scope, DateOnly firstDate,
        DateTimeOffset instant, TimeZoneInfo zone)
    {
        return GetSessions(scope).Where(x =>
            x.StartedAt <= instant &&
            DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(x.EndedAt, zone).Date) >= firstDate).ToArray();
    }

    private static IReadOnlyList<InstancePlaytimeSession> FilterActiveSessions(PlaytimeScope scope,
        IReadOnlyList<InstancePlaytimeSession>? activeSessions, IReadOnlySet<Guid> completedIds)
    {
        return (activeSessions ?? []).Where(x =>
                !completedIds.Contains(x.Id) &&
                (scope.Kind == PlaytimeScopeKind.AllEmerald || PathsEqual(x.BasePathSnapshot, scope.BasePath)) &&
                (scope.Kind != PlaytimeScopeKind.Instance || x.InstanceId == scope.InstanceId))
            .ToArray();
    }

    private static (Dictionary<DateOnly, TimeSpan> Days, Dictionary<(DayOfWeek, int), TimeSpan> Hours)
        AllocateContributions(IEnumerable<InstancePlaytimeSession> sessions, TimeZoneInfo zone, DateOnly firstDate,
            DateTimeOffset localNow)
    {
        var days = new Dictionary<DateOnly, TimeSpan>();
        var hours = new Dictionary<(DayOfWeek, int), TimeSpan>();
        foreach (var session in sessions)
        {
            SplitSession(session, zone, firstDate, localNow, days, hours);
        }

        return (days, hours);
    }

    private static int CalendarDayCount(PlaytimeRange range, DateTimeOffset localNow,
        IReadOnlyList<PlaytimeDayBucket> daily)
    {
        if (range != PlaytimeRange.AllTime)
        {
            return range switch { PlaytimeRange.SevenDays => 7, PlaytimeRange.ThirtyDays => 30, _ => 90 };
        }

        return Math.Max(1, daily.Count == 0
            ? 1
            : DateOnly.FromDateTime(localNow.Date).DayNumber - daily[0].Date.DayNumber + 1);
    }

    private static IReadOnlyList<InstancePlaytimeRanking> BuildRanking(
        IEnumerable<InstancePlaytimeSession> contributions, IReadOnlySet<Guid> completedIds, TimeZoneInfo zone,
        DateOnly firstDate, DateTimeOffset localNow)
    {
        return contributions.GroupBy(x => (x.InstanceId,
                Base: OperatingSystem.IsWindows() ? x.BasePathSnapshot.ToUpperInvariant() : x.BasePathSnapshot))
            .Select(g => new InstancePlaytimeRanking(g.Key.InstanceId, g.First().BasePathSnapshot,
                g.OrderByDescending(x => x.StartedAt).First().InstanceNameSnapshot,
                g.Aggregate(TimeSpan.Zero, (sum, session) => sum + Contribution(session, zone, firstDate, localNow)),
                g.Count(x => completedIds.Contains(x.Id))))
            .OrderByDescending(x => x.Duration).ToArray();
    }

    private static TimeSpan Contribution(InstancePlaytimeSession session, TimeZoneInfo zone, DateOnly firstDate,
        DateTimeOffset localNow)
    {
        var days = new Dictionary<DateOnly, TimeSpan>();
        SplitSession(session, zone, firstDate, localNow, days, new Dictionary<(DayOfWeek, int), TimeSpan>());
        return days.Values.Aggregate(TimeSpan.Zero, (sum, value) => sum + value);
    }

    private PlaytimeHistoryEnvelope ReadEnvelope()
    {
        var envelope = _baseSettingsService.Get(SettingsKeys.PlaytimeHistory, new PlaytimeHistoryEnvelope());
        if (envelope.SchemaVersion > SchemaVersion)
        {
            _isReadOnly = true;
            _logger.LogWarning(
                "Playtime history schema {SchemaVersion} is newer than supported schema {SupportedSchemaVersion}; leaving it unchanged.",
                envelope.SchemaVersion, SchemaVersion);
        }

        return envelope;
    }

    private IReadOnlyList<InstancePlaytimeSession> ReadHistory()
    {
        return ReadEnvelope().Sessions;
    }

    private static void SplitSession(InstancePlaytimeSession session, TimeZoneInfo zone, DateOnly firstDate,
        DateTimeOffset localNow, Dictionary<DateOnly, TimeSpan> days, Dictionary<(DayOfWeek, int), TimeSpan> hours)
    {
        var cursor = TimeZoneInfo.ConvertTime(session.StartedAt, zone);
        var end = TimeZoneInfo.ConvertTime(session.EndedAt, zone);
        if (end > localNow)
        {
            end = localNow;
        }

        while (cursor < end)
        {
            var nextHour = new DateTimeOffset(cursor.Year, cursor.Month, cursor.Day, cursor.Hour, 0, 0, cursor.Offset)
                .AddHours(1);
            var segmentEnd = nextHour < end ? nextHour : end;
            var date = DateOnly.FromDateTime(cursor.Date);
            if (date >= firstDate)
            {
                var duration = segmentEnd - cursor;
                days[date] = days.GetValueOrDefault(date) + duration;
                var key = (cursor.DayOfWeek, cursor.Hour);
                hours[key] = hours.GetValueOrDefault(key) + duration;
            }

            cursor = TimeZoneInfo.ConvertTime(segmentEnd, zone);
        }
    }

    private static (int Current, int Longest) CalculateStreaks(DateOnly[] dates, DateOnly today)
    {
        if (dates.Length == 0)
        {
            return (0, 0);
        }

        var longest = 1;
        var streak = 1;
        for (var i = 1; i < dates.Length; i++)
        {
            if (dates[i].DayNumber == dates[i - 1].DayNumber + 1)
            {
                streak++;
            }
            else
            {
                streak = 1;
            }

            longest = Math.Max(longest, streak);
        }

        var last = dates[^1];
        var current = last == today || last == today.AddDays(-1) ? 1 : 0;
        for (var i = dates.Length - 2; current > 0 && i >= 0 && dates[i + 1].DayNumber == dates[i].DayNumber + 1; i--)
        {
            current++;
        }

        return (current, longest);
    }

    private static TimeSpan Divide(TimeSpan value, int divisor)
    {
        return divisor <= 0 ? TimeSpan.Zero : TimeSpan.FromTicks(value.Ticks / divisor);
    }

    private static bool PathsEqual(string left, string? right)
    {
        return !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right) && string.Equals(
            NormalizePath(left), NormalizePath(right),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    private static string NormalizePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return PlaytimeScope.Normalize(path);
    }
}
