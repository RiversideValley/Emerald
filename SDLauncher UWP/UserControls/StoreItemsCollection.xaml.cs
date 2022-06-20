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
using SDLauncher_UWP.Helpers;
// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace SDLauncher_UWP.UserControls
{
    public sealed partial class StoreItemsCollection : UserControl
    {
        public event EventHandler<StoreItemSelectedEventArgs> StoreItemSelected = delegate { };
        public event EventHandler<SearchRequestedEventArgs> SearchRequested = delegate { };
        public StoreItemsCollection()
        {
            this.InitializeComponent();
        }
        public string SearchText
        {
            get
            {
                return txtSearch.Text.ToString();
            }
        }
        public List<StoreItem> ItemsSource
        {
            get { return (List<StoreItem>)GetValue(ItemsSourceProperty); }
            set { SetValue(ItemsSourceProperty, value); storeItemsGrid.ItemsSource = value; }
        }

        // Using a DependencyProperty as the backing store for ItemsSource.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register("ItemsSource", typeof(List<StoreItem>), typeof(StoreItemsCollection), new PropertyMetadata(null));

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
                SearchRequested(this, new SearchRequestedEventArgs(txtSearch.Text));
            }
            else
            {
                SearchRequested(this, new SearchRequestedEventArgs(""));
            }
        }

        private void txtSearch_KeyDown(object sender, KeyRoutedEventArgs e)
        {

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
        public SearchRequestedEventArgs(string itm)
        {
            Name = itm;
        }
    }
}
