using CommunityToolkit.Mvvm.DependencyInjection;
using Emerald.Services;
using Emerald.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Emerald.Views;

public sealed partial class AddGameDialog : ContentDialog
{
    public GamesPageViewModel ViewModel { get; }

    public AddGameDialog(GamesPageViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = ViewModel;
        InitializeComponent();
        Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;
        RequestedTheme = (ElementTheme)Ioc.Default.GetService<SettingsService>().Settings.App.Appearance.Theme;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
        => Hide();

    private async void Create_Click(object sender, RoutedEventArgs e)
    {
        if (await ViewModel.SubmitAddGameAsync())
        {
            Hide();
        }
    }
}
