using System.Collections.ObjectModel;
using System.Globalization;
using Emerald.CoreX;
using Emerald.CoreX.Helpers;
using Emerald.CoreX.Runtime;

namespace Emerald.ViewModels;

public sealed record Choice<T>(T Value, string Label)
{
    public override string ToString()
    {
        return Label;
    }
}

public sealed record HomeSelection(
    Game Game,
    MinecraftLaunchTargetKind Kind,
    CoreX.Services.Servers.SavedServer? Server = null,
    CoreX.Services.Worlds.MinecraftWorld? World = null,
    CoreX.Models.EAccount? Account = null,
    Guid? ProfileId = null);

public sealed record PlaytimeNavigation(PlaytimeScope Scope, PlaytimeRange Range);

public sealed record PlaytimeBarViewModel(string Label, string Duration, double Height);

public sealed record PlaytimeHeatCellViewModel(string Label, double Opacity, string HelpText);

public sealed record PlaytimeHeatRowViewModel(string Label, IReadOnlyList<PlaytimeHeatCellViewModel> Cells);

public sealed record PlaytimeKpiViewModel(string Label, string Value);

public static class DashboardText
{
    public static string Get(string key)
    {
        return ("Refine" + key).Localize();
    }

    public static string Format(string key, params object[] args)
    {
        return string.Format(CultureInfo.CurrentCulture, Get(key), args);
    }

    public static string Duration(TimeSpan value)
    {
        return value.TotalHours >= 1 ? Format("HoursMinutes", (int)value.TotalHours, value.Minutes)
            : value.TotalMinutes >= 1 ? Format("Minutes", (int)value.TotalMinutes)
            : value.TotalSeconds >= 1 ? Format("Seconds", (int)value.TotalSeconds)
            : value > TimeSpan.Zero ? Get("UnderSecond") : Format("Minutes", 0);
    }

    public static string Relative(DateTimeOffset? value)
    {
        if (value == null)
        {
            return Get("NeverPlayed");
        }

        var local = value.Value.ToLocalTime();
        var days = (DateTime.Today - local.Date).Days;
        return days == 0 ? Format("TodayAt", local.ToString("t")) :
            days == 1 ? Format("YesterdayAt", local.ToString("t")) : local.ToString("d");
    }

    public static string Glyph(MinecraftLaunchTargetKind kind)
    {
        return kind switch
        {
            MinecraftLaunchTargetKind.World => "\uE909", MinecraftLaunchTargetKind.Server => "\uE968", _ => "\uE768"
        };
    }

    public static string Target(MinecraftLaunchTargetKind kind)
    {
        return Get(kind == MinecraftLaunchTargetKind.World ? "World" :
            kind == MinecraftLaunchTargetKind.Server ? "Server" : "MainMenu");
    }

    public static IReadOnlyList<InstancePlaytimeSession> Active(Core core, IGameRuntimeService runtime,
        DateTimeOffset now)
    {
        return runtime.Sessions
            .Where(s => s.IsActive && s.ProcessStartedAt.HasValue).Select(s => new InstancePlaytimeSession
            {
                Id = s.SessionId,
                InstanceId = s.Game.InstanceId,
                BasePathSnapshot = s.Game.SharedMinecraftBasePath ?? string.Empty,
                InstancePath = s.GamePath,
                InstanceNameSnapshot = s.DisplayName,
                StartedAt = s.ProcessStartedAt!.Value,
                EndedAt = now,
                TargetKind = s.Target.Kind,
                TargetDisplayNameSnapshot = s.TargetDisplayName
            }).ToArray();
    }

    public static IReadOnlyList<PlaytimeBarViewModel> Bars(IEnumerable<PlaytimeDayBucket> buckets, int days,
        double height)
    {
        var values = buckets.ToDictionary(x => x.Date, x => x.Duration);
        var start = DateOnly.FromDateTime(DateTime.Today).AddDays(1 - days);
        var all = Enumerable.Range(0, days)
            .Select(i => new PlaytimeDayBucket(start.AddDays(i), values.GetValueOrDefault(start.AddDays(i)))).ToArray();
        var max = Math.Max(1, all.Select(x => x.Duration.TotalSeconds).DefaultIfEmpty().Max());
        return all.Select(x => new PlaytimeBarViewModel(x.Date.ToString("ddd"), $"{x.Date:d} · {Duration(x.Duration)}",
            height * x.Duration.TotalSeconds / max)).ToArray();
    }
}
