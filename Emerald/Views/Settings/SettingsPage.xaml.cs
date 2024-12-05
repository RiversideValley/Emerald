using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using System;

namespace Emerald.Views.Settings;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();

        Loaded += (_, _) => 
                NavigateOnce(typeof(GeneralPage));
    }

    private void navView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        Navigate(navView.SelectedItem as NavigationViewItem);
    }

    private void Navigate(NavigationViewItem itm)
    {
        switch (itm.Tag)
        {
            case "Appearance":
                NavigateOnce(typeof(AppearancePage));
                break;
            case "About":
                throw new NotImplementedException();
                break;
            default:
                NavigateOnce(typeof(GeneralPage));
                break;
        }
    }

    private void NavigateOnce(Type type)
    {
        if (contentframe.Content == null || contentframe.Content.GetType() != type)
        {
            contentframe.Navigate(type, null, new DrillInNavigationTransitionInfo());
        }
    }
}
