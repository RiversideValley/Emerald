using CommunityToolkit.Mvvm.DependencyInjection;
using Emerald.CoreX.Helpers;
using Emerald.CoreX.Services.Worlds;
using Emerald.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Windows.ApplicationModel.DataTransfer;
namespace Emerald.Views;
public sealed partial class WorldsPage : Page
{
    private CancellationTokenSource? _loadCancellation;
    public WorldsPageViewModel ViewModel { get; } = Ioc.Default.GetRequiredService<WorldsPageViewModel>();
    public WorldsPage() => InitializeComponent();
    protected override async void OnNavigatedTo(NavigationEventArgs e) { base.OnNavigatedTo(e); _loadCancellation = new(); await ViewModel.InitializeAsync(e.Parameter as CoreX.Game); }
    protected override void OnNavigatedFrom(NavigationEventArgs e) { _loadCancellation?.Cancel(); base.OnNavigatedFrom(e); }
    private void Back_Click(object sender, RoutedEventArgs e) { if (Frame.CanGoBack) Frame.GoBack(); else Frame.Navigate(typeof(HomePage)); }
    private async void Refresh_Click(object sender, RoutedEventArgs e) { _loadCancellation?.Cancel(); _loadCancellation = new(); await ViewModel.RefreshAsync(_loadCancellation.Token); }
    private void Reveal_Click(object sender, RoutedEventArgs e) { if (sender is FrameworkElement { Tag: MinecraftWorld world }) FileManager.Reveal(world.FolderPath); }
    private async void Launch_Click(object sender, RoutedEventArgs e) { if (sender is FrameworkElement { Tag: MinecraftWorld world }) await ViewModel.LaunchAsync(world); }
    private async void Details_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: MinecraftWorld world }) return;
        var seed = world.Seed?.ToString() ?? "Unavailable"; var copy = new Button { Content = "Copy seed", IsEnabled = world.Seed.HasValue, HorizontalAlignment = HorizontalAlignment.Left }; copy.Click += (_, _) => { var data = new DataPackage(); data.SetText(seed); Clipboard.SetContent(data); };
        var select = new Button { Content = "Select for Home", HorizontalAlignment = HorizontalAlignment.Left }; select.Click += (_, _) => { var home = Ioc.Default.GetRequiredService<HomePageViewModel>(); home.SelectedGame = ViewModel.SelectedGame; home.SelectedDestination = "World"; home.SelectedWorld = world; Frame.Navigate(typeof(HomePage)); };
        var panel = new StackPanel { Spacing = 7 }; panel.Children.Add(new TextBlock { Text = $"Folder: {world.FolderName}\nLast played: {world.LastPlayed?.ToLocalTime():g}\nMode: {world.GameMode}\nDifficulty: {world.Difficulty}\nHardcore: {world.Hardcore}\nCheats: {world.CheatsEnabled}\nData version: {world.DataVersion}\nMinecraft: {world.MinecraftVersion}\nSeed: {seed}\nMetadata: {(world.UsedBackupMetadata ? "level.dat_old" : "level.dat")}", TextWrapping = TextWrapping.Wrap }); panel.Children.Add(new TextBlock { Text = world.Warning, Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCautionBrush"], TextWrapping = TextWrapping.Wrap }); panel.Children.Add(copy); panel.Children.Add(select);
        await new ContentDialog { XamlRoot = XamlRoot, Title = world.DisplayName, Content = panel, CloseButtonText = "Close", FullSizeDesired = true }.ShowAsync();
    }
}
