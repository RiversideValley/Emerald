using CommunityToolkit.Mvvm.DependencyInjection;
using Emerald.CoreX.Runtime;
using Emerald.CoreX.Services;
using Emerald.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;

namespace Emerald.Views;

public sealed partial class HomePage : Page
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    public HomePageViewModel ViewModel { get; } = Ioc.Default.GetRequiredService<HomePageViewModel>();
    public HomePage()
    {
        InitializeComponent();
        _timer.Tick += (_, _) => ViewModel.RefreshRuntime();
    }
    protected override async void OnNavigatedTo(NavigationEventArgs e) { base.OnNavigatedTo(e); await ViewModel.InitializeAsync(); _timer.Start(); }
    protected override void OnNavigatedFrom(NavigationEventArgs e) { _timer.Stop(); base.OnNavigatedFrom(e); }
    private MainPage? Shell => App.Current?.MainWindow?.Content is Frame { Content: MainPage main } ? main : null;
    private void Accounts_Click(object sender, RoutedEventArgs e) => Shell?.NavigateToTag("Accounts");
    private void Primary_Click(object sender, RoutedEventArgs e) { if (!ViewModel.HasAccount) Shell?.NavigateToTag("Accounts"); else ViewModel.LaunchCommand.Execute(null); }
    private void Instances_Click(object sender, RoutedEventArgs e) => Shell?.NavigateToTag("Instances", ViewModel.SelectedGame);
    private void Logs_Click(object sender, RoutedEventArgs e) => Shell?.NavigateToTag("Logs", ViewModel.SelectedGame);
    private void Servers_Click(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(ServersPage), ViewModel.SelectedGame);
    private void Worlds_Click(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(WorldsPage), ViewModel.SelectedGame);
    private void PlaytimeCard_Tapped(object sender, TappedRoutedEventArgs e) => Frame.Navigate(typeof(PlaytimePage));
    private async void ProfileSelect_Click(object sender, RoutedEventArgs e) { if (sender is FrameworkElement { Tag: QuickProfile profile }) await ViewModel.SelectProfileAsync(profile, false); }
    private async void ProfilePlay_Click(object sender, RoutedEventArgs e) { if (sender is FrameworkElement { Tag: QuickProfile profile }) await ViewModel.SelectProfileAsync(profile, true); }
    private async void AddProfile_Click(object sender, RoutedEventArgs e) => await ShowProfileEditorAsync(null);
    private void ProfileMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: QuickProfile profile } anchor) return;
        var flyout = new MenuFlyout();
        Add("Edit", async () => await ShowProfileEditorAsync(profile));
        Add("Duplicate", () => { Ioc.Default.GetRequiredService<IQuickProfileService>().Duplicate(profile.Id); ViewModel.ReloadProfiles(); });
        Add("Move left", () => { Ioc.Default.GetRequiredService<IQuickProfileService>().Move(profile.Id, -1); ViewModel.ReloadProfiles(); });
        Add("Move right", () => { Ioc.Default.GetRequiredService<IQuickProfileService>().Move(profile.Id, 1); ViewModel.ReloadProfiles(); });
        Add("Delete", async () => { if (await new ContentDialog { XamlRoot = XamlRoot, Title = "Delete quick profile?", Content = profile.Name, PrimaryButtonText = "Delete", CloseButtonText = "Cancel" }.ShowAsync() == ContentDialogResult.Primary) { Ioc.Default.GetRequiredService<IQuickProfileService>().Remove(profile.Id); ViewModel.ReloadProfiles(); } });
        flyout.ShowAt(anchor);
        void Add(string text, Action action) { var item = new MenuFlyoutItem { Text = text }; item.Click += (_, _) => action(); flyout.Items.Add(item); }
    }
    private async Task ShowProfileEditorAsync(QuickProfile? existing)
    {
        var accounts = Ioc.Default.GetRequiredService<IAccountService>(); var instance = existing == null ? ViewModel.SelectedGame : ViewModel.Games.FirstOrDefault(x => x.InstanceId == existing.InstanceId); var account = existing == null ? accounts.GetSelectedAccount() : accounts.Accounts.FirstOrDefault(x => x.UniqueId == existing.AccountUniqueId);
        var name = new TextBox { Header = "Name", Text = existing?.Name ?? (instance == null ? "Quick play" : $"{instance.Version.DisplayName} quick play") };
        var glyph = new ComboBox { Header = "Icon", ItemsSource = new[] { "Play", "Server", "World", "Adventure", "Build" }, SelectedItem = existing?.GlyphKey ?? "Play", HorizontalAlignment = HorizontalAlignment.Stretch };
        var accent = new ComboBox { Header = "Accent", ItemsSource = new[] { "Emerald", "Blue", "Purple", "Orange", "Rose" }, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
        var instances = new ComboBox { Header = "Instance", ItemsSource = ViewModel.Games, DisplayMemberPath = "Version.DisplayName", SelectedItem = instance, HorizontalAlignment = HorizontalAlignment.Stretch };
        var accountBox = new ComboBox { Header = "Account", ItemsSource = accounts.Accounts, DisplayMemberPath = "Name", SelectedItem = account, HorizontalAlignment = HorizontalAlignment.Stretch };
        var destination = new ComboBox { Header = "Destination", ItemsSource = new[] { "Main menu", "Server", "World" }, SelectedItem = existing?.TargetKind == MinecraftLaunchTargetKind.Server ? "Server" : existing?.TargetKind == MinecraftLaunchTargetKind.World ? "World" : "Main menu", HorizontalAlignment = HorizontalAlignment.Stretch };
        var server = new ComboBox { Header = "Favorite server", ItemsSource = ViewModel.FavoriteServers, DisplayMemberPath = "Name", SelectedItem = ViewModel.FavoriteServers.FirstOrDefault(x => x.Id == existing?.SavedServerId), HorizontalAlignment = HorizontalAlignment.Stretch };
        var world = new ComboBox { Header = "World", ItemsSource = ViewModel.RecentWorlds, DisplayMemberPath = "DisplayName", SelectedItem = ViewModel.RecentWorlds.FirstOrDefault(x => x.FolderName == existing?.WorldFolderName), HorizontalAlignment = HorizontalAlignment.Stretch };
        var preview = new Border { Padding = new Thickness(14), Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"], CornerRadius = new CornerRadius(10), Child = new TextBlock { Text = "Quick profile preview", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold } };
        var error = new TextBlock { Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCriticalBrush"], TextWrapping = TextWrapping.Wrap };
        var panel = new StackPanel { Spacing = 10 }; foreach (var control in new UIElement[] { name, glyph, accent, instances, accountBox, destination, server, world, preview, error }) panel.Children.Add(control);
        void UpdateDestination() { server.Visibility = (string?)destination.SelectedItem == "Server" ? Visibility.Visible : Visibility.Collapsed; world.Visibility = (string?)destination.SelectedItem == "World" ? Visibility.Visible : Visibility.Collapsed; }
        destination.SelectionChanged += (_, _) => UpdateDestination(); UpdateDestination();
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = existing == null ? "New quick profile" : "Edit quick profile", Content = new ScrollViewer { Content = panel, MaxHeight = 620 }, PrimaryButtonText = "Save", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Primary, FullSizeDesired = true };
        QuickProfile? candidate = null;
        dialog.PrimaryButtonClick += (_, args) =>
        {
            var chosenInstance = instances.SelectedItem as CoreX.Game; var chosenAccount = accountBox.SelectedItem as CoreX.Models.EAccount; var chosenServer = server.SelectedItem as CoreX.Services.Servers.SavedServer; var chosenWorld = world.SelectedItem as CoreX.Services.Worlds.MinecraftWorld;
            candidate = new QuickProfile { Id = existing?.Id ?? Guid.NewGuid(), CreatedAt = existing?.CreatedAt ?? DateTimeOffset.UtcNow, Name = name.Text, GlyphKey = (string?)glyph.SelectedItem ?? "Play", AccentArgb = (string?)accent.SelectedItem switch { "Blue" => 0xFF0067C0, "Purple" => 0xFF744DA9, "Orange" => 0xFFCA5010, "Rose" => 0xFFC239B3, _ => 0xFF107C10 }, InstanceId = chosenInstance?.InstanceId ?? Guid.Empty, AccountUniqueId = chosenAccount?.UniqueId ?? string.Empty, TargetKind = (string?)destination.SelectedItem == "Server" ? MinecraftLaunchTargetKind.Server : (string?)destination.SelectedItem == "World" ? MinecraftLaunchTargetKind.World : MinecraftLaunchTargetKind.MainMenu, SavedServerId = chosenServer?.Id, WorldFolderName = chosenWorld?.FolderName, TargetDisplayNameSnapshot = chosenServer?.Name ?? chosenWorld?.DisplayName ?? "Main menu" };
            var validation = Ioc.Default.GetRequiredService<IQuickProfileService>().Validate(candidate, ViewModel.Games, accounts.Accounts, ViewModel.FavoriteServers); if (!validation.IsValid) { args.Cancel = true; error.Text = string.Join(Environment.NewLine, validation.Messages); }
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary && candidate != null) { Ioc.Default.GetRequiredService<IQuickProfileService>().Save(candidate); ViewModel.ReloadProfiles(); }
    }
}
