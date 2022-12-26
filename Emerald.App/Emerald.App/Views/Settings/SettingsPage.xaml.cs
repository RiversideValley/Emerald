using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using System;

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
                    NavigateOnce(typeof(AppearancePage));
                    navView.AlwaysShowHeader = true;
                    break;
                case "About":
                    navView.AlwaysShowHeader = false;
                    NavigateOnce(typeof(AboutPage));
                    break;
                default:
                    NavigateOnce(typeof(GeneralPage));
                    navView.AlwaysShowHeader = true;
                    break;
            }
            navView.Header = itm.Content;
        }
        private void NavigateOnce(Type type)
        {
            if (contentframe.Content == null || contentframe.Content.GetType() != type)
            {
                contentframe.Navigate(type, null, new DrillInNavigationTransitionInfo());
            }
        }
    }

}
