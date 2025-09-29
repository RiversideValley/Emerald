using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.DependencyInjection;
using Emerald.CoreX;
using Emerald.CoreX.Helpers;
using Emerald.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Windows.Storage;
using Windows.System;

namespace Emerald.Views;

public sealed partial class GamesPage : Page
{
    public GamesPageViewModel ViewModel { get; }

    public GamesPage()
    {
        ViewModel = Ioc.Default.GetService<GamesPageViewModel>();
        DataContext = ViewModel;
        this.InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        AddGameDialog.XamlRoot = App.Current.MainWindow.Content.XamlRoot;
        SettingsDialog.XamlRoot = App.Current.MainWindow.Content.XamlRoot;
        await ViewModel.InitializeCommand.ExecuteAsync(null);
    }

    private async void AddGame_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.StartAddGameCommand.Execute(null);
        var result = await AddGameDialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            await ViewModel.CreateGameCommand.ExecuteAsync(null);
        }
    }

    private async void VersionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel.SelectedModLoaderType != CoreX.Versions.Type.Vanilla)
        {
            await ViewModel.LoadModLoadersCommand.ExecuteAsync(null);
        }
    }

    private async void ModLoaderType_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            var comboBox = sender as ComboBox;
            if (comboBox?.SelectedItem is ComboBoxItem item)
            {
                var typeString = item.Tag?.ToString();
                if (Enum.TryParse<CoreX.Versions.Type>(typeString, out var type))
                {
                    ViewModel.SelectedModLoaderType = type;

                    bool showModLoaderOptions = type != CoreX.Versions.Type.Vanilla;
                    ModLoaderVersionComboBox.Visibility = showModLoaderOptions ? Visibility.Visible : Visibility.Collapsed;
                    
                    if (showModLoaderOptions && ViewModel.SelectedVersion != null)
                    {
                        await ViewModel.LoadModLoadersCommand.ExecuteAsync(null);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            this.Log().LogError(ex, "Failed to change modloader type");
        }
    }

    private async void ManageSettings_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.Tag is Game game)
        {
            GameSettingsControl.GameSettings = game.Options;
            var result = await SettingsDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                var core = Ioc.Default.GetService<Core>();
                core.SaveGames();
            }
        }
    }

    private async void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.Tag is Game game)
        {
            try
            {
                var folderPath = game.Path.BasePath;
                if (Directory.Exists(folderPath))
                {
                    await Launcher.LaunchFolderAsync(await StorageFolder.GetFolderFromPathAsync(folderPath));
                }
                else
                {
                    var notification = Ioc.Default.GetService<CoreX.Notifications.INotificationService>();
                    notification.Warning("FolderNotFound", "Game folder does not exist");
                }
            }
            catch (Exception ex)
            {
                this.Log().LogError(ex, "Failed to open game folder");
            }
        }
    }

    private void InstallGame_Click(object sender, RoutedEventArgs e)
    {
        if(sender is Button btn && btn.Tag is Game game)
        {
           _= ViewModel.InstallGameCommand.ExecuteAsync(game);
        }
    }

    private void LaunchGame_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Game game)
        {
            _= ViewModel.LaunchGameCommand.ExecuteAsync(game);
        }
    }
}
