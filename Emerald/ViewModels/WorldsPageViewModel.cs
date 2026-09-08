using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Emerald.CoreX;
using Emerald.CoreX.Runtime;
using Emerald.CoreX.Services;
using Emerald.CoreX.Services.Worlds;
namespace Emerald.ViewModels;
public sealed class WorldRowViewModel(MinecraftWorld world)
{
 public MinecraftWorld World { get; } = world;
 public string Name => World.DisplayName;
 public string? IconPath => World.IconPath;
 public string Metadata => string.Join(" · ", new[] { DashboardText.Get(World.GameMode.ToString()), World.MinecraftVersion, DashboardText.Relative(World.LastPlayed) }.Where(x => !string.IsNullOrWhiteSpace(x)));
 public bool HasWarning => !string.IsNullOrWhiteSpace(World.Warning);
 public string Warning => DashboardText.Get("WorldWarning");
 public bool CanLaunch => World.CanQuickLaunch;
}
public partial class WorldsPageViewModel(Core core, IAccountService accounts, IMinecraftWorldService worlds, IMinecraftLaunchCapabilityResolver capabilities, IGameRuntimeService runtime) : ObservableObject
{
 private CancellationTokenSource? _load;
 private bool _initializing;
 public ObservableCollection<Game> Games { get; } = [];
 public ObservableCollection<MinecraftWorld> Worlds { get; } = [];
 public ObservableCollection<WorldRowViewModel> FilteredWorlds { get; } = [];
 public IReadOnlyList<Choice<int>> Sorts { get; } = [new(0, DashboardText.Get("LastPlayed")), new(1, DashboardText.Get("Name")), new(2, DashboardText.Get("Size")), new(3, DashboardText.Get("GameMode"))];
 [ObservableProperty] private Game? _selectedGame;
 [ObservableProperty] private string _searchText = string.Empty;
 [ObservableProperty] private Choice<int>? _selectedSort;
 [ObservableProperty] private bool _isLoading;
 [ObservableProperty] private string? _launchLimitation;
 [ObservableProperty] private string? _errorMessage;
 public bool HasLimitation => !string.IsNullOrWhiteSpace(LaunchLimitation);
 public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
 public bool IsEmpty => !IsLoading && !HasError && FilteredWorlds.Count == 0;
 public string EmptyText => DashboardText.Get(string.IsNullOrWhiteSpace(SearchText) ? "NoWorlds" : "NoMatches");
 public async Task InitializeAsync(Game? preferred = null)
 {
  _initializing = true; Games.ReplaceWith(core.Games); SelectedGame = preferred ?? Games.FirstOrDefault(); SelectedSort = Sorts[0]; _initializing = false;
  await RefreshAsync();
 }
 partial void OnSelectedGameChanged(Game? value) { if (!_initializing) _ = RefreshAsync(); }
 partial void OnSearchTextChanged(string value) => ApplyFilter();
 partial void OnSelectedSortChanged(Choice<int>? value) { if (!_initializing) { if (value?.Value == 2) _ = RefreshAsync(); else ApplyFilter(); } }
 partial void OnLaunchLimitationChanged(string? value) => OnPropertyChanged(nameof(HasLimitation));
 partial void OnErrorMessageChanged(string? value) { OnPropertyChanged(nameof(HasError)); OnPropertyChanged(nameof(IsEmpty)); }
 partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsEmpty));
 public void Cancel() => _load?.Cancel();
 public async Task RefreshAsync(CancellationToken cancellationToken = default)
 {
  _load?.Cancel(); var load = _load = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); var game = SelectedGame;
  ErrorMessage = null; LaunchLimitation = null; if (game == null) { Worlds.Clear(); ApplyFilter(); return; }
  IsLoading = true;
  try
  {
   var result = await worlds.ScanAsync(game, load.Token); var caps = await capabilities.ResolveAsync(game, load.Token);
   if (load.IsCancellationRequested || SelectedGame != game) return;
   foreach (var world in result) world.CanQuickLaunch = world.IsReadable && game.CanLaunch && caps.CanQuickPlayWorld;
   if (SelectedSort?.Value == 2) await Task.WhenAll(result.Select(x => worlds.CalculateSizeAsync(x, load.Token)));
   if (load.IsCancellationRequested || SelectedGame != game) return;
   LaunchLimitation = caps.WorldLaunchUnavailableReason; Worlds.ReplaceWith(result); ApplyFilter();
  }
  catch (OperationCanceledException) { }
  catch (Exception ex) { if (ReferenceEquals(load, _load)) ErrorMessage = ex.Message; }
  finally { if (ReferenceEquals(load, _load)) IsLoading = false; }
 }
 public async Task<bool> LaunchAsync(MinecraftWorld world)
 {
  var game = SelectedGame; var account = accounts.GetSelectedAccount();
  if (account == null) { ErrorMessage = DashboardText.Get("ChooseAccount"); return false; }
  if (game == null || !game.CanLaunch || !world.IsReadable) { ErrorMessage = DashboardText.Get("WorldUnavailable"); return false; }
  try { var caps = await capabilities.ResolveAsync(game); if (!caps.CanQuickPlayWorld) { LaunchLimitation = caps.WorldLaunchUnavailableReason; return false; }
   await runtime.LaunchAsync(new GameLaunchRequest(game, account, MinecraftLaunchTarget.ForWorld(world.FolderName, world.DisplayName))); return true; }
  catch (Exception ex) { ErrorMessage = ex.Message; return false; }
 }
 private void ApplyFilter()
 {
  IEnumerable<MinecraftWorld> query = Worlds.Where(x => string.IsNullOrWhiteSpace(SearchText) || x.DisplayName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) || x.FolderName.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
  query = SelectedSort?.Value switch { 1 => query.OrderBy(x => x.DisplayName), 2 => query.OrderByDescending(x => x.SizeBytes), 3 => query.OrderBy(x => x.GameMode), _ => query.OrderByDescending(x => x.LastPlayed) };
  FilteredWorlds.ReplaceWith(query.Select(x => new WorldRowViewModel(x))); OnPropertyChanged(nameof(IsEmpty)); OnPropertyChanged(nameof(EmptyText));
 }
}
