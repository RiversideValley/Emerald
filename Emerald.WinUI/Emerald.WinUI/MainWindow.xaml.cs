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
// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Emerald.WinUI
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            Title = "Emerald";
            Initialize();
        }
        public void Initialize()
        {
            NavView.Header = new NavViewHeader() { HeaderText = "Deploy", HeaderMargin = GetNavViewHeaderMargin() };
            NavView.DisplayModeChanged += (_, _) => (NavView.Header as NavViewHeader).HeaderMargin = GetNavViewHeaderMargin();
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
                var h = (NavView.SelectedItem as UserControls.NavViewItem).Content.ToString();
                switch (h)
                {
                    case "Deploy":
                        break;
                    case "Store":
                        break;
                    case "Discord":
                        break;
                    case "Logs":
                        break;
                    default:
                        break;
                }
                (NavView.Header as NavViewHeader).HeaderText = h;
                (NavView.Header as NavViewHeader).HeaderMargin = GetNavViewHeaderMargin();
            }
        }
    }
}
