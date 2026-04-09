using CommunityToolkit.Mvvm.DependencyInjection;
using Emerald.CoreX.Helpers;
using Emerald.CoreX.Store;
using Emerald.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;

namespace Emerald.Views.Store;

public sealed partial class ModrinthStorePage : Page
{
    public ModrinthStorePageViewModel ViewModel { get; }

    public ModrinthStorePage()
    {
        ViewModel = Ioc.Default.GetService<ModrinthStorePageViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.InitializeCommand.ExecuteAsync(e.Parameter);
    }

    private async void CategoryToggle_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.SearchCommand.ExecuteAsync(null);
    }

    private async void Page_KeyUp(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            await ViewModel.SearchCommand.ExecuteAsync(null);
        }
    }

    private async void RemoveTracked_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is InstalledStoreItem item)
        {
            await ViewModel.RemoveTrackedCommand.ExecuteAsync(item);
        }
    }

    private async void ForceRemove_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not InstalledStoreItem item)
        {
            return;
        }

        var confirmDialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Force remove untracked item?",
            Content = $"This will permanently delete \"{item.DisplayName}\" from disk.",
            PrimaryButtonText = "Force Remove",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close
        };

        var result = await confirmDialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            await ViewModel.ForceRemoveCommand.ExecuteAsync(item);
        }
    }
}
