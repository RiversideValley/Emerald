using Emerald.WinUI.Helpers.Settings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Linq;

namespace Emerald.WinUI.Views.Store
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class StorePage : Page
    {
        public StorePage()
        {
            this.InitializeComponent();
        }

        private void StoreItem_Click(object sender, RoutedEventArgs e)
        {

        }

        private async void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
               var r = await App.Current.Launcher.Labrinth.Search(txtSearch.Text, 30, SettingsSystem.Settings.App.Store.SortOptions.GetResult(), SettingsSystem.Settings.App.Store.Filter.GetResult());
                storeItemsGrid.ItemsSource = r.Hits.Select(x => new Models.StoreItem(x));
                pnlEmpty.Visibility = r.Hits.Select(x => new Models.StoreItem(x)).Any() ? Visibility.Collapsed : Visibility.Visible;
            }
            catch { }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            txtSearch.Focus(FocusState.Programmatic);
            SettingsSystem.Settings.App.Store.SortOptions.PropertyChanged += Store_PropertyChanged;
            SettingsSystem.Settings.App.Store.Filter.PropertyChanged += Store_PropertyChanged;
            Store_PropertyChanged(null, null);
        }

        private void Store_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            txtSearch_TextChanged(null, null);
            All.IsChecked = SettingsSystem.Settings.App.Store.Filter.All;
        }
        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
        }

        private void All_Click(object sender, RoutedEventArgs e)
        {
            SettingsSystem.Settings.App.Store.Filter.All = true;
            All.IsChecked = true;
        }
    }
}
