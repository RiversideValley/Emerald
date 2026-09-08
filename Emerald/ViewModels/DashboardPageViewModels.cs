using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Emerald.CoreX;
using Emerald.CoreX.Models;
using Emerald.CoreX.Runtime;
using Emerald.CoreX.Services;
using Emerald.CoreX.Services.Servers;
using Emerald.CoreX.Services.Worlds;

namespace Emerald.ViewModels;

public sealed record PlaytimeBarViewModel(string Label, string Duration, double Height);
public sealed record PlaytimeHeatCellViewModel(string Label, double Opacity, string HelpText);
public sealed record PlaytimeHeatRowViewModel(string Label, IReadOnlyList<PlaytimeHeatCellViewModel> Cells);
public sealed record PlaytimeKpiViewModel(string Label, string Value);

public partial class PlaytimePageViewModel(Core core, IInstancePlaytimeService playtime) : ObservableObject
{
    public ObservableCollection<string> Scopes { get; } = ["All Emerald", "Current base"];
    public ObservableCollection<string> Ranges { get; } = ["7 days", "30 days", "90 days", "All time"];
    public ObservableCollection<PlaytimeKpiViewModel> Kpis { get; } = [];
    public ObservableCollection<PlaytimeBarViewModel> DailyBars { get; } = [];
    public ObservableCollection<PlaytimeHeatRowViewModel> Heatmap { get; } = [];
    public ObservableCollection<InstancePlaytimeRanking> Rankings { get; } = [];
    public ObservableCollection<InstancePlaytimeSession> Sessions { get; } = [];
    [ObservableProperty] private string _selectedScope = "All Emerald";
    [ObservableProperty] private string _selectedRange = "30 days";
    [ObservableProperty] private string _patternsText = "No playtime patterns yet.";

    public void Initialize()
    {
        foreach (var game in core.Games)
        {
            Scopes.Add(game.Version.DisplayName);
        }

        Refresh();
    }

    partial void OnSelectedScopeChanged(string value) => Refresh();
    partial void OnSelectedRangeChanged(string value) => Refresh();

    public void Refresh()
    {
        var basePath = core.BasePath?.BasePath;
        var game = core.Games.FirstOrDefault(x => x.Version.DisplayName == SelectedScope);
        var scope = game != null && basePath != null
            ? PlaytimeScope.ForInstance(basePath, game.InstanceId)
            : SelectedScope == "Current base" && basePath != null
                ? PlaytimeScope.ForBase(basePath)
                : PlaytimeScope.AllEmerald;
        var range = SelectedRange switch
        {
            "7 days" => PlaytimeRange.SevenDays,
            "90 days" => PlaytimeRange.NinetyDays,
            "All time" => PlaytimeRange.AllTime,
            _ => PlaytimeRange.ThirtyDays
        };
        var result = playtime.GetAnalytics(scope, range);
        Kpis.ReplaceWith(
        [
            new("Total", Format(result.TotalPlaytime)),
            new("Average session", Format(result.AverageCompletedSession)),
            new("Active days", result.ActiveDayCount.ToString()),
            new("Longest", Format(result.LongestCompletedSession)),
            new("Streak", $"{result.CurrentStreakDays} days")
        ]);
        var max = Math.Max(1, result.DailyBuckets.Select(x => x.Duration.TotalMinutes).DefaultIfEmpty().Max());
        DailyBars.ReplaceWith(
            result.DailyBuckets.Select(x => new PlaytimeBarViewModel(
                x.Date.ToString("MMM d"),
                Format(x.Duration),
                Math.Max(2, 112 * x.Duration.TotalMinutes / max))));
        var heatLookup = result.HeatmapBuckets.ToDictionary(x => (x.Weekday, x.Hour));
        var heatMax = Math.Max(1, result.HeatmapBuckets.Select(x => x.Duration.TotalMinutes).DefaultIfEmpty().Max());
        Heatmap.ReplaceWith(
            Enum.GetValues<DayOfWeek>().Select(day =>
                new PlaytimeHeatRowViewModel(
                    day.ToString()[..2],
                    Enumerable.Range(0, 24)
                        .Select(hour =>
                        {
                            var duration = heatLookup.GetValueOrDefault((day, hour))?.Duration ?? TimeSpan.Zero;
                            return new PlaytimeHeatCellViewModel(
                                $"{day.ToString()[..2]} {hour:00}",
                                0.12 + 0.88 * duration.TotalMinutes / heatMax,
                                $"{day}, {hour:00}:00 — {Format(duration)}");
                        })
                        .ToArray())));
        Rankings.ReplaceWith(result.InstanceRanking);
        Sessions.ReplaceWith(result.Sessions);
        PatternsText = result.MostPlayedDate == null ? "No playtime patterns yet." : $"Best date: {result.MostPlayedDate:MMM d, yyyy}  •  Favorite day: {result.MostPlayedWeekday}  •  Peak hour: {result.PeakLocalHour:00}:00";
    }

