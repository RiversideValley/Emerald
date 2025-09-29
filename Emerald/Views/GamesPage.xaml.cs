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

namespace Emerald.Views;

public sealed partial class GamesPage : Page
{
    public GamesPageViewModel ViewModel { get; }

    public GamesPage()
    {
        ViewModel = Ioc.Default.GetService<GamesPageViewModel>();
        DataContext = ViewModel;
        this.InitializeComponent();
        // Subscribe to step changes to update dialog buttons
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModel.AddGameWizardStep) || e.PropertyName == nameof(ViewModel.IsStepOneNextEnabled) || e.PropertyName == nameof(ViewModel.IsStepTwoNextEnabled) || e.PropertyName == nameof(ViewModel.IsStepThreeCreateEnabled))
        {
            UpdateAddGameDialogButtons();
        }
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
        UpdateAddGameDialogButtons(); // Set initial button state
        await AddGameDialog.ShowAsync();
    }

    private void UpdateAddGameDialogButtons()
    {
        switch (ViewModel.AddGameWizardStep)
        {
            case 0: // Version Selection
                AddGameDialog.Title = "Add New Game (Step 1 of 3)";
                AddGameDialog.PrimaryButtonText = "Next";
                AddGameDialog.SecondaryButtonText = "Cancel";
                AddGameDialog.IsPrimaryButtonEnabled = ViewModel.IsStepOneNextEnabled;
                break;
            case 1: // Mod Loader Selection
                AddGameDialog.Title = "Add New Game (Step 2 of 3)";
                AddGameDialog.PrimaryButtonText = "Next";
                AddGameDialog.SecondaryButtonText = "Back";
                AddGameDialog.IsPrimaryButtonEnabled = ViewModel.IsStepTwoNextEnabled;
                break;
            case 2: // Final Details
                AddGameDialog.Title = "Add New Game (Step 3 of 3)";
                AddGameDialog.PrimaryButtonText = "Create";
                AddGameDialog.SecondaryButtonText = "Back";
                AddGameDialog.IsPrimaryButtonEnabled = ViewModel.IsStepThreeCreateEnabled;
                break;
        }
    }

    private void AddGameDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // This is the "Next" or "Create" button
        if (ViewModel.AddGameWizardStep < 2)
        {
            args.Cancel = true; // Prevent the dialog from closing
            ViewModel.GoToNextStepCommand.Execute(null);
        }
        else
        {
            // Last step, create the game and let the dialog close
            ViewModel.CreateGameCommand.Execute(null);
        }
    }

    private void AddGameDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // This is the "Back" or "Cancel" button
        if (ViewModel.AddGameWizardStep > 0)
        {
            args.Cancel = true; // Prevent the dialog from closing
            ViewModel.GoToPreviousStepCommand.Execute(null);
        }
        // On step 0, do nothing (let the dialog close as "Cancel")
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
                    else
                    {
                        ViewModel.AvailableModLoaders.Clear();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            this.Log().LogError(ex, "Failed to change modloader type");
        }
    }

    // Unchanged methods below...
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
