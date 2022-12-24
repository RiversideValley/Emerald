using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Emerald.WinUI.Views.Settings
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SettingsPage : Page
    {
        public SettingsPage()
        {
            this.InitializeComponent();
            this.Loaded += (_, _) => Navigate(navView.SelectedItem as UserControls.NavViewItem);
        }

        private void navView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            Navigate(navView.SelectedItem as UserControls.NavViewItem);
        }
        private void Navigate(UserControls.NavViewItem itm)
        {
            switch (itm.Tag)
            {
                case "Appearance":
                    contentframe.Navigate(typeof(AppearancePage),null, new DrillInNavigationTransitionInfo());
                    navView.AlwaysShowHeader = true;
                    break;
                case "About":
                    navView.AlwaysShowHeader = false;
                    contentframe.Navigate(typeof(AboutPage), null, new DrillInNavigationTransitionInfo());
                    break;
                default:
                    contentframe.Navigate(typeof(GeneralPage), null, new DrillInNavigationTransitionInfo());
                    navView.AlwaysShowHeader = true;
                    break;
            }
            navView.Header = itm.Content;
        }
    }

}