    public static string Format(TimeSpan value) => value.TotalHours >= 1 ? $"{(int)value.TotalHours}h {value.Minutes}m" : $"{Math.Max(0, (int)value.TotalMinutes)}m";
}

public partial class ServersPageViewModel(Core core, IAccountService accounts, IServerDirectoryService directory, ISavedServerService saved, IServerStatusService status, IGameRuntimeService runtime) : ObservableObject
{
    public ObservableCollection<Game> Games { get; } = [];
    public ObservableCollection<ServerDirectoryEntry> DiscoverServers { get; } = [];
    public ObservableCollection<SavedServer> FavoriteServers { get; } = [];
    public ObservableCollection<string> Sorts { get; } = ["Votes", "Players", "Rating", "Newest", "Name"];
    [ObservableProperty] private Game? _selectedGame;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _versionFilter = string.Empty;
    [ObservableProperty] private string _tagFilter = string.Empty;
    [ObservableProperty] private string _selectedSort = "Votes";
    [ObservableProperty] private int _page = 1;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;

    public async Task InitializeAsync()
    {
        Games.ReplaceWith(core.Games);
        SelectedGame ??= Games.FirstOrDefault(x => x.CanLaunch) ?? Games.FirstOrDefault();
        VersionFilter = SelectedGame?.Version.BasedOn ?? string.Empty;
        RefreshFavorites();
        await SearchAsync();
    }

    public async Task SearchAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var sort = Enum.TryParse<ServerDirectorySort>(SelectedSort, true, out var parsed)
                ? parsed
                : ServerDirectorySort.Votes;
            var version = string.IsNullOrWhiteSpace(VersionFilter) ? null : VersionFilter;
            var result = await directory.SearchAsync(
                new(SearchText, TagFilter, version, sort, Page, 20),
                cancellationToken);
            DiscoverServers.ReplaceWith(result.Servers);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public SavedServer Favorite(ServerDirectoryEntry entry)
    {
        var result = saved.Save(new SavedServer
        {
            Name = entry.Name,
            Host = entry.Host,
            Port = entry.Port,
            SourceKind = SavedServerSourceKind.Directory,
            SourceSlug = entry.Slug,
            SourcePageUrl = entry.PageUrl,
            IconUrl = entry.IconUrl,
            BannerUrl = entry.BannerUrl
        });
        RefreshFavorites();
        return result;
    }

    public SavedServer AddCustom(string name, string address)
    {
        var parsed = MinecraftServerAddressParser.Parse(address);
        var result = saved.Save(new SavedServer
        {
            Name = string.IsNullOrWhiteSpace(name) ? parsed.Host : name.Trim(),
            Host = parsed.Host,
            Port = parsed.Port,
            SourceKind = SavedServerSourceKind.Custom
        });
        RefreshFavorites();
        return result;
    }

    public void Remove(SavedServer server)
    {
        saved.Remove(server.Id);
        RefreshFavorites();
    }

    public Task<ServerStatusSnapshot> GetStatusAsync(SavedServer server, bool force = false) =>
        status.GetStatusAsync(new(server.Host, server.Port), force);

