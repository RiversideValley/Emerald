using Emerald.WinUI.Helpers;
using Microsoft.UI.Xaml.Controls;
using System.Linq;
using Windows.ApplicationModel.DataTransfer;
using SS = Emerald.WinUI.Helpers.Settings.SettingsSystem;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Emerald.WinUI.Views.Settings
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class AboutPage : Page
    {
        public AboutPage()
        {
            this.InitializeComponent();
            this.Loaded += Start;
        }

        private void Start(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            lvBackups.ItemsSource = SS.GetBackups();
            this.Loaded -= Start;
        }

        private void Version_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            vTip.IsOpen = true;
        }

        private void vTip_ActionButtonClick(TeachingTip sender, object args)
        {
            vTip.IsOpen = false;
        }

        private void vTip_CloseButtonClick(TeachingTip sender, object args)
        {
            var VerData = new DataPackage();
            VerData.RequestedOperation = DataPackageOperation.Copy;
            VerData.SetText($"{"Version".ToLocalizedString()}: {DirectResoucres.AppVersion}\n{"BuildType".ToLocalizedString()}: {DirectResoucres.BuildType}");
            Clipboard.SetContent(VerData);
        }

        private void lvBackups_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
            cmdBarBackups.Visibility = lvBackups.SelectedItems.Any() ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;


    }
}
