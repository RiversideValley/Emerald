using CommunityToolkit.Mvvm.DependencyInjection;
using Emerald.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using System.ComponentModel;

namespace Emerald.Views.Store;

public sealed partial class ModrinthStorePage : Page
{
    public ModrinthStorePageViewModel ViewModel { get; }

    public ModrinthStorePage()
    {
        ViewModel = Ioc.Default.GetService<ModrinthStorePageViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        Unloaded += ModrinthStorePage_Unloaded;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.InitializeCommand.ExecuteAsync(e.Parameter);
        UpdateDetailsNavigationState();
        StoreSectionsNav.SelectedItem = InstalledNavItem;
        Navigate("Installed");
    }

    private void StoreSectionsNav_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        Navigate((StoreSectionsNav.SelectedItem as NavigationViewItem)?.Tag as string);
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ModrinthStorePageViewModel.SelectedSearchResult)
            or nameof(ModrinthStorePageViewModel.HasSelectedSearchResult))
        {
            UpdateDetailsNavigationState();

            if (ViewModel.SelectedSearchResult != null)
            {
                StoreSectionsNav.SelectedItem = DetailsNavItem;
                Navigate("Details");
            }
            else if (StoreSectionsNav.SelectedItem == DetailsNavItem)
            {
                StoreSectionsNav.SelectedItem = BrowseNavItem;
                Navigate("Browse");
            }
        }
    }

    private void UpdateDetailsNavigationState()
    {
        DetailsNavItem.IsEnabled = ViewModel.HasSelectedSearchResult;
    }

    private void Navigate(string? target)
    {
        var pageType = target switch
        {
            "Browse" => typeof(ModrinthStoreBrowsePage),
            "Details" when ViewModel.HasSelectedSearchResult => typeof(ModrinthStoreDetailsPage),
            _ => typeof(ModrinthStoreInstalledPage)
        };

        if (contentframe.Content?.GetType() != pageType)
        {
            contentframe.Navigate(pageType, ViewModel, new DrillInNavigationTransitionInfo());
        }
    }

    private void ModrinthStorePage_Unloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        Unloaded -= ModrinthStorePage_Unloaded;
    }
}
