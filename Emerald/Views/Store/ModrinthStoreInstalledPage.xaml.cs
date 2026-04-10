using Emerald.CoreX.Store;
using Emerald.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Emerald.Views.Store;

public sealed partial class ModrinthStoreInstalledPage : Page
{
    private ModrinthStorePageViewModel? ViewModel => DataContext as ModrinthStorePageViewModel;

    public ModrinthStoreInstalledPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        DataContext = e.Parameter as ModrinthStorePageViewModel;
    }

    private async void RemoveTracked_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is InstalledStoreItem item && ViewModel != null)
        {
            await ViewModel.RemoveTrackedCommand.ExecuteAsync(item);
        }
    }

    private async void ForceRemove_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not InstalledStoreItem item || ViewModel == null)
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
