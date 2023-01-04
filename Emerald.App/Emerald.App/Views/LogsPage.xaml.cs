// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
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
            this.InitializeComponent();
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
            var p = App.MainWindow.CreateSaveFilePicker();
            p.FileTypeChoices.Add("Logs File", new List<string> { ".log" });
            p.FileTypeChoices.Add("Text File", new List<string> { ".txt" });
            var f = await p.PickSaveFileAsync();
            if (f!= null)
            {
               await FileIO.WriteTextAsync(f, MainWindow.HomePage.Logs);
            }
        }
    }
}
