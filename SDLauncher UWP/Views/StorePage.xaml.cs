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
using System.Threading.Tasks;
// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace SDLauncher.UWP.Views
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
            vars.Launcher.Labrinth.MainUIChangeRequested += Labrinth_MainUIChangeRequested;
            LoadData();
        }

        private void Labrinth_MainUIChangeRequested(object sender, Helpers.SDLauncher.UIChangeRequestedEventArgs e)
        {
            this.IsEnabled = e.UI;
        }

        private void Labrinth_UIChangeRequested(object sender, Helpers.SDLauncher.UIChangeRequestedEventArgs e)
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
        public async Task Search(string name, bool AddExists,LabrinthResults.SearchSortOptions sortBy = LabrinthResults.SearchSortOptions.Relevance, LabrinthResults.SearchCategories[] categories = null)
        {
            try
            {
                var r = await vars.Launcher.Labrinth.Search(name, 30, sortBy, categories);

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
            if (itm.SampleImages.Count < 1)
            {
                flipSamples.Visibility = Visibility.Collapsed;
            }
            else
            {
                flipSamples.Visibility = Visibility.Visible;
            }
            flipSamples.ItemsSource = itm.SampleImages;
            imgIcon.Source = itm.Icon;
            expdDownloads.IsExpanded = false;
            expdGallery.IsExpanded = false;
            expdVers.IsExpanded = false;
            var alldVers = await vars.Launcher.Labrinth.GetVersions(itm.ProjectID);
            ModdownloadView.ItemsSource = alldVers;
            itm.DownloadLinks = alldVers;
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
        private static void ScrollToElement(ScrollViewer scrollViewer, UIElement element, bool isVerticalScrolling = true, bool smoothScrolling = true, float? zoomFactor = null)
        {
            var transform = element.TransformToVisual((UIElement)scrollViewer.Content);
            var position = transform.TransformPoint(new Point(0, 0));

            if (isVerticalScrolling)
            {
                scrollViewer.ChangeView(null, position.Y, zoomFactor, !smoothScrolling);
            }
            else
            {
                scrollViewer.ChangeView(position.X, null, zoomFactor, !smoothScrolling);
            }
        }
        private void ItemsCollection_SearchRequested(object sender, UserControls.SearchRequestedEventArgs e)
        {
            Search(e.Name, false,e.SortOptions,e.SearchCategories);
            
        }

        private void btnDownload_Click(object sender, RoutedEventArgs e)
        {
            expdDownloads.IsExpanded = true;
            ScrollToElement(scrlView, expdDownloads);
        }

        private void ModdownloadView_DownloadRequested(UserControls.ModrinthDownloadsListView sender, LabrinthResults.DownloadManager.File args)
        {
            vars.Launcher.Labrinth.DownloadMod(args, vars.Launcher.Launcher.MinecraftPath);
        }
    }
}