    public async Task LaunchAsync(SavedServer server)
    {
        if (SelectedGame == null)
        {
            return;
        }

        var account = accounts.GetSelectedAccount();
        if (account == null)
        {
            return;
        }

        var snapshot = await GetStatusAsync(server);
        var host = snapshot.ResolvedIp ?? server.Host;
        var port = snapshot.ResolvedPort ?? server.Port;
        saved.MarkLaunched(server.Id);
        await runtime.LaunchAsync(new GameLaunchRequest(
            SelectedGame,
            account,
            MinecraftLaunchTarget.ForServer(host, port, server.Name)));
    }

    public async Task LaunchAsync(ServerDirectoryEntry server)
    {
        var favorite = Favorite(server);
        await LaunchAsync(favorite);
    }

    private void RefreshFavorites() => FavoriteServers.ReplaceWith(saved.GetAll());
}

public partial class WorldsPageViewModel(Core core, IAccountService accounts, IMinecraftWorldService worlds, IMinecraftLaunchCapabilityResolver capabilities, IGameRuntimeService runtime) : ObservableObject
{
    public ObservableCollection<Game> Games { get; } = [];
    public ObservableCollection<MinecraftWorld> Worlds { get; } = [];
    public ObservableCollection<MinecraftWorld> FilteredWorlds { get; } = [];
    public ObservableCollection<string> Sorts { get; } = ["Last played", "Name", "Size", "Game mode"];
    [ObservableProperty] private Game? _selectedGame;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _selectedSort = "Last played";
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _launchLimitation;
    public async Task InitializeAsync(Game? preferred = null)
    {
        Games.ReplaceWith(core.Games);
        SelectedGame = preferred ?? Games.FirstOrDefault();
        await RefreshAsync();
    }

    partial void OnSelectedGameChanged(Game? value) => _ = RefreshAsync();
    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnSelectedSortChanged(string value) => ApplyFilter();

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        Worlds.Clear();
        FilteredWorlds.Clear();
        if (SelectedGame == null)
        {
            return;
        }

        IsLoading = true;
        try
        {
            Worlds.ReplaceWith(await worlds.ScanAsync(SelectedGame, cancellationToken));
            var caps = await capabilities.ResolveAsync(SelectedGame, cancellationToken);
            LaunchLimitation = caps.WorldLaunchUnavailableReason;

            foreach (var world in Worlds)
            {
                world.CanQuickLaunch = world.IsReadable && SelectedGame.CanLaunch && caps.CanQuickPlayWorld;
            }

            ApplyFilter();
            foreach (var world in Worlds.Take(8))
            {
                _ = worlds.CalculateSizeAsync(world, cancellationToken);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task<bool> LaunchAsync(MinecraftWorld world)
    {
        if (SelectedGame == null || !world.IsReadable || !SelectedGame.CanLaunch)
        {
            return false;
        }

        var caps = await capabilities.ResolveAsync(SelectedGame);
        if (!caps.CanQuickPlayWorld)
        {
            LaunchLimitation = caps.WorldLaunchUnavailableReason;
            return false;
        }

        var account = accounts.GetSelectedAccount();
        if (account == null)
        {
            return false;
        }

        await runtime.LaunchAsync(new GameLaunchRequest(
            SelectedGame,
            account,
            MinecraftLaunchTarget.ForWorld(world.FolderName, world.DisplayName)));
        return true;
    }

    private void ApplyFilter()
    {
        IEnumerable<MinecraftWorld> query = Worlds.Where(x =>
            string.IsNullOrWhiteSpace(SearchText)
            || x.DisplayName.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
            || x.FolderName.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        query = SelectedSort switch
        {
            "Name" => query.OrderBy(x => x.DisplayName),
            "Size" => query.OrderByDescending(x => x.SizeBytes),
            "Game mode" => query.OrderBy(x => x.GameMode),
            _ => query.OrderByDescending(x => x.LastPlayed)
        };
        FilteredWorlds.ReplaceWith(query);
    }
}
