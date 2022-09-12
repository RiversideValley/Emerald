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
using Emerald.WinUI.Models;
using Emerald.WinUI.UserControls;
using Emerald.WinUI.Helpers;
// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Emerald.WinUI
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public static Views.HomePage HomePage { get; set; } = new();
        public static Frame MainFrame { get; private set; }
        public MainWindow()
        {
            this.InitializeComponent();
            Title = "Emerald";
            Initialize();
        }
        public void Initialize()
        {
            NavView.MenuItems.Add(new NavViewItem() { Content = "Home".ToLocalizedString(), IconGlyph = "\xE71D" });
            NavView.MenuItems.Add(new NavViewItem() { Content = "Store".ToLocalizedString(), IconGlyph = "\xE71D" });
            NavView.MenuItems.Add(new NavViewItem() { Content = "Discord".ToLocalizedString(), IconGlyph = "\xE8F2" });
            NavView.FooterMenuItems.Add(new NavViewItem() { Content = "Tasks".ToLocalizedString(), IconGlyph = "\xe9d5" });
            NavView.FooterMenuItems.Add(new NavViewItem() { Content = "Logs".ToLocalizedString(), IconGlyph = "\xe756" });
            NavView.Header = new NavViewHeader() { HeaderText = "Home".ToLocalizedString(), HeaderMargin = GetNavViewHeaderMargin() };
            NavView.SelectedItem = NavView.MenuItems[0];
            NavView.DisplayModeChanged += (_, _) => (NavView.Header as NavViewHeader).HeaderMargin = GetNavViewHeaderMargin();
            MainFrame = frame;

            Helpers.WindowManager.SetTitleBar(this, AppTitleBar);

        }
        private Thickness GetNavViewHeaderMargin()
        {
            if (NavView.DisplayMode == NavigationViewDisplayMode.Minimal)
            {
                NavView.IsPaneToggleButtonVisible = true;
                return new Thickness(35, -40, 0, 0);
            }
            else
            {
                NavView.IsPaneToggleButtonVisible = false;
                return new Thickness(-30, -20, 0, 10);
            }
        }
        private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (!args.IsSettingsInvoked)
            {
                var h = (NavView.SelectedItem as NavViewItem).Content.ToString();
                if(h == "Home".ToLocalizedString())
                {
                    MainFrame.Content = HomePage;
                }
                else if (h == "Store".ToLocalizedString())
                {
                    
                }
                else if (h == "Discord".ToLocalizedString())
                {

                }
                else if (h == "Tasks".ToLocalizedString())
                {

                }
                else if (h == "Logs".ToLocalizedString())
                {

                }
                (NavView.Header as NavViewHeader).HeaderText = h;
                (NavView.Header as NavViewHeader).HeaderMargin = GetNavViewHeaderMargin();
            }
        }
    }
}
