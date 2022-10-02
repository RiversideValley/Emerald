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
using Emerald.Core;
using System.Threading.Tasks;
using CmlLib.Core;
using Windows.Storage;
using Emerald.WinUI.Helpers;
// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Emerald.WinUI.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class HomePage : Page
    {
        public HomePage()
        {
            this.InitializeComponent();
            MainCore.Intialize();
            MainCore.Launcher.InitializeLauncher(new MinecraftPath(ApplicationData.Current.LocalFolder.Path));
        }
        public void Initialize()
        {
            ToggleMenuFlyoutItem createItm(string name)
            {
                var itm = new ToggleMenuFlyoutItem();
                itm.Text = name;
                itm.Click += tglMitVerSort_Click;
                return itm;
            }
            var m = new MenuFlyout()
            {
                Items =
                {
                    createItm("Release".ToLocalizedString()),
                    createItm("Snapshot".ToLocalizedString()),
                    createItm("OldBeta".ToLocalizedString()),
                    createItm("OldAlpha".ToLocalizedString()),
                    createItm("Custom".ToLocalizedString())
                }
            };
        }
        private async void btnVersion_Click(object sender, RoutedEventArgs e)
        {
            paneVersions.IsPaneOpen = true;
            await MainCore.Launcher.RefreshVersions();
            treeVer.ItemsSource = MCVersionsCreator.CreateVersions();
        }

        private void tglMitVerSort_Click(object sender, RoutedEventArgs e)
        {
            var mit = sender as ToggleMenuFlyoutItem;
            if(mit.Text == "Release".ToLocalizedString())
            {
                MCVersionsCreator.Configuration.Release = mit.IsChecked;
            }
            else if (mit.Text == "Snapshot".ToLocalizedString())
            {
                MCVersionsCreator.Configuration.Snapshot = mit.IsChecked;
            }
            else if (mit.Text == "Oldbeta".ToLocalizedString())
            {
                MCVersionsCreator.Configuration.OldBeta = mit.IsChecked;
            }
            else if (mit.Text == "OldAlpha".ToLocalizedString())
            {
                MCVersionsCreator.Configuration.OldAlpha = mit.IsChecked;
            }
            else if (mit.Text == "Custom".ToLocalizedString())
            {
                MCVersionsCreator.Configuration.Custom = mit.IsChecked;
            }
            treeVer.ItemsSource = MCVersionsCreator.CreateVersions();
        }
    }
}
