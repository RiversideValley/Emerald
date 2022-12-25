using Microsoft.UI.Xaml;
using System.Threading.Tasks;
// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Emerald.WinUI
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        public static async Task<string> CheckForUpdates()
        {
            return null;
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
