using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Emerald.CoreX;
using Emerald.CoreX.Runtime;
namespace Emerald.ViewModels;

public sealed record PlaytimeSessionRow(string Name, string Target, string Glyph, string Duration, string Started);
public sealed record PlaytimeRankingRow(int Rank, string Name, string Duration, double Proportion);
public partial class PlaytimePageViewModel(Core core, IInstancePlaytimeService playtime, IGameRuntimeService runtime) : ObservableObject
{
    private PlaytimeAnalyticsSnapshot _result = new();
    private bool _initializing;
    private int _sessionPage;
    private int _rankingPage;
    public ObservableCollection<Choice<PlaytimeScope>> Scopes { get; } = [];
    public IReadOnlyList<Choice<PlaytimeRange>> Ranges { get; } = [new(PlaytimeRange.SevenDays, DashboardText.Get("SevenDays")), new(PlaytimeRange.ThirtyDays, DashboardText.Get("ThirtyDays")), new(PlaytimeRange.NinetyDays, DashboardText.Get("NinetyDays")), new(PlaytimeRange.AllTime, DashboardText.Get("AllTime"))];
    public ObservableCollection<PlaytimeKpiViewModel> Kpis { get; } = [];
    public ObservableCollection<PlaytimeBarViewModel> DailyBars { get; } = [];
    public ObservableCollection<PlaytimeBarViewModel> WeekdayBars { get; } = [];
    public ObservableCollection<PlaytimeHeatRowViewModel> Heatmap { get; } = [];
    public ObservableCollection<PlaytimeRankingRow> Rankings { get; } = [];
    public ObservableCollection<PlaytimeSessionRow> Sessions { get; } = [];
    [ObservableProperty] private Choice<PlaytimeScope>? _selectedScope;
    [ObservableProperty] private Choice<PlaytimeRange>? _selectedRange;
    [ObservableProperty] private string _chartTitle = string.Empty;
    [ObservableProperty] private string _chartScale = string.Empty;
    [ObservableProperty] private string _bestDate = string.Empty;
    [ObservableProperty] private string _bestDateDuration = string.Empty;
    [ObservableProperty] private string _favoriteDay = string.Empty;
    [ObservableProperty] private string _peakHour = string.Empty;
    [ObservableProperty] private string _peakDuration = string.Empty;
    public string PeriodText => SelectedRange?.Label ?? string.Empty;
    public bool HasSessions => _result.Sessions.Count > 0;
    public bool CanPreviousSession => _sessionPage > 0;
    public bool CanNextSession => (_sessionPage + 1) * 10 < _result.Sessions.Count;
    public bool CanPreviousRanking => _rankingPage > 0;
    public bool CanNextRanking => (_rankingPage + 1) * 5 < _result.InstanceRanking.Count;
    public string SessionPageText => PageText(_sessionPage, 10, _result.Sessions.Count);
    public string RankingPageText => PageText(_rankingPage, 5, _result.InstanceRanking.Count);
    public bool HasActive => runtime.Sessions.Any(x => x.IsActive && x.ProcessStartedAt.HasValue);
    public event EventHandler? DataChanged;
    private void HistoryChanged(object? sender, EventArgs e) => DataChanged?.Invoke(this, e);
    public void Activate() { playtime.HistoryChanged += HistoryChanged; }
    public void Deactivate() { playtime.HistoryChanged -= HistoryChanged; }
    public void Initialize(PlaytimeNavigation? navigation = null)
    {
        _initializing = true; var previous = SelectedScope?.Value;
        Scopes.Clear(); Scopes.Add(new(PlaytimeScope.AllEmerald, DashboardText.Get("AllEmerald")));
        if (core.BasePath is { } path)
        {
            Scopes.Add(new(PlaytimeScope.ForBase(path.BasePath), DashboardText.Get("CurrentBase")));
            foreach (var game in core.Games) Scopes.Add(new(PlaytimeScope.ForInstance(path.BasePath, game.InstanceId), game.Version.DisplayName));
        }
        SelectedScope = Scopes.FirstOrDefault(x => x.Value == (navigation?.Scope ?? previous)) ?? Scopes[0];
        SelectedRange = Ranges.FirstOrDefault(x => x.Value == navigation?.Range) ?? SelectedRange ?? Ranges[1];
        _initializing = false; Refresh();
    }
    partial void OnSelectedScopeChanged(Choice<PlaytimeScope>? value) { if (!_initializing) { _sessionPage = _rankingPage = 0; Refresh(); } }
    partial void OnSelectedRangeChanged(Choice<PlaytimeRange>? value) { if (!_initializing) { _sessionPage = _rankingPage = 0; Refresh(); } }
    public void Refresh()
    {
        if (_initializing) return;
        var now = DateTimeOffset.Now; var range = SelectedRange?.Value ?? PlaytimeRange.ThirtyDays;
        _result = playtime.GetAnalytics(SelectedScope?.Value ?? PlaytimeScope.AllEmerald, range, now, activeSessions: DashboardText.Active(core, runtime, now));
        var r = _result;
        Kpis.ReplaceWith([new(DashboardText.Get("TotalPlaytime"), DashboardText.Duration(r.TotalPlaytime)), new(DashboardText.Get("AverageSession"), DashboardText.Duration(r.AverageCompletedSession)), new(DashboardText.Get("ActiveDays"), r.ActiveDayCount.ToString()), new(DashboardText.Get("LongestSession"), DashboardText.Duration(r.LongestCompletedSession)), new(DashboardText.Get("CurrentStreak"), DashboardText.Format("Days", r.CurrentStreakDays))]);
        BuildChart(range);
        var heat = r.HeatmapBuckets.ToDictionary(x => (x.Weekday, x.Hour)); var max = Math.Max(1, r.HeatmapBuckets.Select(x => x.Duration.TotalSeconds).DefaultIfEmpty().Max());
        var weekdays = Enumerable.Range(0, 7).Select(i => (DayOfWeek)((i + (int)System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek) % 7)).ToArray();
        Heatmap.ReplaceWith(weekdays.Select(day => new PlaytimeHeatRowViewModel(System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedDayName(day), Enumerable.Range(0, 24).Select(hour => { var duration = heat.GetValueOrDefault((day, hour))?.Duration ?? TimeSpan.Zero; return new PlaytimeHeatCellViewModel($"{day} {hour:00}", 0.08 + 0.92 * duration.TotalSeconds / max, $"{System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetDayName(day)} {hour:00}:00 · {DashboardText.Duration(duration)}"); }).ToArray())));
        var weekdayTotals = weekdays.Select(day => new { Day = day, Duration = TimeSpan.FromTicks(r.DailyBuckets.Where(x => x.Date.DayOfWeek == day).Sum(x => x.Duration.Ticks)) }).ToArray();
        var dayMax = Math.Max(1, weekdayTotals.Max(x => x.Duration.TotalSeconds));
        WeekdayBars.ReplaceWith(weekdayTotals.Select(x => new PlaytimeBarViewModel(System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedDayName(x.Day), DashboardText.Duration(x.Duration), 48 * x.Duration.TotalSeconds / dayMax)));
        BestDate = r.MostPlayedDate?.ToString("m") ?? DashboardText.Get("NoPatterns");
        BestDateDuration = r.MostPlayedDate is { } best ? DashboardText.Duration(r.DailyBuckets.First(x => x.Date == best).Duration) : string.Empty;
        FavoriteDay = r.MostPlayedWeekday is { } day ? System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetDayName(day) : DashboardText.Get("NoPatterns");
        PeakHour = r.PeakLocalHour is int hour ? $"{hour:00}:00–{(hour + 1) % 24:00}:00" : DashboardText.Get("NoPatterns");
        PeakDuration = r.PeakLocalHour is int peak ? DashboardText.Duration(TimeSpan.FromTicks(r.HeatmapBuckets.Where(x => x.Hour == peak).Sum(x => x.Duration.Ticks))) : string.Empty;
        _sessionPage = Math.Min(_sessionPage, Math.Max(0, (r.Sessions.Count - 1) / 10)); _rankingPage = Math.Min(_rankingPage, Math.Max(0, (r.InstanceRanking.Count - 1) / 5)); UpdatePages(); OnPropertyChanged(nameof(PeriodText));
    }
    private void BuildChart(PlaytimeRange range)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var first = range switch { PlaytimeRange.SevenDays => today.AddDays(-6), PlaytimeRange.ThirtyDays => today.AddDays(-29), PlaytimeRange.NinetyDays => today.AddDays(-89), _ => _result.DailyBuckets.FirstOrDefault()?.Date ?? today };
        var length = today.DayNumber - first.DayNumber + 1;
        var mode = range == PlaytimeRange.NinetyDays || range == PlaytimeRange.AllTime && length > 31 ? length > 366 ? 2 : 1 : 0;
        ChartTitle = DashboardText.Get(mode == 2 ? "MonthlyPlaytime" : mode == 1 ? "WeeklyPlaytime" : "DailyPlaytime");
        var lookup = _result.DailyBuckets.ToDictionary(x => x.Date, x => x.Duration);
        var values = Enumerable.Range(0, length).Select(i => first.AddDays(i)).GroupBy(d => mode == 2 ? new DateOnly(d.Year, d.Month, 1) : mode == 1 ? first.AddDays((d.DayNumber - first.DayNumber) / 7 * 7) : d)
         .Select(g => new { Start = g.Key, End = g.Max(), Duration = TimeSpan.FromTicks(g.Sum(d => lookup.GetValueOrDefault(d).Ticks)) }).ToArray();
        var max = Math.Max(1, values.Select(x => x.Duration.TotalSeconds).DefaultIfEmpty().Max()); ChartScale = DashboardText.Duration(TimeSpan.FromSeconds(max));
        DailyBars.ReplaceWith(values.Select((x, i) => new PlaytimeBarViewModel(i % Math.Max(1, (int)Math.Ceiling(values.Length / 7d)) == 0 ? x.Start.ToString(mode == 2 ? "MMM yy" : "MMM d") : string.Empty,
         $"{x.Start:d}" + (x.End != x.Start ? $" – {x.End:d}" : string.Empty) + $" · {DashboardText.Duration(x.Duration)}", 136 * x.Duration.TotalSeconds / max)));
    }
    private static string PageText(int page, int size, int count) => count == 0 ? DashboardText.Get("NoSessions") : DashboardText.Format("PageCount", page * size + 1, Math.Min(count, (page + 1) * size), count);
    public void ChangeSessionPage(int offset) { _sessionPage = Math.Clamp(_sessionPage + offset, 0, Math.Max(0, (_result.Sessions.Count - 1) / 10)); UpdatePages(); }
    public void ChangeRankingPage(int offset) { _rankingPage = Math.Clamp(_rankingPage + offset, 0, Math.Max(0, (_result.InstanceRanking.Count - 1) / 5)); UpdatePages(); }
    private void UpdatePages()
    {
        Sessions.ReplaceWith(_result.Sessions.Skip(_sessionPage * 10).Take(10).Select(s => new PlaytimeSessionRow(s.InstanceNameSnapshot, s.TargetDisplayNameSnapshot ?? DashboardText.Target(s.TargetKind), DashboardText.Glyph(s.TargetKind), DashboardText.Duration(s.Playtime), DashboardText.Relative(s.StartedAt))));
        var max = Math.Max(1, _result.InstanceRanking.Select(x => x.Duration.TotalSeconds).DefaultIfEmpty().Max());
        Rankings.ReplaceWith(_result.InstanceRanking.Skip(_rankingPage * 5).Take(5).Select((s, i) => new PlaytimeRankingRow(_rankingPage * 5 + i + 1, s.DisplayName, DashboardText.Duration(s.Duration), 100 * s.Duration.TotalSeconds / max)));
        foreach (var name in new[] { nameof(HasSessions), nameof(CanPreviousSession), nameof(CanNextSession), nameof(CanPreviousRanking), nameof(CanNextRanking), nameof(SessionPageText), nameof(RankingPageText) }) OnPropertyChanged(name);
    }
    public static string Format(TimeSpan value) => DashboardText.Duration(value);
}
