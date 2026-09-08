using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Emerald.CoreX;
using Emerald.CoreX.Helpers;
using Emerald.CoreX.Installation;
using Emerald.CoreX.Models;
using Emerald.CoreX.Notifications;
using Emerald.CoreX.Runtime;
using Emerald.CoreX.Services;
using Emerald.CoreX.Services.Servers;
using Emerald.CoreX.Services.Worlds;

namespace Emerald.ViewModels;

public sealed class QuickProfileCardViewModel
{
    private readonly QuickProfileValidation _validation;
    public QuickProfile Profile { get; }
    public QuickProfileCardViewModel(QuickProfile profile, QuickProfileValidation validation) { Profile = profile; _validation = validation; }
    public string Name => Profile.Name;
    public string Glyph => Profile.GlyphKey switch { "Server" => "\uE968", "World" => "\uE909", "Adventure" => "\uE7FC", "Build" => "\uE70F", _ => "\uE768" };
    public string Target => Profile.TargetDisplayNameSnapshot ?? Profile.TargetKind.ToString();
    public bool NeedsAttention => !_validation.IsValid;
    public string Status => _validation.IsValid ? Target : "Needs attention";
}

public partial class HomePageViewModel : ObservableObject
{
    private readonly Core _core;
    private readonly IAccountService _accounts;
    private readonly IGameRuntimeService _runtime;
    private readonly IInstancePlaytimeService _playtime;
    private readonly IHomePreferencesService _preferences;
    private readonly ISavedServerService _servers;
    private readonly IServerStatusService _status;
    private readonly IMinecraftWorldService _worlds;
    private readonly IMinecraftLaunchCapabilityResolver _capabilities;
    private readonly IQuickProfileService _profiles;
    private readonly INotificationService _notifications;
    private HomePreferences _homePreferences = new();

