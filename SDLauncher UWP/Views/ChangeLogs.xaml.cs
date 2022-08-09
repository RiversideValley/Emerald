using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel.AppService;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace SDLauncher.UWP.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ChangeLogsPage : Page
    {
        public ChangeLogsPage()
        {
            this.InitializeComponent();
            Core.MainCore.Launcher.LogsUpdated += Launcher_LogsUpdated;
        }

        private void Launcher_LogsUpdated(object sender, EventArgs e)
        {
            UpdateLogs();
        }

        public void UpdateLogs()
        {
            wvLogs.NavigateToString("");
            string finalHTML;
            if (this.ActualTheme == ElementTheme.Dark)
            {
                finalHTML = "<html>\n<head>\n<style>\np,h1,li,span,body,html {\ncolor: white;\n}\n</style>\n</head><body>" + Core.MainCore.Launcher.ChangeLogsHTMLBody + "</body></html>";
            }
            else
            {
                finalHTML = "<html>\n<head>\n<style>\np,h1,li,span,body,html {\ncolor: black;\n}\n</style>\n</head><body>" + Core.MainCore.Launcher.ChangeLogsHTMLBody + "</body></html>";
            }
            wvLogs.NavigateToString(finalHTML);
        }

        private void Page_ActualThemeChanged(FrameworkElement sender, object args)
        {
            UpdateLogs();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateLogs();
        }
    }
}
