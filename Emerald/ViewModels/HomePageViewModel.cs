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

public partial class HomePageViewModel : ObservableObject
{
    private readonly Core _core;
    private readonly IUiDispatcher _dispatcher;
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
    private CancellationTokenSource? _worldCancellation;
    private CancellationTokenSource? _serverStatusCancellation;
    private int _selectionVersion;
    private bool _initialized;
    private string? _initializedBase;
    private readonly HashSet<EAccount> _trackedAccounts = [];
    private int _avatarVersion;
    public IReadOnlyList<string> PlaytimeScopes { get; } = [DashboardText.Get("ThisInstance"), DashboardText.Get("AllEmerald")];
    public int PlaytimeScopeIndex { get => ShowAllEmeraldPlaytime ? 1 : 0; set => ShowAllEmeraldPlaytime = value == 1; }
    public PlaytimeScope CurrentPlaytimeScope => !ShowAllEmeraldPlaytime && SelectedGame != null && _core.BasePath != null
        ? PlaytimeScope.ForInstance(_core.BasePath.BasePath, SelectedGame.InstanceId) : PlaytimeScope.AllEmerald;
    public string DestinationTitle => SelectedDestination == MinecraftLaunchTargetKind.Server ? SelectedServer?.Name ?? DashboardText.Get("ChooseServer")
        : SelectedDestination == MinecraftLaunchTargetKind.World ? SelectedWorld?.DisplayName ?? DashboardText.Get("ChooseWorld") : DashboardText.Get("MainMenu");
    public string DestinationSubtitle => SelectedDestination == MinecraftLaunchTargetKind.Server ? SelectedServer?.Address ?? DashboardText.Get("BrowseServers")
        : SelectedDestination == MinecraftLaunchTargetKind.World ? DashboardText.Get("World") : DashboardText.Get("MainMenuHint");
    public string DestinationGlyph => DashboardText.Glyph(SelectedDestination);
    public string? DestinationImage => SelectedDestination == MinecraftLaunchTargetKind.World ? SelectedWorld?.IconPath : null;
    public string? DestinationServerIcon => SelectedServerStatus?.IconDataUrl ?? SelectedServer?.IconUrl;
    public string DestinationServerMetadata => SelectedServerStatus?.Version ?? string.Empty;
    public string DestinationServerStatus => IsSelectedServerStatusLoading ? DashboardText.Get("Loading") : SelectedServerStatus is { } status
        ? status.State == ServerStatusState.Online
            ? DashboardText.Format("Players", status.Players, status.MaxPlayers) + (status.LatencyMilliseconds is long latency ? $" · {latency} ms" : string.Empty)
            : DashboardText.Get(status.State.ToString())
        : string.Empty;
    public bool HasLaunchMessage => !string.IsNullOrWhiteSpace(LaunchMessage);
    public bool HasProfiles => QuickProfiles.Count > 0;
    public bool CanPlay => !IsBusy && SelectedGame?.HasActiveSession != true
        && (!HasAccount || !IsWorldDestination || SelectedWorld?.CanQuickLaunch == true);
    [ObservableProperty] private Microsoft.UI.Xaml.Media.ImageSource? _accountAvatar;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasGames))] [NotifyPropertyChangedFor(nameof(IsHeroEnabled))] private Game? _selectedGame;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasAccount))] [NotifyPropertyChangedFor(nameof(AccountName))] [NotifyPropertyChangedFor(nameof(PrimaryButtonText))] private EAccount? _selectedAccount;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsServerDestination))] [NotifyPropertyChangedFor(nameof(IsWorldDestination))] private MinecraftLaunchTargetKind _selectedDestination = MinecraftLaunchTargetKind.MainMenu;
    [ObservableProperty] private SavedServer? _selectedServer;
    [ObservableProperty] private MinecraftWorld? _selectedWorld;
    [ObservableProperty] private ServerStatusSnapshot? _selectedServerStatus;
    [ObservableProperty] private bool _isSelectedServerStatusLoading;
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
    public bool IsServerDestination => SelectedDestination == MinecraftLaunchTargetKind.Server;
    public bool IsWorldDestination => SelectedDestination == MinecraftLaunchTargetKind.World;
    public string AccountName => SelectedAccount?.Name ?? DashboardText.Get("ChooseAccount");
    public string PrimaryButtonText => !HasAccount ? DashboardText.Get("ChooseAccount") : SelectedGame?.HasActiveSession == true ? DashboardText.Get("Running")
        : IsBusy ? DashboardText.Get("Preparing") : SelectedGame?.CanLaunch != true ? DashboardText.Get("ManageInstance")
        : DashboardText.Get(IsServerDestination ? "JoinServer" : IsWorldDestination ? "PlayWorld" : "Play");
    public string InstanceDetails => SelectedGame == null ? "No instance selected" : $"{SelectedGame.Version.Type} • {SelectedGame.Version.BasedOn} • {SelectedGame.InstallationStatusText}";
    public string RuntimeStatus => SelectedGame?.StatusText ?? "Install an instance to begin";
    public MinecraftWorld? MostRecentWorld => RecentWorlds.FirstOrDefault();
    public SavedServer? FavoriteServer => FavoriteServers.FirstOrDefault();
    public string MostRecentWorldText => MostRecentWorld?.DisplayName ?? "NoLocalWorldsYet".Localize();

    public HomePageViewModel(Core core, IAccountService accounts, IGameRuntimeService runtime, IInstancePlaytimeService playtime, IHomePreferencesService preferences, ISavedServerService servers, IServerStatusService status, IMinecraftWorldService worlds, IMinecraftLaunchCapabilityResolver capabilities, IQuickProfileService profiles, INotificationService notifications, IUiDispatcher dispatcher)
    {
        _core = core;
        _dispatcher = dispatcher;
        _accounts = accounts;
        _runtime = runtime;
        _playtime = playtime;
        _preferences = preferences;
        _servers = servers;
        _status = status;
        _worlds = worlds;
        _capabilities = capabilities;
        _profiles = profiles;
        _notifications = notifications;

        _core.Games.CollectionChanged += OnSourceChanged;
        _accounts.Accounts.CollectionChanged += OnSourceChanged;
        _playtime.HistoryChanged += OnHistoryChanged;
        _servers.Changed += OnRepositoryChanged;
        _profiles.Changed += OnRepositoryChanged;
    }

    public async Task InitializeAsync()
    {
        SyncAccounts();
        SelectedAccount = _accounts.GetSelectedAccount();
        if (!_initialized || _initializedBase != _core.BasePath?.BasePath)
        {
            _initialized = true;
            _initializedBase = _core.BasePath?.BasePath;
            SelectedDestination = MinecraftLaunchTargetKind.MainMenu;
            SelectedServer = null; SelectedWorld = null;
            _homePreferences = _preferences.Load();
            ShowAllEmeraldPlaytime = _homePreferences.ShowAllEmeraldPlaytime;
            Games.ReplaceWith(_core.Games);
            SelectedGame = ResolveInitialGame();
        }
        FavoriteServers.ReplaceWith(_servers.GetAll());
        await LoadWorldsAsync();
        RefreshProfiles(); RefreshAnalytics(); RefreshRuntime(); await RefreshFavoriteStatusAsync();
    }
    private void SyncAccounts()
    {
        foreach (var account in _trackedAccounts.Where(x => !_accounts.Accounts.Contains(x)).ToArray()) { account.PropertyChanged -= AccountChanged; _trackedAccounts.Remove(account); }
        foreach (var account in _accounts.Accounts) if (_trackedAccounts.Add(account)) account.PropertyChanged += AccountChanged;
    }
    private void AccountChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_dispatcher.HasThreadAccess) { _dispatcher.Invoke(() => AccountChanged(sender, e)); return; }
        if (e.PropertyName is nameof(EAccount.IsSelected) or nameof(EAccount.Name) or nameof(EAccount.Skin))
        { SelectedAccount = _accounts.GetSelectedAccount(); OnPropertyChanged(nameof(AccountName)); _ = UpdateAvatarAsync(); }
    }
    partial void OnSelectedAccountChanged(EAccount? value) => _ = UpdateAvatarAsync();
    private async Task UpdateAvatarAsync()
    {
        var version = ++_avatarVersion; var account = SelectedAccount; AccountAvatar = null;
        if (account == null) return;
        try
        {
            var skin = account.Skin ?? await _accounts.GetSkinAsync(account);
            var image = await Controls.MinecraftSkinImageFactory.CreateHeadAsync(skin, 96);
            if (version == _avatarVersion && ReferenceEquals(account, SelectedAccount)) AccountAvatar = image;
        }
        catch { /* Match the shell's non-blocking avatar fallback. */ }
    }
    partial void OnSelectedGameChanged(Game? oldValue, Game? newValue)
    {
        ++_selectionVersion;
        if (oldValue != null) oldValue.PropertyChanged -= OnGamePropertyChanged;
        if (newValue != null) newValue.PropertyChanged += OnGamePropertyChanged;
        _homePreferences.SelectedInstanceId = newValue?.InstanceId;
        _preferences.Save(_homePreferences);
        ActiveQuickProfileId = null;
        OnPropertyChanged(nameof(InstanceDetails));
        OnPropertyChanged(nameof(RuntimeStatus));
        OnPropertyChanged(nameof(PrimaryButtonText));
        if (IsWorldDestination) { SelectedWorld = null; SelectedDestination = MinecraftLaunchTargetKind.MainMenu; }
        _ = LoadWorldsAsync();
        RefreshAnalytics();
    }

    partial void OnSelectedDestinationChanged(MinecraftLaunchTargetKind value)
    {
        ++_selectionVersion;
        ActiveQuickProfileId = null;
        LaunchMessage = null;
        SelectionChanged();
        if (value == MinecraftLaunchTargetKind.Server) _ = RefreshSelectedServerStatusAsync();
        else
        {
            _serverStatusCancellation?.Cancel();
            SelectedServerStatus = null;
            IsSelectedServerStatusLoading = false;
        }
    }

    partial void OnSelectedServerChanged(SavedServer? value)
    {
        ++_selectionVersion;
        ActiveQuickProfileId = null;
        SelectionChanged();
        if (IsServerDestination) _ = RefreshSelectedServerStatusAsync();
    }
    partial void OnSelectedWorldChanged(MinecraftWorld? value) { ++_selectionVersion; ActiveQuickProfileId = null; SelectionChanged(); }
    partial void OnSelectedServerStatusChanged(ServerStatusSnapshot? value) => SelectionChanged();
    partial void OnIsSelectedServerStatusLoadingChanged(bool value) => SelectionChanged();

    partial void OnShowAllEmeraldPlaytimeChanged(bool value)
    {
        _homePreferences.ShowAllEmeraldPlaytime = value;
        _preferences.Save(_homePreferences);
        RefreshAnalytics();
    }

    partial void OnActiveQuickProfileIdChanged(Guid? value) { foreach (var profile in QuickProfiles) profile.IsSelected = profile.Profile.Id == value; }
    partial void OnLaunchMessageChanged(string? value) => OnPropertyChanged(nameof(HasLaunchMessage));
    partial void OnIsBusyChanged(bool value) { OnPropertyChanged(nameof(CanPlay)); OnPropertyChanged(nameof(PrimaryButtonText)); }
    private void SelectionChanged()
    {
        foreach (var name in new[] { nameof(DestinationTitle), nameof(DestinationSubtitle), nameof(DestinationGlyph), nameof(DestinationImage), nameof(DestinationServerIcon), nameof(DestinationServerMetadata), nameof(DestinationServerStatus), nameof(CanPlay), nameof(PrimaryButtonText) }) OnPropertyChanged(name);
    }

    private async Task RefreshSelectedServerStatusAsync()
    {
        _serverStatusCancellation?.Cancel();
        var request = _serverStatusCancellation = new CancellationTokenSource();
        var server = SelectedServer;
        SelectedServerStatus = null;
        IsSelectedServerStatusLoading = server != null;
        if (server == null) return;
        try
        {
            var snapshot = await _status.GetStatusAsync(new MinecraftServerAddress(server.Host, server.Port), cancellationToken: request.Token);
            if (!request.IsCancellationRequested && SelectedServer?.Id == server.Id) SelectedServerStatus = snapshot;
        }
        catch (OperationCanceledException) { }
        catch
        {
            if (!request.IsCancellationRequested && SelectedServer?.Id == server.Id)
                SelectedServerStatus = new(ServerStatusState.Unavailable, DateTimeOffset.UtcNow, new(server.Host, server.Port));
        }
        finally
        {
            if (ReferenceEquals(_serverStatusCancellation, request)) IsSelectedServerStatusLoading = false;
        }
    }
    [RelayCommand]
    private async Task LaunchAsync()
    {
        var game = SelectedGame; var account = _accounts.GetSelectedAccount(); var kind = SelectedDestination;
        var server = SelectedServer; var world = SelectedWorld; var profile = ActiveQuickProfileId;
        if (IsBusy || game == null) return;
        if (account == null) { LaunchMessage = DashboardText.Get("ChooseAccount"); return; }
        if (!game.CanLaunch) { LaunchMessage = DashboardText.Get("InstanceUnavailable"); return; }
        IsBusy = true; LaunchMessage = null;
        try
        {
            var target = MinecraftLaunchTarget.MainMenu;
            if (kind == MinecraftLaunchTargetKind.Server)
            {
                if (server == null) { LaunchMessage = DashboardText.Get("ChooseServer"); return; }
                // Availability is advisory; launch the user's address without waiting on status.
                target = MinecraftLaunchTarget.ForServer(server.Host, server.Port, server.Name);
            }
            else if (kind == MinecraftLaunchTargetKind.World)
            {
                if (world == null || !world.IsReadable) { LaunchMessage = DashboardText.Get("ChooseWorld"); return; }
                var caps = await _capabilities.ResolveAsync(game);
                if (!caps.CanQuickPlayWorld) { LaunchMessage = caps.WorldLaunchUnavailableReason; return; }
                target = MinecraftLaunchTarget.ForWorld(world.FolderName, world.DisplayName);
            }
            var result = await _runtime.LaunchAsync(new GameLaunchRequest(game, account, target, profile));
            if (result != null && server != null && kind == MinecraftLaunchTargetKind.Server) _servers.MarkLaunched(server.Id);
        }
        catch (Exception ex) { LaunchMessage = ex.Message; }
        finally { IsBusy = false; RefreshRuntime(); }
    }
    [RelayCommand]
    private Task StopAsync() => SelectedGame == null
        ? Task.CompletedTask
        : _runtime.StopAsync(SelectedGame, GameStopMode.Gentle);

    [RelayCommand]
    private Task ForceStopAsync() => SelectedGame == null
        ? Task.CompletedTask
        : _runtime.StopAsync(SelectedGame, GameStopMode.Force);

    public async Task ApplySelectionAsync(HomeSelection selection)
    {
        var version = ++_selectionVersion;
        var world = selection.World;
        if (selection.Kind == MinecraftLaunchTargetKind.World)
        {
            var list = await _worlds.ScanAsync(selection.Game);
            world = list.FirstOrDefault(x => x.FolderName == selection.World?.FolderName);
            if (world == null) { LaunchMessage = DashboardText.Get("WorldMissing"); return; }
            var caps = await _capabilities.ResolveAsync(selection.Game);
            world.CanQuickLaunch = world.IsReadable && selection.Game.CanLaunch && caps.CanQuickPlayWorld;
            if (version != _selectionVersion) return;
        }
        if (version != _selectionVersion) return;
        SelectedGame = selection.Game;
        if (selection.Account != null) { _accounts.SetSelectedAccount(selection.Account); SelectedAccount = selection.Account; }
        SelectedDestination = selection.Kind; SelectedServer = selection.Server; SelectedWorld = world;
        ActiveQuickProfileId = selection.ProfileId;
        if (world != null && !world.CanQuickLaunch) LaunchMessage = DashboardText.Get("WorldUnavailable");
        SelectionChanged();
    }
    public async Task SelectProfileAsync(QuickProfile profile, bool launch)
    {
        var validation = _profiles.Validate(profile, Games, _accounts.Accounts, FavoriteServers);
        if (!validation.IsValid) { LaunchMessage = DashboardText.Get("ProfileRepair"); return; }
        var game = Games.First(x => x.InstanceId == profile.InstanceId);
        var account = _accounts.Accounts.First(x => x.UniqueId == profile.AccountUniqueId);
        var world = profile.TargetKind == MinecraftLaunchTargetKind.World ? new MinecraftWorld { FolderName = profile.WorldFolderName! } : null;
        await ApplySelectionAsync(new(game, profile.TargetKind, FavoriteServers.FirstOrDefault(x => x.Id == profile.SavedServerId), world, account, profile.Id));
        if (launch && ActiveQuickProfileId == profile.Id) await LaunchAsync();
    }
    public void RefreshRuntime()
    {
        if (SelectedGame == null)
        {
            return;
        }

        var session = _runtime.TryGetActiveSession(SelectedGame);
        RuntimeElapsedText = session?.IsActive == true ? FormatDuration(DateTimeOffset.Now - session.StartedAt) : string.Empty;
        OnPropertyChanged(nameof(RuntimeStatus));
        OnPropertyChanged(nameof(PrimaryButtonText));
        OnPropertyChanged(nameof(CanPlay));
    }

    private Game? ResolveInitialGame()
    {
        var persisted = Games.FirstOrDefault(x => x.InstanceId == _homePreferences.SelectedInstanceId);
        if (persisted != null)
        {
            return persisted;
        }

        var basePath = _core.BasePath?.BasePath;
        if (basePath != null)
        {
            var recent = _playtime.GetSessions(PlaytimeScope.ForBase(basePath)).FirstOrDefault();
            var recentGame = Games.FirstOrDefault(x => x.InstanceId == recent?.InstanceId);
            if (recentGame != null)
            {
                return recentGame;
            }
        }

        return Games.FirstOrDefault(x => x.CanLaunch) ?? Games.FirstOrDefault();
    }

    private async Task LoadWorldsAsync()
    {
        _worldCancellation?.Cancel(); var cancellation = _worldCancellation = new(); var game = SelectedGame;
        if (game == null) { RecentWorlds.Clear(); return; }
        try
        {
            var worlds = await _worlds.ScanAsync(game, cancellation.Token);
            var caps = await _capabilities.ResolveAsync(game, cancellation.Token);
            if (cancellation.IsCancellationRequested || SelectedGame != game) return;
            foreach (var world in worlds) world.CanQuickLaunch = world.IsReadable && game.CanLaunch && caps.CanQuickPlayWorld;
            RecentWorlds.ReplaceWith(worlds.Take(8));
            if (IsWorldDestination && SelectedWorld != null)
            {
                var replacement = worlds.FirstOrDefault(x => x.FolderName == SelectedWorld.FolderName);
                var active = ActiveQuickProfileId; SelectedWorld = replacement; ActiveQuickProfileId = active;
            }
            OnPropertyChanged(nameof(MostRecentWorld)); OnPropertyChanged(nameof(MostRecentWorldText)); SelectionChanged();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { LaunchMessage = ex.Message; }
    }
    public void RefreshAnalytics()
    {
        var now = DateTimeOffset.Now; var active = DashboardText.Active(_core, _runtime, now);
        var total = _playtime.GetAnalytics(CurrentPlaytimeScope, PlaytimeRange.AllTime, now, activeSessions: active);
        var week = _playtime.GetAnalytics(CurrentPlaytimeScope, PlaytimeRange.SevenDays, now, activeSessions: active);
        TotalPlaytimeText = DashboardText.Duration(total.TotalPlaytime);
        WeekPlaytimeText = DashboardText.Duration(week.CurrentWeekPlaytime);
        SessionCountText = DashboardText.Format("SessionCount", total.CompletedSessionCount);
        HomeDailyBars.ReplaceWith(DashboardText.Bars(week.DailyBuckets, 7, 88));
    }
    private void RefreshProfiles()
    {
        QuickProfiles.ReplaceWith(_profiles.GetAll().Select(x =>
            new QuickProfileCardViewModel(x, _profiles.Validate(x, Games, _accounts.Accounts, FavoriteServers), Games.FirstOrDefault(g => g.InstanceId == x.InstanceId)?.Version.DisplayName, _accounts.Accounts.FirstOrDefault(a => a.UniqueId == x.AccountUniqueId)?.Name) { IsSelected = x.Id == ActiveQuickProfileId }));
        OnPropertyChanged(nameof(HasProfiles));
    }

    public void ReloadProfiles() { RefreshProfiles(); OnPropertyChanged(nameof(HasProfiles)); }
    private void OnHistoryChanged(object? sender, EventArgs e) => _dispatcher.Invoke(RefreshAnalytics);
    private void OnRepositoryChanged(object? sender, EventArgs e)
    {
        if (!_dispatcher.HasThreadAccess) { _dispatcher.Invoke(() => OnRepositoryChanged(sender, e)); return; }
        var active = ActiveQuickProfileId;
        var selectedServerId = SelectedServer?.Id;
        FavoriteServers.ReplaceWith(_servers.GetAll());
        if (selectedServerId is Guid id)
        {
            SelectedServer = FavoriteServers.FirstOrDefault(x => x.Id == id);
        }

        ActiveQuickProfileId = active;
        _ = RefreshFavoriteStatusAsync();
        RefreshProfiles();
    }

    private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!_dispatcher.HasThreadAccess) { _dispatcher.Invoke(() => OnSourceChanged(sender, e)); return; }
        SyncAccounts();
        Games.ReplaceWith(_core.Games);
        SelectedAccount = _accounts.GetSelectedAccount();
        if (SelectedGame == null || !Games.Contains(SelectedGame))
        {
            SelectedGame = ResolveInitialGame();
        }

        RefreshProfiles();
    }

    private void OnGamePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(RuntimeStatus));
        OnPropertyChanged(nameof(PrimaryButtonText));
        OnPropertyChanged(nameof(CanPlay));
    }

    private Task RefreshFavoriteStatusAsync()
    {
        FavoriteServerStatus = DashboardText.Format("FavoriteCount", FavoriteServers.Count);
        return Task.CompletedTask;
    }

    private static string FormatDuration(TimeSpan value) => DashboardText.Duration(value);
}

internal static class ObservableCollectionHomeExtensions
{
    public static void ReplaceWith<T>(this ObservableCollection<T> target, IEnumerable<T> values)
    {
        target.Clear();
        foreach (var value in values)
        {
            target.Add(value);
        }
    }
}