    public ObservableCollection<Game> Games { get; } = [];
    public ObservableCollection<SavedServer> FavoriteServers { get; } = [];
    public ObservableCollection<MinecraftWorld> RecentWorlds { get; } = [];
    public ObservableCollection<QuickProfileCardViewModel> QuickProfiles { get; } = [];
    public ObservableCollection<PlaytimeBarViewModel> HomeDailyBars { get; } = [];
    public IReadOnlyList<string> DestinationOptions { get; } = ["Main menu", "Server", "World"];

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasGames))] [NotifyPropertyChangedFor(nameof(IsHeroEnabled))] private Game? _selectedGame;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasAccount))] [NotifyPropertyChangedFor(nameof(AccountName))] [NotifyPropertyChangedFor(nameof(PrimaryButtonText))] private EAccount? _selectedAccount;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsServerDestination))] [NotifyPropertyChangedFor(nameof(IsWorldDestination))] private string _selectedDestination = "Main menu";
    [ObservableProperty] private SavedServer? _selectedServer;
    [ObservableProperty] private MinecraftWorld? _selectedWorld;
    [ObservableProperty] private Guid? _activeQuickProfileId;
    [ObservableProperty] private string _totalPlaytimeText = "0m";
    [ObservableProperty] private string _weekPlaytimeText = "0m";
    [ObservableProperty] private string _sessionCountText = "0 sessions";
    [ObservableProperty] private string _runtimeElapsedText = string.Empty;
    [ObservableProperty] private string _favoriteServerStatus = string.Empty;
    [ObservableProperty] private bool _showAllEmeraldPlaytime;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _launchMessage;

    public bool HasGames => Games.Count > 0;
    public bool HasAccount => SelectedAccount != null;
    public bool IsHeroEnabled => SelectedGame != null;
    public bool IsServerDestination => SelectedDestination == "Server";
    public bool IsWorldDestination => SelectedDestination == "World";
    public string AccountName => SelectedAccount?.Name ?? "Choose an account";
    public string PrimaryButtonText => !HasAccount ? "Choose account" : SelectedGame?.HasActiveSession == true ? "Running" : SelectedGame?.CanLaunch == true ? "Play" : "Manage instance";
    public string InstanceDetails => SelectedGame == null ? "No instance selected" : $"{SelectedGame.Version.Type} • {SelectedGame.Version.BasedOn} • {SelectedGame.InstallationStatusText}";
    public string RuntimeStatus => SelectedGame?.StatusText ?? "Install an instance to begin";
    public MinecraftWorld? MostRecentWorld => RecentWorlds.FirstOrDefault();
    public SavedServer? FavoriteServer => FavoriteServers.FirstOrDefault();
    public string MostRecentWorldText => MostRecentWorld?.DisplayName ?? "NoLocalWorldsYet".Localize();

    public HomePageViewModel(Core core, IAccountService accounts, IGameRuntimeService runtime, IInstancePlaytimeService playtime, IHomePreferencesService preferences, ISavedServerService servers, IServerStatusService status, IMinecraftWorldService worlds, IMinecraftLaunchCapabilityResolver capabilities, IQuickProfileService profiles, INotificationService notifications)
    {
        _core = core; _accounts = accounts; _runtime = runtime; _playtime = playtime; _preferences = preferences; _servers = servers; _status = status; _worlds = worlds; _capabilities = capabilities; _profiles = profiles; _notifications = notifications;
        _core.Games.CollectionChanged += OnSourceChanged; _accounts.Accounts.CollectionChanged += OnSourceChanged;
        _playtime.HistoryChanged += OnHistoryChanged; _servers.Changed += OnRepositoryChanged; _profiles.Changed += OnRepositoryChanged;
    }

    public async Task InitializeAsync()
    {
        _homePreferences = _preferences.Load();
        ShowAllEmeraldPlaytime = _homePreferences.ShowAllEmeraldPlaytime;
        Games.ReplaceWith(_core.Games);
        SelectedAccount = _accounts.GetSelectedAccount();
        SelectedGame = ResolveInitialGame();
        FavoriteServers.ReplaceWith(_servers.GetAll());
        _ = RefreshFavoriteStatusAsync();
        await LoadWorldsAsync();
        RefreshProfiles(); RefreshAnalytics(); RefreshRuntime();
    }

    partial void OnSelectedGameChanged(Game? oldValue, Game? newValue)
    {
        if (oldValue != null) oldValue.PropertyChanged -= OnGamePropertyChanged;
        if (newValue != null) newValue.PropertyChanged += OnGamePropertyChanged;
        _homePreferences.SelectedInstanceId = newValue?.InstanceId; _preferences.Save(_homePreferences);
        ActiveQuickProfileId = null; OnPropertyChanged(nameof(InstanceDetails)); OnPropertyChanged(nameof(RuntimeStatus)); OnPropertyChanged(nameof(PrimaryButtonText));
        _ = LoadWorldsAsync(); RefreshAnalytics();
    }

    partial void OnSelectedDestinationChanged(string value) { ActiveQuickProfileId = null; LaunchMessage = null; }
    partial void OnSelectedServerChanged(SavedServer? value) { ActiveQuickProfileId = null; }
    partial void OnSelectedWorldChanged(MinecraftWorld? value) { ActiveQuickProfileId = null; }
    partial void OnShowAllEmeraldPlaytimeChanged(bool value) { _homePreferences.ShowAllEmeraldPlaytime = value; _preferences.Save(_homePreferences); RefreshAnalytics(); }

    [RelayCommand]
    private async Task LaunchAsync()
    {
        if (SelectedGame == null) return;
        SelectedAccount = _accounts.GetSelectedAccount();
        if (SelectedAccount == null) { LaunchMessage = "Choose an account before launching."; return; }
        if (!SelectedGame.CanLaunch) { LaunchMessage = SelectedGame.InstallationState == InstanceInstallationState.Ready ? "This instance is already active." : "Manage or repair this instance before launching."; return; }
        var target = await BuildTargetAsync(); if (target == null) return;
        IsBusy = true;
        try { await _runtime.LaunchAsync(new GameLaunchRequest(SelectedGame, SelectedAccount, target, ActiveQuickProfileId)); }
        finally { IsBusy = false; RefreshRuntime(); }
    }

    [RelayCommand] private Task StopAsync() => SelectedGame == null ? Task.CompletedTask : _runtime.StopAsync(SelectedGame, GameStopMode.Gentle);
    [RelayCommand] private Task ForceStopAsync() => SelectedGame == null ? Task.CompletedTask : _runtime.StopAsync(SelectedGame, GameStopMode.Force);

    public async Task SelectProfileAsync(QuickProfile profile, bool launch)
    {
        var validation = _profiles.Validate(profile, Games, _accounts.Accounts, FavoriteServers);
        if (!validation.IsValid) { _notifications.Warning("Profile needs attention", string.Join(" ", validation.Messages)); return; }
        var game = Games.First(x => x.InstanceId == profile.InstanceId);
        var account = _accounts.Accounts.First(x => x.UniqueId == profile.AccountUniqueId);
        _accounts.SetSelectedAccount(account); SelectedAccount = account; SelectedGame = game; ActiveQuickProfileId = profile.Id;
        if (profile.TargetKind == MinecraftLaunchTargetKind.Server) { SelectedDestination = "Server"; SelectedServer = FavoriteServers.First(x => x.Id == profile.SavedServerId); }
        else if (profile.TargetKind == MinecraftLaunchTargetKind.World) { SelectedDestination = "World"; SelectedWorld = RecentWorlds.First(x => x.FolderName == profile.WorldFolderName); }
        else SelectedDestination = "Main menu";
        ActiveQuickProfileId = profile.Id;
        if (launch) await LaunchAsync();
    }

    public void RefreshRuntime()
    {
        if (SelectedGame == null) return;
        var session = _runtime.TryGetActiveSession(SelectedGame);
        RuntimeElapsedText = session?.IsActive == true ? FormatDuration(DateTimeOffset.Now - session.StartedAt) : string.Empty;
        OnPropertyChanged(nameof(RuntimeStatus)); OnPropertyChanged(nameof(PrimaryButtonText));
    }

    private async Task<MinecraftLaunchTarget?> BuildTargetAsync()
    {
        if (SelectedDestination == "Server")
        {
            if (SelectedServer == null) { LaunchMessage = "Choose a favorite server or browse servers."; return null; }
            var address = new MinecraftServerAddress(SelectedServer.Host, SelectedServer.Port);
            var snapshot = await _status.GetStatusAsync(address);
            var host = snapshot.ResolvedIp ?? address.Host; var port = snapshot.ResolvedPort ?? address.Port;
            _servers.MarkLaunched(SelectedServer.Id);
            return MinecraftLaunchTarget.ForServer(host, port, SelectedServer.Name);
        }
        if (SelectedDestination == "World")
        {
            if (SelectedWorld == null) { LaunchMessage = "Choose a world or browse local saves."; return null; }
            var capability = await _capabilities.ResolveAsync(SelectedGame!);
            if (!capability.CanQuickPlayWorld) { LaunchMessage = capability.WorldLaunchUnavailableReason; return null; }
            return MinecraftLaunchTarget.ForWorld(SelectedWorld.FolderName, SelectedWorld.DisplayName);
        }
        return MinecraftLaunchTarget.MainMenu;
    }

    private Game? ResolveInitialGame()
    {
        var persisted = Games.FirstOrDefault(x => x.InstanceId == _homePreferences.SelectedInstanceId); if (persisted != null) return persisted;
        var basePath = _core.BasePath?.BasePath;
        if (basePath != null)
        {
            var recent = _playtime.GetSessions(PlaytimeScope.ForBase(basePath)).FirstOrDefault();
            var recentGame = Games.FirstOrDefault(x => x.InstanceId == recent?.InstanceId); if (recentGame != null) return recentGame;
        }
        return Games.FirstOrDefault(x => x.CanLaunch) ?? Games.FirstOrDefault();
    }

    private async Task LoadWorldsAsync()
    {
        RecentWorlds.Clear(); if (SelectedGame == null) return;
        try { RecentWorlds.ReplaceWith((await _worlds.ScanAsync(SelectedGame)).Take(8)); SelectedWorld ??= RecentWorlds.FirstOrDefault(); OnPropertyChanged(nameof(MostRecentWorld)); OnPropertyChanged(nameof(MostRecentWorldText)); }
        catch (OperationCanceledException) { }
    }
    private void RefreshAnalytics()
    {
        if (_core.BasePath == null) return;
        var scope = ShowAllEmeraldPlaytime || SelectedGame == null ? PlaytimeScope.AllEmerald : PlaytimeScope.ForInstance(_core.BasePath.BasePath, SelectedGame.InstanceId);
        var result = _playtime.GetAnalytics(scope, PlaytimeRange.SevenDays);
        TotalPlaytimeText = FormatDuration(result.TotalPlaytime); WeekPlaytimeText = FormatDuration(result.CurrentWeekPlaytime); SessionCountText = $"{result.CompletedSessionCount} sessions";
        var maximum = Math.Max(1, result.DailyBuckets.Select(x => x.Duration.TotalMinutes).DefaultIfEmpty().Max());
        HomeDailyBars.ReplaceWith(result.DailyBuckets.TakeLast(7).Select(x => new PlaytimeBarViewModel(x.Date.ToString("ddd"), FormatDuration(x.Duration), Math.Max(3, 34 * x.Duration.TotalMinutes / maximum))));
    }
    private void RefreshProfiles() { QuickProfiles.ReplaceWith(_profiles.GetAll().Select(x => new QuickProfileCardViewModel(x, _profiles.Validate(x, Games, _accounts.Accounts, FavoriteServers)))); }
    public void ReloadProfiles() => RefreshProfiles();
    private void OnHistoryChanged(object? sender, EventArgs e) => RefreshAnalytics();
    private void OnRepositoryChanged(object? sender, EventArgs e) { FavoriteServers.ReplaceWith(_servers.GetAll()); _ = RefreshFavoriteStatusAsync(); RefreshProfiles(); }
    private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e) { Games.ReplaceWith(_core.Games); SelectedAccount = _accounts.GetSelectedAccount(); if (SelectedGame == null || !Games.Contains(SelectedGame)) SelectedGame = ResolveInitialGame(); RefreshProfiles(); }
    private void OnGamePropertyChanged(object? sender, PropertyChangedEventArgs e) { OnPropertyChanged(nameof(RuntimeStatus)); OnPropertyChanged(nameof(PrimaryButtonText)); }
    private async Task RefreshFavoriteStatusAsync()
    {
        var favorite = FavoriteServer;
        if (favorite == null) { FavoriteServerStatus = "NoFavoriteServerYet".Localize(); return; }
        try
        {
            var snapshot = await _status.GetStatusAsync(new MinecraftServerAddress(favorite.Host, favorite.Port));
            FavoriteServerStatus = snapshot.State == ServerStatusState.Online
                ? string.Format("FavoriteServerOnlineFormat".Localize(), favorite.Name, snapshot.Players, snapshot.MaxPlayers)
                : string.Format("FavoriteServerStateFormat".Localize(), favorite.Name, snapshot.State);
        }
        catch { FavoriteServerStatus = string.Format("FavoriteServerUnavailableFormat".Localize(), favorite.Name); }
    }
    private static string FormatDuration(TimeSpan value) => value.TotalHours >= 1 ? $"{(int)value.TotalHours}h {value.Minutes}m" : $"{Math.Max(0, (int)value.TotalMinutes)}m";
}

internal static class ObservableCollectionHomeExtensions
{
    public static void ReplaceWith<T>(this ObservableCollection<T> target, IEnumerable<T> values) { target.Clear(); foreach (var value in values) target.Add(value); }
}
