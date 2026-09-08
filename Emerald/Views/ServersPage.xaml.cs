using CommunityToolkit.Mvvm.DependencyInjection;
using Emerald.CoreX.Services.Servers;
using Emerald.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Windows.ApplicationModel.DataTransfer;
namespace Emerald.Views;
public sealed partial class ServersPage : Page
{
    private CancellationTokenSource? _searchCancellation;
    public ServersPageViewModel ViewModel { get; } = Ioc.Default.GetRequiredService<ServersPageViewModel>();
    public ServersPage() => InitializeComponent();
    protected override async void OnNavigatedTo(NavigationEventArgs e) { base.OnNavigatedTo(e); await ViewModel.InitializeAsync(); if (e.Parameter is CoreX.Game game) ViewModel.SelectedGame = game; }
    protected override void OnNavigatedFrom(NavigationEventArgs e) { _searchCancellation?.Cancel(); base.OnNavigatedFrom(e); }
    private void Back_Click(object sender, RoutedEventArgs e) { if (Frame.CanGoBack) Frame.GoBack(); else Frame.Navigate(typeof(HomePage)); }
    private async void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return; _searchCancellation?.Cancel(); var cts = _searchCancellation = new();
        try { await Task.Delay(350, cts.Token); ViewModel.Page = 1; await ViewModel.SearchAsync(cts.Token); } catch (OperationCanceledException) { }
    }
    private async void DiscoverPlay_Click(object sender, RoutedEventArgs e) { if (sender is FrameworkElement { Tag: ServerDirectoryEntry server }) await ViewModel.LaunchAsync(server); }
    private void DiscoverFavorite_Click(object sender, RoutedEventArgs e) { if (sender is FrameworkElement { Tag: ServerDirectoryEntry server }) ViewModel.Favorite(server); }
    private async void PreviousPage_Click(object sender, RoutedEventArgs e) { if (ViewModel.Page > 1) { ViewModel.Page--; await ViewModel.SearchAsync(); } }
    private async void NextPage_Click(object sender, RoutedEventArgs e) { ViewModel.Page++; await ViewModel.SearchAsync(); }
    private async void FavoritePlay_Click(object sender, RoutedEventArgs e) { if (sender is FrameworkElement { Tag: SavedServer server }) await ViewModel.LaunchAsync(server); }
    private async void DiscoverDetails_Click(object sender, RoutedEventArgs e) { if (sender is FrameworkElement { Tag: ServerDirectoryEntry server }) await ShowDetailsAsync(server.Name, server.Address, server.Motd, server.Version, server.PlayerText, server.TagsText); }
    private async void FavoriteDetails_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: SavedServer server }) return; var snapshot = await ViewModel.GetStatusAsync(server);
        await ShowDetailsAsync(server.Name, server.Address, snapshot.Motd, snapshot.Version, $"{snapshot.Players:N0} / {snapshot.MaxPlayers:N0} players", $"{snapshot.State} • checked {snapshot.CheckedAt.ToLocalTime():g}\n{snapshot.Software}\nMap: {snapshot.Map}\nEULA blocked: {snapshot.EulaBlocked}");
    }
    private async Task ShowDetailsAsync(string name, string address, string? motd, string? version, string players, string? extra)
    {
        var copy = new Button { Content = "Copy address", HorizontalAlignment = HorizontalAlignment.Left }; copy.Click += (_, _) => { var package = new DataPackage(); package.SetText(address); Clipboard.SetContent(package); };
        var content = new StackPanel { Spacing = 8 }; content.Children.Add(new TextBlock { Text = address, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold }); content.Children.Add(copy); content.Children.Add(new TextBlock { Text = motd, TextWrapping = TextWrapping.Wrap }); content.Children.Add(new TextBlock { Text = $"{version} • {players}\n{extra}", TextWrapping = TextWrapping.Wrap });
        await new ContentDialog { XamlRoot = XamlRoot, Title = name, Content = content, CloseButtonText = "Close", FullSizeDesired = true }.ShowAsync();
    }
    private async void AddCustom_Click(object sender, RoutedEventArgs e)
    {
        var name = new TextBox { Header = "Name" }; var address = new TextBox { Header = "Address", PlaceholderText = "play.example.net:25565" }; var error = new TextBlock { Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCriticalBrush"] }; var panel = new StackPanel { Spacing = 10 }; panel.Children.Add(name); panel.Children.Add(address); panel.Children.Add(error);
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = "Add custom server", Content = panel, PrimaryButtonText = "Add", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Primary };
        dialog.PrimaryButtonClick += (_, args) => { if (!MinecraftServerAddressParser.TryParse(address.Text, out var parsedAddress, out var message)) { args.Cancel = true; error.Text = message; } };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary) ViewModel.AddCustom(name.Text, address.Text);
    }
    private async void RemoveFavorite_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: SavedServer server }) return;
        if (await new ContentDialog { XamlRoot = XamlRoot, Title = "Remove favorite?", Content = $"Profiles using {server.Name} will be marked as needing attention.", PrimaryButtonText = "Remove", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Close }.ShowAsync() == ContentDialogResult.Primary) ViewModel.Remove(server);
    }
}
