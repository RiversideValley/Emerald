using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using SDLauncher.UWP.Helpers;
using SDLauncher.Core.Store;
// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace SDLauncher.UWP.UserControls
{
    public sealed partial class StoreItemsCollection : UserControl
    {
        public event EventHandler<StoreItemSelectedEventArgs> StoreItemSelected = delegate { };
        public event EventHandler<SearchRequestedEventArgs> SearchRequested = delegate { };
        public StoreItemsCollection()
        {
            this.InitializeComponent();
        }
        public string SearchText => txtSearch.Text.ToString();
        private  List<StoreItem> _ItemsSource;
        public List<StoreItem> ItemsSource
        {
            get { return _ItemsSource; }
            set { _ItemsSource = value; UpdateItems(); }
        }
        private void UpdateItems()
        {
            if(_ItemsSource != null)
            {
                storeItemsGrid.ItemsSource = null;
                storeItemsGrid.ItemsSource = ItemsSource;
                if(_ItemsSource.Count < 1)
                {
                    pnlEmpty.Visibility = Visibility.Visible;
                }
                else
                {
                    pnlEmpty.Visibility = Visibility.Collapsed;
                }
            }
        }
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            StoreItemSelected(this, new StoreItemSelectedEventArgs(int.Parse((sender as Button).Tag.ToString())));
        }
        public void FocusSearch()
        {
            txtSearch.Focus(FocusState.Programmatic);
        }
        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (txtSearch.Text.Length > 0)
            {
                SearchRequested(this, new SearchRequestedEventArgs(txtSearch.Text,this.SortOptions,this.SearchCategories.ToArray()));
            }
            else
            {
                SearchRequested(this, new SearchRequestedEventArgs("", this.SortOptions, this.SearchCategories.ToArray()));
            }
        }

        private void txtSearch_KeyDown(object sender, KeyRoutedEventArgs e)
        {

        }
        private LabrinthResults.SearchSortOptions SortOptions = LabrinthResults.SearchSortOptions.Relevance;
        private List<LabrinthResults.SearchCategories> SearchCategories = new List<LabrinthResults.SearchCategories>();
        private void SortOptions_Click(object sender, RoutedEventArgs e)
        {
           SortOptions = (LabrinthResults.SearchSortOptions)Enum.Parse(typeof(LabrinthResults.SearchSortOptions), (sender as Microsoft.UI.Xaml.Controls.RadioMenuFlyoutItem).Text);
            txtSearch_TextChanged(null, null);
        }

        private void Categories_Click(object sender, RoutedEventArgs e)
        {
            try {
                string name = (sender as ToggleMenuFlyoutItem).Text;
                ToggleMenuFlyoutItem btn = (sender as ToggleMenuFlyoutItem);
                if (name == "All")
                {
                    SearchCategories = new List<LabrinthResults.SearchCategories>();
                    tglAll.IsChecked = true;
                    tglAdv.IsChecked = false;
                    tglCursed.IsChecked = false;
                    tglDeco.IsChecked = false;
                    tglEqu.IsChecked = false;
                    tglFood.IsChecked = false;
                    tglLib.IsChecked = false;
                    tglMagic.IsChecked = false;
                    tglMisc.IsChecked = false;
                    tglOptimi.IsChecked = false;
                    tglStore.IsChecked = false;
                    tglTech.IsChecked = false;
                    tglUtil.IsChecked = false;
                    tglWorld.IsChecked = false;
                }
                else
                {
                    if (btn.IsChecked)
                    {
                        tglAll.IsChecked = false;
                        SearchCategories.Add((LabrinthResults.SearchCategories)Enum.Parse(typeof(LabrinthResults.SearchCategories), name));
                    }
                    else
                    {
                        SearchCategories.Remove((LabrinthResults.SearchCategories)Enum.Parse(typeof(LabrinthResults.SearchCategories), name));
                        if (SearchCategories.Count < 1)
                        {
                            tglAll.IsChecked = true;
                        }
                    }
                }
            }
            catch { }txtSearch_TextChanged(null, null);
        }
    }
    public class StoreItemSelectedEventArgs : EventArgs
    {
        public int ID { get; private set; }
        public StoreItemSelectedEventArgs(int itm)
        {
            ID = itm;
        }
    }
    public class SearchRequestedEventArgs : EventArgs
    {
        public string Name { get; private set; }
        public LabrinthResults.SearchSortOptions SortOptions { get; private set; }
        public LabrinthResults.SearchCategories[] SearchCategories { get; private set; }
        public SearchRequestedEventArgs(string itm, LabrinthResults.SearchSortOptions sortBy, LabrinthResults.SearchCategories[] categories)
        {
            Name = itm;
            SortOptions = sortBy;
            SearchCategories = categories;
        }
    }
}
