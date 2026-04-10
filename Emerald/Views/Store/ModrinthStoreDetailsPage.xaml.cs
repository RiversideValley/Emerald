using Emerald.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Emerald.Views.Store;

public sealed partial class ModrinthStoreDetailsPage : Page
{
    public ModrinthStoreDetailsPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        DataContext = e.Parameter as ModrinthStorePageViewModel;
    }
}
