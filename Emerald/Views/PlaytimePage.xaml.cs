using CommunityToolkit.Mvvm.DependencyInjection;
using Emerald.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
namespace Emerald.Views;
public sealed partial class PlaytimePage : Page
{
    public PlaytimePageViewModel ViewModel { get; } = Ioc.Default.GetRequiredService<PlaytimePageViewModel>();
    public PlaytimePage() => InitializeComponent();
    protected override void OnNavigatedTo(NavigationEventArgs e) { base.OnNavigatedTo(e); ViewModel.Initialize(); }
    private void Back_Click(object sender, RoutedEventArgs e) { if (Frame.CanGoBack) Frame.GoBack(); else Frame.Navigate(typeof(HomePage)); }
}
