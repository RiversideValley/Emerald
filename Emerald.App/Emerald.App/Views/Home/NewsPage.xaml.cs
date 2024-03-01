using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;
using System.Threading.Tasks;
using SS = Emerald.WinUI.Helpers.Settings.SettingsSystem;


namespace Emerald.WinUI.Views.Home
{
    public sealed partial class NewsPage : Page
    {
        public event EventHandler? BackRequested;
        public NewsPage()
        {
            InitializeComponent();

            _ = App.Current.Launcher.News.LoadEntries(SS.Settings.App.NewsFilter.GetResult());

            this.Loaded += async (_, _) =>
            {
                SearchBox.Focus(FocusState.Programmatic);
                
                App.Current.Launcher.News.Entries.CollectionChanged += (_, _) =>
                    pnlEmpty.Visibility = App.Current.Launcher.News.Entries.Any() ? Visibility.Collapsed : Visibility.Visible;

                await Task.Delay(300);
                pnlEmpty.Visibility = App.Current.Launcher.News.Entries.Any() ? Visibility.Collapsed : Visibility.Visible;
            };

        }
        private void BackButton_Click(object sender, RoutedEventArgs e) =>
            BackRequested?.Invoke(this, new EventArgs());

        private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args) =>
            App.Current.Launcher.News.Search(sender.Text, SS.Settings.App.NewsFilter.GetResult());

        private void FilterButton_Click(object sender, RoutedEventArgs e) =>
            App.Current.Launcher.News.Search(SearchBox.Text, SS.Settings.App.NewsFilter.GetResult());
    }
}
