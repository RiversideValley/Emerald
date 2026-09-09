using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Emerald.CoreX;
using Emerald.CoreX.Runtime;
using Emerald.CoreX.Services;
using Emerald.CoreX.Services.Servers;
namespace Emerald.ViewModels;

public partial class ServerRowViewModel : ObservableObject
{
    public ServerDirectoryEntry? Directory { get; }
    public SavedServer? Saved { get; }
    public string Name => Saved?.Name ?? Directory!.Name;
    public string Address => Saved?.Address ?? Directory!.Address;
    public string? Icon => Snapshot?.IconDataUrl ?? Saved?.IconUrl ?? Directory?.IconUrl;
    public string Metadata => string.Join(" · ", new[] { Address, Snapshot?.Version ?? Directory?.Version, Directory?.TagsText }.Where(x => !string.IsNullOrWhiteSpace(x)));
    public string? Motd => Snapshot?.Motd ?? Directory?.Motd;
    [ObservableProperty] private bool _isFavorite;
    [ObservableProperty] private ServerStatusSnapshot? _snapshot;
    public string FavoriteGlyph => IsFavorite ? "\uE735" : "\uE734";
    public string Status => Snapshot is { } s ? s.State == ServerStatusState.Online ? DashboardText.Format("Players", s.Players, s.MaxPlayers) + (s.LatencyMilliseconds is long ms ? $" · {ms} ms" : string.Empty)
      : DashboardText.Get(s.State.ToString()) : Directory is { } d ? d.Online ? DashboardText.Format("Players", d.Players, d.MaxPlayers) : DashboardText.Get("Offline") : DashboardText.Get("Loading");
    public string StatusHelp => Snapshot is { } s ? $"{DashboardText.Get(s.State.ToString())} · {s.CheckedAt.ToLocalTime():g}" : DashboardText.Get("DirectoryStatus");
    public ServerRowViewModel(ServerDirectoryEntry entry, bool favorite) { Directory = entry; IsFavorite = favorite; }
    public ServerRowViewModel(SavedServer saved) { Saved = saved; IsFavorite = true; }
    partial void OnIsFavoriteChanged(bool value) => OnPropertyChanged(nameof(FavoriteGlyph));
    partial void OnSnapshotChanged(ServerStatusSnapshot? value) { foreach (var key in new[] { nameof(Icon), nameof(Status), nameof(StatusHelp), nameof(Motd), nameof(Metadata) }) OnPropertyChanged(key); }
}
public partial class ServersPageViewModel(Core core, IAccountService accounts, IServerDirectoryService directory, ISavedServerService saved, IServerStatusService status, IGameRuntimeService runtime) : ObservableObject
{
    private CancellationTokenSource? _search;
    private CancellationTokenSource? _statusLoad;
    private bool _initializing;
    private bool _active;
    private int _page = 1;
    public ObservableCollection<Game> Games { get; } = [];
    public ObservableCollection<ServerRowViewModel> DiscoverServers { get; } = [];
    public ObservableCollection<ServerRowViewModel> FavoriteServers { get; } = [];
    public IReadOnlyList<Choice<ServerDirectorySort>> Sorts { get; } = Enum.GetValues<ServerDirectorySort>().Select(x => new Choice<ServerDirectorySort>(x, DashboardText.Get(x == ServerDirectorySort.Players ? "SortPlayers" : x.ToString()))).ToArray();
    public IReadOnlyList<Choice<int>> FavoriteSorts { get; } = [new(0, DashboardText.Get("Recent")), new(1, DashboardText.Get("Name"))];
    [ObservableProperty] private Game? _selectedGame;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _versionFilter = string.Empty;
    [ObservableProperty] private bool _useInstanceVersion = true;
    [ObservableProperty] private string _tagFilter = string.Empty;
    [ObservableProperty] private Choice<ServerDirectorySort>? _selectedSort;
    [ObservableProperty] private Choice<int>? _favoriteSort;
    [ObservableProperty] private int _tabIndex;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _canNext;
    [ObservableProperty] private string? _errorMessage;
    public bool IsDiscover => TabIndex == 0;
    public bool IsFavorites => !IsDiscover;
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsEmpty => !IsLoading && !HasError && (IsDiscover ? DiscoverServers.Count : FavoriteServers.Count) == 0;
    public bool CanPrevious => _page > 1 && !IsLoading;
    public string PageText => DashboardText.Format("Page", _page);
    public string FilterText => DashboardText.Format("FiltersCount", (string.IsNullOrWhiteSpace(TagFilter) ? 0 : 1) + (string.IsNullOrWhiteSpace(EffectiveVersion) ? 0 : 1));
    private string? EffectiveVersion => UseInstanceVersion ? SelectedGame?.Version.BasedOn : VersionFilter;
    public async Task InitializeAsync(Game? preferred = null)
    {
        _initializing = true; _active = true; Games.ReplaceWith(core.Games); SelectedGame = preferred ?? Games.FirstOrDefault(x => x.CanLaunch) ?? Games.FirstOrDefault(); SelectedSort = Sorts[0]; FavoriteSort = FavoriteSorts[0]; _initializing = false;
        RefreshFavorites(); await SearchAsync();
    }
    public void Cancel() { _active = false; _search?.Cancel(); _statusLoad?.Cancel(); }
    partial void OnSelectedGameChanged(Game? value) { if (UseInstanceVersion) Schedule(false); }
    partial void OnSearchTextChanged(string value) { if (IsDiscover) Schedule(true); else RefreshFavorites(); }
    partial void OnVersionFilterChanged(string value) => Schedule(true);
    partial void OnTagFilterChanged(string value) => Schedule(true);
    partial void OnUseInstanceVersionChanged(bool value) => Schedule(false);
    partial void OnSelectedSortChanged(Choice<ServerDirectorySort>? value) => Schedule(false);
    partial void OnFavoriteSortChanged(Choice<int>? value) { if (!_initializing) RefreshFavorites(); }
    partial void OnTabIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsDiscover)); OnPropertyChanged(nameof(IsFavorites));
        _search?.Cancel(); _statusLoad?.Cancel(); IsLoading = false; ErrorMessage = null;
        if (!_active || _initializing) return;
        if (IsDiscover) Schedule(false); else { RefreshFavorites(); _ = RefreshStatusAsync(); }
    }
    partial void OnErrorMessageChanged(string? value) { OnPropertyChanged(nameof(HasError)); OnPropertyChanged(nameof(IsEmpty)); }
    partial void OnIsLoadingChanged(bool value) { OnPropertyChanged(nameof(IsEmpty)); OnPropertyChanged(nameof(CanPrevious)); }
    private void Schedule(bool debounce)
    {
        OnPropertyChanged(nameof(FilterText)); if (_initializing || !_active || !IsDiscover) return;
        _page = 1; _ = SearchAsync(debounce);
    }
    public void ClearFilters() { _initializing = true; TagFilter = string.Empty; VersionFilter = string.Empty; UseInstanceVersion = false; _initializing = false; Schedule(false); }
    public async Task SearchAsync(bool debounce = false)
    {
        _search?.Cancel(); var request = _search = new(); var token = request.Token;
        IsLoading = true; ErrorMessage = null; CanNext = false;
        var query = new ServerDirectoryQuery(SearchText, TagFilter, EffectiveVersion, SelectedSort?.Value ?? ServerDirectorySort.Votes, _page, 20);
        try
        {
            if (debounce) await Task.Delay(350, token);
            var result = await directory.SearchAsync(query, token);
            if (token.IsCancellationRequested || !_active || !IsDiscover) return;
            foreach (var entry in result.Servers) EnrichSavedServer(entry);
            var favorites = saved.GetAll().Select(x => new MinecraftServerAddress(x.Host, x.Port).CanonicalKey).ToHashSet();
            DiscoverServers.ReplaceWith(result.Servers.Select(x => new ServerRowViewModel(x, favorites.Contains(new MinecraftServerAddress(x.Host, x.Port).CanonicalKey))));
            CanNext = result.Total.HasValue ? _page * result.PageSize < result.Total : result.Servers.Count >= result.PageSize;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (ReferenceEquals(_search, request)) ErrorMessage = ex.Message; }
        finally { if (ReferenceEquals(_search, request)) { IsLoading = false; OnPropertyChanged(nameof(PageText)); OnPropertyChanged(nameof(CanPrevious)); } }
    }
    public async Task PageAsync(int delta) { if (IsLoading || delta < 0 && !CanPrevious || delta > 0 && !CanNext) return; _page = Math.Max(1, _page + delta); await SearchAsync(); }
    public void RefreshFavorites()
    {
        var snapshots = FavoriteServers.Where(x => x.Saved != null).ToDictionary(x => x.Saved!.Id, x => x.Snapshot);
        IEnumerable<SavedServer> rows = saved.GetAll();
        if (IsFavorites && !string.IsNullOrWhiteSpace(SearchText)) rows = rows.Where(x => x.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) || x.Address.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        if (FavoriteSort?.Value == 1) rows = rows.OrderBy(x => x.Name);
        FavoriteServers.ReplaceWith(rows.Select(x => new ServerRowViewModel(x) { Snapshot = snapshots.GetValueOrDefault(x.Id) }));
        OnPropertyChanged(nameof(IsEmpty));
    }
    public async Task RefreshStatusAsync(bool force = false)
    {
        _statusLoad?.Cancel(); var load = _statusLoad = new();
        await Task.WhenAll(FavoriteServers.ToArray().Select(async row =>
        {
            try { var result = await status.GetStatusAsync(new(row.Saved!.Host, row.Saved.Port), force, load.Token); if (!load.IsCancellationRequested) row.Snapshot = result; }
            catch (OperationCanceledException) { }
            catch (Exception) { if (!load.IsCancellationRequested) row.Snapshot = new(ServerStatusState.Unavailable, DateTimeOffset.Now, new(row.Saved!.Host, row.Saved.Port)); }
        }));
    }
    public SavedServer SaveCustom(string name, string address, SavedServer? original = null)
    {
        var parsed = MinecraftServerAddressParser.Parse(address);
        var result = saved.Save(new SavedServer { Id = original?.Id ?? Guid.NewGuid(), Name = string.IsNullOrWhiteSpace(name) ? parsed.Host : name.Trim(), Host = parsed.Host, Port = parsed.Port, SourceKind = SavedServerSourceKind.Custom, DateAdded = original?.DateAdded ?? DateTimeOffset.UtcNow, LastLaunchedAt = original?.LastLaunchedAt });
        RefreshFavorites(); return result;
    }
    public SavedServer EnsureSaved(ServerRowViewModel row)
    {
        if (row.Saved != null) return row.Saved;
        var entry = row.Directory!; var existing = saved.GetAll().FirstOrDefault(x => new MinecraftServerAddress(x.Host, x.Port).CanonicalKey == new MinecraftServerAddress(entry.Host, entry.Port).CanonicalKey);
        if (existing != null) { EnrichSavedServer(entry, existing); return saved.Find(existing.Id) ?? existing; }
        return saved.Save(new SavedServer { Name = entry.Name, Host = entry.Host, Port = entry.Port, SourceKind = SavedServerSourceKind.Directory, SourceSlug = entry.Slug, SourcePageUrl = entry.PageUrl, IconUrl = entry.IconUrl, BannerUrl = entry.BannerUrl });
    }
    private void EnrichSavedServer(ServerDirectoryEntry entry, SavedServer? existing = null)
    {
        existing ??= saved.GetAll().FirstOrDefault(x => new MinecraftServerAddress(x.Host, x.Port).CanonicalKey == new MinecraftServerAddress(entry.Host, entry.Port).CanonicalKey);
        if (existing?.SourceKind != SavedServerSourceKind.Directory) return;
        var changed = false;
        if (string.IsNullOrWhiteSpace(existing.IconUrl) && !string.IsNullOrWhiteSpace(entry.IconUrl)) { existing.IconUrl = entry.IconUrl; changed = true; }
        if (string.IsNullOrWhiteSpace(existing.BannerUrl) && !string.IsNullOrWhiteSpace(entry.BannerUrl)) { existing.BannerUrl = entry.BannerUrl; changed = true; }
        if (string.IsNullOrWhiteSpace(existing.SourceSlug) && !string.IsNullOrWhiteSpace(entry.Slug)) { existing.SourceSlug = entry.Slug; changed = true; }
        if (string.IsNullOrWhiteSpace(existing.SourcePageUrl) && !string.IsNullOrWhiteSpace(entry.PageUrl)) { existing.SourcePageUrl = entry.PageUrl; changed = true; }
        if (changed) saved.Save(existing);
    }
    public void ToggleFavorite(ServerRowViewModel row)
    {
        if (row.IsFavorite) { var match = row.Saved ?? saved.GetAll().FirstOrDefault(x => x.Address == row.Address); if (match != null) saved.Remove(match.Id); row.IsFavorite = false; }
        else { EnsureSaved(row); row.IsFavorite = true; }
        RefreshFavorites();
    }
    public async Task LaunchAsync(ServerRowViewModel row)
    {
        var game = SelectedGame; var account = accounts.GetSelectedAccount();
        if (account == null) { ErrorMessage = DashboardText.Get("ChooseAccount"); return; }
        if (game?.CanLaunch != true) { ErrorMessage = DashboardText.Get("InstanceUnavailable"); return; }
        try { var server = EnsureSaved(row); var result = await runtime.LaunchAsync(new GameLaunchRequest(game, account, MinecraftLaunchTarget.ForServer(server.Host, server.Port, server.Name))); if (result != null) saved.MarkLaunched(server.Id); }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }
}
