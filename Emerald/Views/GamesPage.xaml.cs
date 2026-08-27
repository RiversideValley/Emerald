using System;
using CommunityToolkit.Mvvm.DependencyInjection;
using Emerald.CoreX;
using Emerald.CoreX.Helpers;
using Emerald.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Windows.Storage;
using Windows.System;
using Path = System.IO.Path;
using System.IO;
using System.Linq;
using Emerald.CoreX.Models;
using Emerald.CoreX.Services;
using Emerald.CoreX.Installation;
using Emerald.Helpers;
using Emerald.Helpers.Enums;
using Emerald.UserControls;
using Emerald.Views.Store;
using Microsoft.UI.Xaml.Media.Animation;

namespace Emerald.Views;

public sealed partial class GamesPage : Page
{
    public GamesPageViewModel ViewModel { get; }
    private ContentDialog? _addGameDialog;

    public GamesPage()
    {
        ViewModel = Ioc.Default.GetService<GamesPageViewModel>();
        DataContext = ViewModel;
        this.InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.InitializeCommand.ExecuteAsync(null);
    }

    private async void AddGame_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.StartAddGameCommand.Execute(null);

        _addGameDialog = new AddGameDialog(ViewModel)
        {
            XamlRoot = XamlRoot
        };
        _addGameDialog.Resources["ContentDialogMaxWidth"] = 1400;

        try
        {
            await _addGameDialog.ShowAsync();
        }
        finally
        {
            _addGameDialog = null;
        }
    }

    // Unchanged methods below...
    private async void ManageSettings_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.Tag is Game game)
        {
            var GameSettingsControl = new MinecraftSettingsUC()
            {
                ShowMainSettings = false,
                Game = game
            };
            GameSettingsControl.GameSettings = game.GetEditableSettings();
            var SettingsDialog = GameSettingsControl.ToContentDialog("Game Settings - " + game.Version.DisplayName, "Close");

            var result = await SettingsDialog.ShowAsync();

                var core = Ioc.Default.GetService<Core>();
                core.SaveGames();
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
                    folderPath.RevealInFinder();
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

    private async void InstallGame_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Game game)
        {
            await ViewModel.InstallGameCommand.ExecuteAsync(game);
        }
    }

    private async void VerifyGame_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: Game game }) return;
        await ViewModel.VerifyGameCommand.ExecuteAsync(game);
    }

    private async void RepairGame_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: Game game }) return;
        await ViewModel.RepairGameCommand.ExecuteAsync(game);
    }

    private async void LaunchGame_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Game game)
        {
            var accountService = Ioc.Default.GetService<IAccountService>();
            var notificationService = Ioc.Default.GetService<CoreX.Notifications.INotificationService>();

            try
            {
                if (accountService.Accounts.Count == 0 || accountService.GetSelectedAccount() == null)
                {
                    await accountService.LoadAllAccountsAsync();
                }
            }
            catch (Exception ex)
            {
                this.Log().LogError(ex, "Failed to load accounts for launch.");
                notificationService.Error("AccountLoadError", "Could not load accounts for launch.", ex: ex);
                return;
            }

            var selectedAccount = accountService.GetSelectedAccount();
            if (selectedAccount == null)
            {
                notificationService.Warning("NoSelectedAccount", "Select an account before launching a game.");
                NavigateToAccounts();
                return;
            }

            await ViewModel.LaunchGameAsync(game, selectedAccount);
        }
    }

    private void StopGame_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Game game)
        {
            _ = ViewModel.StopGameCommand.ExecuteAsync(game);
        }
    }

    private async void ForceStopGame_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement item && item.DataContext is Game game)
        {
            var result = await MessageBox.Show(
                "Force Stop",
                "Do you really want to force stop your game? This might cause corruptions in your game files.",
                MessageBoxButtons.YesNo);
            
            if(result is not MessageBoxResults.Yes) 
                return;
            
            _ = ViewModel.ForceStopGameCommand.ExecuteAsync(game);
        }
    }

    private void ViewLogs_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not Game game)
        {
            return;
        }

        if (App.Current.MainWindow.Content is Frame rootFrame && rootFrame.Content is MainPage mainPage)
        {
            mainPage.NavigateToTag("Logs", game.Path.BasePath);
            return;
        }

        Frame?.Navigate(typeof(LogsPage), game.Path.BasePath, new EntranceNavigationTransitionInfo());
    }

    private void OpenStore_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not Game game)
        {
            return;
        }

        if (App.Current.MainWindow.Content is Frame rootFrame && rootFrame.Content is MainPage mainPage)
        {
            mainPage.NavigateToTag("Store", game.Path.BasePath);
            return;
        }

        Frame?.Navigate(typeof(ModrinthStorePage), game.Path.BasePath, new EntranceNavigationTransitionInfo());
    }

    private void RemoveGame_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.Tag is Game game)
        {
             ViewModel.RemoveGameCommand.Execute(game);
        }
        }

    private void RemoveGameWFiles_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.Tag is Game game)
        {
            _ = ViewModel.RemoveGameWithFilesCommand.ExecuteAsync(game);
        }
    }

    private void NavigateToAccounts()
    {
        if (App.Current.MainWindow.Content is Frame rootFrame && rootFrame.Content is MainPage mainPage)
        {
            mainPage.NavigateToTag("Accounts");
            return;
        }

        Frame?.Navigate(typeof(AccountsPage), null, new EntranceNavigationTransitionInfo());
    }
    
    private async void EditOptions_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not Game game) return;

        var dialog = new GameOptionsDialog(game) { XamlRoot = XamlRoot };
        await dialog.ShowAsync();
    }
}
