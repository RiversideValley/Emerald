using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using SS = Emerald.WinUI.Helpers.Settings.SettingsSystem;


namespace Emerald.WinUI.Views.Home
{
    public sealed partial class NewsPage : Page
    {
        public event EventHandler? BackRequested;
        public NewsPage()
        {
            this.InitializeComponent();
            _ = App.Launcher.News.LoadEntries(SS.Settings.App.NewsFilter.GetResult());
        }
        private void BackButton_Click(object sender, RoutedEventArgs e) =>
            BackRequested?.Invoke(this, new EventArgs());

        private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)=>
            App.Launcher.News.Search(sender.Text, SS.Settings.App.NewsFilter.GetResult());

        private void FilterButton_Click(object sender, RoutedEventArgs e) =>
            App.Launcher.News.Search(SearchBox.Text, SS.Settings.App.NewsFilter.GetResult());
    }
}
