using CommunityToolkit.Mvvm.DependencyInjection;
using Emerald.CoreX.Helpers;
using Emerald.CoreX.Runtime;
using Emerald.CoreX.Services.Worlds;
using Emerald.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Windows.ApplicationModel.DataTransfer;
namespace Emerald.Views;

public sealed partial class WorldsPage : Page
{
    public WorldsPageViewModel ViewModel { get; } = Ioc.Default.GetRequiredService<WorldsPageViewModel>();
    public WorldsPage() => InitializeComponent();
    protected override async void OnNavigatedTo(NavigationEventArgs e) { base.OnNavigatedTo(e); await ViewModel.InitializeAsync(e.Parameter as CoreX.Game); }
    protected override void OnNavigatedFrom(NavigationEventArgs e) { ViewModel.Cancel(); base.OnNavigatedFrom(e); }
    private void Back_Click(object sender, RoutedEventArgs e) { if (Frame.CanGoBack) Frame.GoBack(); else Frame.Navigate(typeof(HomePage)); }
    private async void Refresh_Click(object sender, RoutedEventArgs e) { Controls.WorldThumbnail.InvalidateCache(); await ViewModel.RefreshAsync(); }
    private async void Select_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: WorldRowViewModel row } && ViewModel.SelectedGame is { } game)
        { await Ioc.Default.GetRequiredService<HomePageViewModel>().ApplySelectionAsync(new(game, MinecraftLaunchTargetKind.World, World: row.World)); Back_Click(sender, e); }
    }
    private async void Launch_Click(object sender, RoutedEventArgs e) { if (sender is FrameworkElement { Tag: WorldRowViewModel row }) await ViewModel.LaunchAsync(row.World); }
    private void Menu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: WorldRowViewModel row } anchor) return;
        var menu = new MenuFlyout(); var details = new MenuFlyoutItem { Text = DashboardText.Get("Details"), Icon = new FontIcon { Glyph = "\uE946" } };
        details.Click += async (_, _) => await DetailsAsync(row.World); menu.Items.Add(details);
        var reveal = new MenuFlyoutItem { Text = DashboardText.Get("Reveal"), Icon = new FontIcon { Glyph = "\uE838" } }; reveal.Click += (_, _) => FileManager.Reveal(row.World.FolderPath); menu.Items.Add(reveal); menu.ShowAt(anchor);
    }
    private async Task DetailsAsync(MinecraftWorld world)
    {
        var root = XamlRoot;
        if (root == null) return;
        var panel = new StackPanel { Spacing = 12, MinWidth = 260 };
        panel.Children.Add(new Controls.WorldThumbnail { Path = world.IconPath, Width = 64, Height = 64, HorizontalAlignment = HorizontalAlignment.Left });
        void Row(string key, string? value) { if (string.IsNullOrWhiteSpace(value)) return; var grid = new Grid { ColumnSpacing = 16 }; grid.ColumnDefinitions.Add(new() { Width = new(130) }); grid.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) }); var label = new TextBlock { Text = DashboardText.Get(key), Opacity = 0.7 }; var text = new TextBlock { Text = value, TextWrapping = TextWrapping.Wrap }; Grid.SetColumn(text, 1); grid.Children.Add(label); grid.Children.Add(text); panel.Children.Add(grid); }
        Row("Folder", world.FolderName); Row("LastPlayed", world.LastPlayed?.ToLocalTime().ToString("g")); Row("GameMode", DashboardText.Get(world.GameMode.ToString())); Row("Version", world.MinecraftVersion);
        Row("Difficulty", world.Difficulty?.ToString()); Row("Hardcore", DashboardText.Get(world.Hardcore ? "Yes" : "No")); Row("Cheats", DashboardText.Get(world.CheatsEnabled ? "Yes" : "No")); Row("DataVersion", world.DataVersion?.ToString()); Row("Seed", world.Seed?.ToString()); Row("Metadata", world.UsedBackupMetadata ? "level.dat_old" : "level.dat");
        var sizeText = new TextBlock { Text = DashboardText.Get("Calculating") }; panel.Children.Add(sizeText);
        if (!string.IsNullOrWhiteSpace(world.Warning)) panel.Children.Add(new TextBlock { Text = world.Warning, TextWrapping = TextWrapping.Wrap });
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var copy = new Button { Content = DashboardText.Get("CopySeed"), IsEnabled = world.Seed.HasValue }; copy.Click += (_, _) => { var data = new DataPackage(); data.SetText(world.Seed?.ToString() ?? string.Empty); Clipboard.SetContent(data); }; actions.Children.Add(copy);
        var reveal = new Button { Content = DashboardText.Get("Reveal") }; reveal.Click += (_, _) => FileManager.Reveal(world.FolderPath); actions.Children.Add(reveal); panel.Children.Add(actions);
        using var cancellation = new CancellationTokenSource();
        async Task SizeAsync() { try { var size = await Ioc.Default.GetRequiredService<IMinecraftWorldService>().CalculateSizeAsync(world, cancellation.Token); if (!cancellation.IsCancellationRequested) sizeText.Text = DashboardText.Get("Size") + ": " + (size.HasValue ? world.SizeText : DashboardText.Get("Unavailable")); } catch (OperationCanceledException) { } }
        _ = SizeAsync();
        try { await new ContentDialog { XamlRoot = XamlRoot, Title = world.DisplayName, Content = new ScrollViewer { Content = panel, MaxHeight = Math.Max(160, root.Size.Height - 220) }, CloseButtonText = DashboardText.Get("Close") }.ShowAsync(); }
        finally { cancellation.Cancel(); }
    }
    private void Layout_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var compact = e.NewSize.Width < 640; LayoutRoot.Padding = new Thickness(compact ? 16 : 24);
        Filters.RowSpacing = compact ? 12 : 0;
        Grid.SetRow(InstancePicker, compact ? 1 : 0); Grid.SetColumn(InstancePicker, compact ? 0 : 2); Grid.SetColumnSpan(InstancePicker, compact ? 2 : 1);
        Filters.ColumnDefinitions[2].Width = compact ? new GridLength(0) : new GridLength(280);
    }
}
