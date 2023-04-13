using Emerald.WinUI.Helpers.AppInstancing;
using Microsoft.UI.Xaml;
using System.Threading.Tasks;

namespace Emerald.WinUI
{
    public partial class App : Application
    {
        private readonly SingleInstanceDesktopApp _singleInstanceApp;
        public readonly Core.Emerald Launcher = new();

        public static Task<string> CheckForUpdates()
        {
            return null;
        }

        public App()
        {
            InitializeComponent();
            _singleInstanceApp = new SingleInstanceDesktopApp("Riverside.Depth.EmeraldLauncher");
            _singleInstanceApp.Launched += OnSingleInstanceLaunched;
        }

        /// <summary>
        /// Gets the current <see cref="App"/> instance in use
        /// </summary>
        public new static App Current => (App)Application.Current;

        // Redirect the OnLaunched event to the single app instance 
        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            _singleInstanceApp.Launch(args.Arguments);
        }

        private void OnSingleInstanceLaunched(object? sender, SingleInstanceLaunchEventArgs e)
        {
            if (e.IsFirstLaunch)
            {
                System.Net.ServicePointManager.DefaultConnectionLimit = 256;
                MainWindow = new MainWindow();
                MainWindow.Activate();
                MainWindow.Closed += (_, _) => Helpers.Settings.SettingsSystem.SaveData();
            }
            else
            {
                // TODO: do things on subsequent launches, like processing arguments from e.Arguments
            }
        }

        public Window MainWindow { get; private set; }
    }
}
