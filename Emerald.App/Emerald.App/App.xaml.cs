﻿using Microsoft.UI.Xaml;
using Windows.UI;
using Serilog;
using Windows.Storage;
using System.IO;
// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Emerald.WinUI
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        public static class DirectResoucres
        {
            public static Color LayerFillColorDefaultColor => (Color)Current.Resources["LayerFillColorDefault"];
        }

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Verbose()
    .WriteTo.File(Path.Combine(ApplicationData.Current.LocalFolder.Path, "logs.log"))
    .WriteTo.Console()
    .CreateLogger();
            MainWindow = new MainWindow();
            MainWindow.Activate();
            Helpers.WindowManager.IntializeWindow(MainWindow);
            MainWindow.Closed += (_,_) => Log.CloseAndFlush();
        }

        public static Window MainWindow { get; private set; }
    }
}
