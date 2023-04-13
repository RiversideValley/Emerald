// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using Windows.Storage;
using WinUIEx;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Emerald.WinUI.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class LogsPage : Page
    {
        public LogsPage()
        {
            InitializeComponent();
            MainWindow.HomePage.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == "Logs")
                    sv.ScrollToVerticalOffset(txt.ActualOffset.Y);
            };
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.HomePage.Logs = null;
        }

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
