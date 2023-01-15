using Microsoft.UI.Xaml;
using System.Threading.Tasks;

namespace Emerald.WinUI
{
    public partial class App : Application
    {
        public static readonly Core.Emerald Launcher = new();

        public static async Task<string> CheckForUpdates()
        {
            return null;
        }

        public App()
        {
            InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            System.Net.ServicePointManager.DefaultConnectionLimit = 256;

            MainWindow = new MainWindow();
            MainWindow.Activate();
            MainWindow.Closed += (_, _) => Helpers.Settings.SettingsSystem.SaveData();
        }

        public static Window MainWindow { get; private set; }
    }
}
