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
using System.Threading.Tasks;
// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace SDLauncher_UWP.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class StorePage : Page
    {
        public List<StoreItem> Items { get; set; }
        public StorePage()
        {
            this.InitializeComponent();
            vars.Launcher.Labrinth.UIChangeRequested += Labrinth_UIChangeRequested;
            LoadData();
        }

        private void Labrinth_UIChangeRequested(object sender, SDLauncher.UIChangeRequestedEventArgs e)
        {
            this.IsEnabled = e.UI;
        }

        public async void LoadData()
        {
            Items = new List<StoreItem>();
            await Search("", true);
        }
        public void UpdateSource()
        {
            ItemsCollection.ItemsSource = null;
            ItemsCollection.ItemsSource = Items;
        }
        public async Task Search(string name,bool AddExists)
        {
            try
            {
                var r = await vars.Launcher.Labrinth.Search(name, 30);
                var itms = new List<StoreManager.StoreMod>();
                if (AddExists)
                {
                    foreach (var hit in r.hits)
                    {
                        Items.Add(new StoreItem(hit, Items.Count + 1));
                    }
                }
                else
                {
                    Items = new List<StoreItem>();
                    foreach (var hit in r.hits)
                    {
                        Items.Add(new StoreItem(hit, Items.Count + 1));
                    }
                    
                }
            }
            catch
            {
                
            }
            UpdateSource();
            ItemsCollection.FocusSearch();
        }
        private void ItemsCollection_StoreItemSelected(object sender, UserControls.StoreItemSelectedEventArgs e)
        {
            foreach (var item in Items)
            {
                if(item.ID == e.ID)
                {
                    SelectItem(item);
                }
            }
        }

        public async void SelectItem(StoreItem itm)
        {
            this.IsEnabled = false;
            string d;
            try
            {
                d = await itm.BigDescription();
            }
            catch
            {
                d = itm.Description;
            }
            this.IsEnabled = false;
            mddescription.Text = d.Replace("<br>", "").Replace("<br/>","");
            txtName.Text = itm.Name;
            txtType.Text = itm.Type.ToString();
            icVers.ItemsSource = null;
            icVers.ItemsSource = itm.SupportedVers;
            flipSamples.ItemsSource = null;
            flipSamples.ItemsSource = itm.SampleImages;
            imgIcon.Source = itm.Icon;
            var f = new MenuFlyout();
            f.Placement = FlyoutPlacementMode.Bottom;
            if (itm.Type == StoreManager.Type.Mod)
            {
                foreach (var item in itm.ModDownloadLinks)
                {
                    var x = new MenuFlyoutItem { Text = item.Version.ToString(), Tag = item.Url };
                    ToolTipService.SetToolTip(x, item.SupportedVer);
                    x.Click += X_Click;
                    f.Items.Add(x);
                }
            }
            else if (itm.Type == StoreManager.Type.Shader)
            {
                foreach (var item in itm.ShaderDownloadLinks)
                {
                    var x = new MenuFlyoutItem { Text = item.Version.ToString(), Tag = item.Url };
                    ToolTipService.SetToolTip(x, item.SupportedVer);
                    x.Click += X_Click;
                    f.Items.Add(x);
                }
            }
            btnDownload.Flyout = f;
            this.IsEnabled = true;
            ItemView.IsPaneOpen = true;
        }

        private void X_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ItemView_PaneClosed(SplitView sender, object args)
        {
            //Canvas.SetZIndex(ItemsCollection, 1);
            //Canvas.SetZIndex(ItemView, 0);
        }

        private void ItemView_PaneOpened(SplitView sender, object args)
        {
            //Canvas.SetZIndex(ItemsCollection, 0);
            //Canvas.SetZIndex(ItemView, 1);
        }

        private async void ItemsCollection_SearchRequested(object sender, UserControls.SearchRequestedEventArgs e)
        {
            var txt = e.Name;
            await Task.Delay(new TimeSpan(0, 0, 0, 2));
            if (ItemsCollection.SearchText == txt)
            {
                Search(e.Name, false);
            }
        }
    }
}
