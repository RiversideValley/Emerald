using System;
using Microsoft.UI.Xaml.Controls;
using Emerald.ViewModels;
using Microsoft.UI.Xaml;
using Emerald.CoreX.Helpers;

namespace Emerald.UserControls;

public sealed partial class AddGameWizardControl : UserControl
{
    // A little hacky, but avoids complex event bubbling or direct ViewModel reference here
    public GamesPageViewModel? ViewModel => this.DataContext as GamesPageViewModel;

    public AddGameWizardControl()
    {
        this.InitializeComponent();
    }

    private async void ModLoaderType_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel == null) return;

        try
        {
            if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem item)
            {
                var typeString = item.Tag?.ToString();
                if (Enum.TryParse<CoreX.Versions.Type>(typeString, out var type))
                {
                    ViewModel.SelectedModLoaderType = type;

                    bool showModLoaderOptions = type != CoreX.Versions.Type.Vanilla;
                    ModLoaderVersionListView.Visibility = showModLoaderOptions ? Visibility.Visible : Visibility.Collapsed;

                    if (showModLoaderOptions && ViewModel.SelectedVersion != null)
                    {
                        await ViewModel.LoadModLoadersCommand.ExecuteAsync(null);
                    }
                    else
                    {
                        ViewModel.AvailableModLoaders.Clear();
                        ViewModel.SelectedModLoader = null; // Clear selection
                    }
                }
            }
        }
        catch (Exception ex)
        {
            this.Log().LogError(ex, "Failed to change modloader type");
        }
    }
}
