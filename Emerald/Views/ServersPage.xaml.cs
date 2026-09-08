using CommunityToolkit.Mvvm.DependencyInjection;
using Emerald.Controls;
using Emerald.CoreX.Runtime;
using Emerald.CoreX.Services.Servers;
using Emerald.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Windows.ApplicationModel.DataTransfer;
namespace Emerald.Views;
public sealed partial class ServersPage : Page
{
 public ServersPageViewModel ViewModel { get; } = Ioc.Default.GetRequiredService<ServersPageViewModel>();
 public ServersPage() => InitializeComponent();
 protected override async void OnNavigatedTo(NavigationEventArgs e) { base.OnNavigatedTo(e); await ViewModel.InitializeAsync(e.Parameter as CoreX.Game); }
 protected override void OnNavigatedFrom(NavigationEventArgs e) { ViewModel.Cancel(); base.OnNavigatedFrom(e); }
 private void Back_Click(object sender, RoutedEventArgs e) { if (Frame.CanGoBack) Frame.GoBack(); else Frame.Navigate(typeof(HomePage)); }
 private async void Select_Row(object? sender, EventArgs e)
 {
  if (sender is ServerRow { Model: { } row } && ViewModel.SelectedGame is { } game)
  { await Ioc.Default.GetRequiredService<HomePageViewModel>().ApplySelectionAsync(new(game, MinecraftLaunchTargetKind.Server, ViewModel.EnsureSaved(row))); Back_Click(this, new()); }
 }
 private async void Play_Row(object? sender, EventArgs e) { if (sender is ServerRow { Model: { } row }) await ViewModel.LaunchAsync(row); }
 private async void Favorite_Row(object? sender, EventArgs e)
 {
  if (sender is not ServerRow { Model: { } row }) return;
  if (row.IsFavorite && await new ContentDialog { XamlRoot = XamlRoot, Title = DashboardText.Get("RemoveFavorite"), Content = DashboardText.Get("RemoveFavoriteHint"), PrimaryButtonText = DashboardText.Get("Remove"), CloseButtonText = DashboardText.Get("Cancel"), DefaultButton = ContentDialogButton.Close }.ShowAsync() != ContentDialogResult.Primary) return;
  ViewModel.ToggleFavorite(row);
 }
 private void Options_Row(object? sender, EventArgs e)
 {
  if (sender is not ServerRow { Model: { } row } anchor) return;
  var menu = new MenuFlyout();
  void Add(string key, Action action) { var item = new MenuFlyoutItem { Text = DashboardText.Get(key) }; item.Click += (_, _) => action(); menu.Items.Add(item); }
  Add("CopyAddress", () => { var package = new DataPackage(); package.SetText(row.Address); Clipboard.SetContent(package); });
  if (row.Saved != null) Add("RefreshStatus", async () => await ViewModel.RefreshStatusAsync(true));
  if (row.Saved?.SourceKind == SavedServerSourceKind.Custom) Add("Edit", async () => await ShowCustomAsync(row.Saved));
  var source = row.Saved?.SourcePageUrl ?? row.Directory?.PageUrl;
  if (Uri.TryCreate(source, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https") Add("SourcePage", async () => await Windows.System.Launcher.LaunchUriAsync(uri));
  menu.ShowAt(anchor);
 }
 private void Discover_Click(object sender, RoutedEventArgs e) { ViewModel.TabIndex = 0; ((Microsoft.UI.Xaml.Controls.Primitives.ToggleButton)sender).IsChecked = true; }
 private void Favorites_Click(object sender, RoutedEventArgs e) { ViewModel.TabIndex = 1; ((Microsoft.UI.Xaml.Controls.Primitives.ToggleButton)sender).IsChecked = true; }
 private async void Refresh_Click(object sender, RoutedEventArgs e) { if (ViewModel.IsDiscover) await ViewModel.SearchAsync(); else await ViewModel.RefreshStatusAsync(true); }
 private void ClearFilters_Click(object sender, RoutedEventArgs e) => ViewModel.ClearFilters();
 private async void Previous_Click(object sender, RoutedEventArgs e) => await ViewModel.PageAsync(-1);
 private async void Next_Click(object sender, RoutedEventArgs e) => await ViewModel.PageAsync(1);
 private async void AddCustom_Click(object sender, RoutedEventArgs e) => await ShowCustomAsync(null);
 private async Task ShowCustomAsync(SavedServer? original)
 {
  var name = new TextBox { Header = DashboardText.Get("Name"), Text = original?.Name ?? string.Empty };
  var address = new TextBox { Header = DashboardText.Get("Address"), Text = original?.Address ?? string.Empty, PlaceholderText = "play.example.net:25565" };
  var error = new TextBlock { TextWrapping = TextWrapping.Wrap };
  var panel = new StackPanel { Spacing = 12 }; panel.Children.Add(name); panel.Children.Add(address); panel.Children.Add(error);
  var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = DashboardText.Get(original == null ? "AddServer" : "EditServer"), Content = panel, PrimaryButtonText = DashboardText.Get("Save"), CloseButtonText = DashboardText.Get("Cancel"), DefaultButton = ContentDialogButton.Primary };
  dialog.PrimaryButtonClick += (_, args) => { if (!MinecraftServerAddressParser.TryParse(address.Text, out var _, out var message)) { args.Cancel = true; error.Text = message; } };
  if (await dialog.ShowAsync() == ContentDialogResult.Primary) { ViewModel.SaveCustom(name.Text, address.Text, original); if (ViewModel.IsFavorites) await ViewModel.RefreshStatusAsync(); }
 }
 private void Layout_SizeChanged(object sender, SizeChangedEventArgs e)
 {
  var compact = e.NewSize.Width < 640; LayoutRoot.Padding = new Thickness(compact ? 16 : 24);
  Grid.SetRow(InstancePicker, compact ? 1 : 0); Grid.SetColumn(InstancePicker, compact ? 0 : 3); Grid.SetColumnSpan(InstancePicker, compact ? 3 : 1);
  Filters.ColumnDefinitions[3].Width = compact ? new GridLength(0) : new GridLength(220);
 }
}
