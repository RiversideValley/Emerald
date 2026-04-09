using System;
using System.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using Emerald.CoreX;
using Emerald.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Windows.Storage;
using Windows.System;
using Path = System.IO.Path;
using System.IO;
using Emerald.UserControls;
using Emerald.Helpers;

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

        var wizardControl = new AddGameWizardControl();
        // The DataContext is inherited from the Page, so the wizard will use our ViewModel
        wizardControl.DataContext = ViewModel;

        _addGameDialog = wizardControl.ToContentDialog(null,defaultButton: ContentDialogButton.Primary);

        // We manage the dialog state manually here
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        _addGameDialog.PrimaryButtonClick += OnDialogPrimaryButtonClick;
        _addGameDialog.SecondaryButtonClick += OnDialogSecondaryButtonClick;

        UpdateAddGameDialogButtons(); // Set initial button state
        await _addGameDialog.ShowAsync();

        // Clean up handlers to prevent memory leaks
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _addGameDialog.PrimaryButtonClick -= OnDialogPrimaryButtonClick;
        _addGameDialog.SecondaryButtonClick -= OnDialogSecondaryButtonClick;
        _addGameDialog = null;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // When a property that affects the button's state changes, we update it
        if (e.PropertyName is nameof(ViewModel.AddGameWizardStep) or nameof(ViewModel.IsPrimaryButtonEnabled))
        {
            UpdateAddGameDialogButtons();
        }
    }

    private void UpdateAddGameDialogButtons()
    {
        if (_addGameDialog is null) return;

        switch (ViewModel.AddGameWizardStep)
        {
            case 0: // Version Selection
                _addGameDialog.Title = "Add New Game (Step 1 of 2)";
                _addGameDialog.PrimaryButtonText = "Next";
                _addGameDialog.SecondaryButtonText = "Cancel";
                break;
            case 1: // Customize & Name
                _addGameDialog.Title = "Add New Game (Step 2 of 2)";
                _addGameDialog.PrimaryButtonText = "Create";
                _addGameDialog.SecondaryButtonText = "Back";
                break;
        }
        _addGameDialog.IsPrimaryButtonEnabled = ViewModel.IsPrimaryButtonEnabled;
    }

    private void OnDialogPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // "Next" or "Create"
        if (ViewModel.AddGameWizardStep < 1) // If not on the last step
        {
            args.Cancel = true; // Keep dialog open
            ViewModel.GoToNextStepCommand.Execute(null);
        }
        else
        {
            // Last step, create game and let dialog close
            ViewModel.CreateGameCommand.Execute(null);
        }
    }

    private void OnDialogSecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // "Back" or "Cancel"
        if (ViewModel.AddGameWizardStep > 0)
        {
            args.Cancel = true; // Keep dialog open
            ViewModel.GoToPreviousStepCommand.Execute(null);
        }
        // On step 0, do nothing, which lets the dialog close as "Cancel"
    }

    // Unchanged methods below...
    private async void ManageSettings_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.Tag is Game game)
        {
            var GameSettingsControl = new MinecraftSettingsUC()
            {
                ShowMainSettings = false
            };
            GameSettingsControl.GameSettings = game.Options;
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
        if (sender is Button btn && btn.Tag is Game game)
        {
            _ = ViewModel.InstallGameCommand.ExecuteAsync(game);
        }
    }

    private void LaunchGame_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Game game)
        {
            _ = ViewModel.LaunchGameCommand.ExecuteAsync(game);
        }
    }
}
