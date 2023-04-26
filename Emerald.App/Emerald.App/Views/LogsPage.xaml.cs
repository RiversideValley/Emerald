using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using Windows.Storage;
using WinUIEx;

namespace Emerald.WinUI.Views
{
    public sealed partial class LogsPage : Page
    {
        public LogsPage()
        {
            InitializeComponent();
            MainWindow.HomePage.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == "Logs")
                    sv.ScrollToVerticalOffset(sv.ScrollableHeight);
            };
        }

        private void Clear_Click(object sender, RoutedEventArgs e) =>
            MainWindow.HomePage.Logs = null;

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            var p = App.Current.MainWindow.CreateSaveFilePicker();
            p.FileTypeChoices.Add("Logs File", new List<string> { ".log" });
            p.FileTypeChoices.Add("Text File", new List<string> { ".txt" });
            var f = await p.PickSaveFileAsync();
            if (f != null)
            {
                await FileIO.WriteTextAsync(f, MainWindow.HomePage.Logs);
            }
        }
    }
}
