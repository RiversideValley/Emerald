using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using System;

namespace Emerald.Views.Settings;

public sealed partial class SettingsPage : Page
{
    private CrashReportsNavigationRequest? _pendingCrashReportsNavigation;

    public SettingsPage()
    {
        InitializeComponent();

        Loaded += SettingsPage_Loaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _pendingCrashReportsNavigation = e.Parameter as CrashReportsNavigationRequest;
    }

    private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (_pendingCrashReportsNavigation is not null)
        {
            var aboutItem = navView.MenuItems
                .OfType<NavigationViewItem>()
                .FirstOrDefault(item => string.Equals(item.Tag as string, "About", StringComparison.OrdinalIgnoreCase));
            navView.SelectedItem = aboutItem;
            NavigateOnce(typeof(CrashReportsPage), _pendingCrashReportsNavigation.ReportId);
            return;
        }

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
                NavigateOnce(typeof(AboutPage));
                break;
            default:
                NavigateOnce(typeof(GeneralPage));
                break;
        }
    }

    private void NavigateOnce(Type type, object? parameter = null)
    {
        ConfigureContentScrolling(type == typeof(CrashReportsPage));

        if (contentframe.Content == null || contentframe.Content.GetType() != type)
        {
            contentframe.Navigate(type, parameter, new DrillInNavigationTransitionInfo());
        }
    }

    private void ConfigureContentScrolling(bool usePageLevelScrolling)
    {
        contentScrollViewer.VerticalScrollMode = usePageLevelScrolling
            ? ScrollMode.Disabled
            : ScrollMode.Enabled;
        contentScrollViewer.VerticalScrollBarVisibility = usePageLevelScrolling
            ? ScrollBarVisibility.Disabled
            : ScrollBarVisibility.Auto;
    }
}

public sealed record CrashReportsNavigationRequest(string? ReportId);
