using Emerald.Helpers;
using Emerald.Models;
using Emerald.Views.Settings;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

namespace Emerald;

public sealed partial class MainPage : Page
{
    public MainPage()
    {
        this.InitializeComponent();
        this.Loaded += MainPage_Loaded;
        NavView.ItemInvoked += MainNavigationView_ItemInvoked;
    }
    private void MainNavigationView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (!args.IsSettingsInvoked && NavView.SelectedItem is SquareNavigationViewItem itm)
        {
            itm.InvokePropertyChanged();
        }
    }
    private void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
#if WINDOWS
        Emerald.Helpers.WindowManager.SetTitleBar(App.Current.MainWindow, AppTitleBar);
#endif
        NavView.MenuItems.Add(new SquareNavigationViewItem("Home".Localize())
        {
            FontIconGlyph = "\xE80F",
            Tag = "Home",
            SolidFontIconGlyph = "\xEA8A",
            IsSelected = true,
            ShowFontIcons = true
        });
        NavView.MenuItems.Add(new SquareNavigationViewItem("Store".Localize())
        {
            Tag = "Store",
            FontIconGlyph = "\xE7BF",
            SolidFontIconGlyph = "\xE7BF",
            IsSelected = false,
            ShowFontIcons = true
        });
        NavView.MenuItems.Add(new SquareNavigationViewItem("News".Localize())
        {
            Tag = "News",
            FontIconGlyph = "\xF57E",
            SolidFontIconGlyph = "\xF57E",
            IsSelected = false,
            ShowFontIcons = true
        });

        NavView.FooterMenuItems.Add(new SquareNavigationViewItem("Tasks".Localize())
        {
            Tag = "Tasks",
            FontIconGlyph = "\xE9D5",
            SolidFontIconGlyph = "\xE9D5",
            IsSelected = false,
            ShowFontIcons = true
        });
        NavView.FooterMenuItems.Add(new SquareNavigationViewItem("Logs".Localize())
        {
            Tag = "Logs",
            FontIconGlyph = "\xE756",
            SolidFontIconGlyph = "\xE756",
            IsSelected = false,
            ShowFontIcons = true
        });
        NavView.FooterMenuItems.Add(new SquareNavigationViewItem("Settings".Localize())
        {
            Tag = "Settings",
            FontIconGlyph = "\xE713",
            SolidFontIconGlyph = "\xE713",
            IsSelected = false,
            ShowFontIcons = true
        });

        NavView.SelectedItem = NavView.MenuItems[0];

        NavView.Header = new NavViewHeader() { HeaderText = "Home".Localize(), HeaderMargin = GetNavViewHeaderMargin() };
        NavView.DisplayModeChanged += (_, _) => (NavView.Header as NavViewHeader).HeaderMargin = GetNavViewHeaderMargin();
        Navigate(NavView.SelectedItem as SquareNavigationViewItem);
    }

    private  Thickness GetNavViewHeaderMargin()
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
    private void navView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        Navigate(NavView.SelectedItem as SquareNavigationViewItem);
    }

    private void Navigate(SquareNavigationViewItem itm)
    {
        switch (itm.Tag)
        {
            default:
                NavigateOnce(typeof(SettingsPage));
                break;
        }
        (NavView.Header as NavViewHeader).HeaderText = itm.Tag == "Tasks" ? (NavView.Header as NavViewHeader).HeaderText : itm.Name;
        (NavView.Header as NavViewHeader).HeaderMargin = GetNavViewHeaderMargin();

    }

    private void NavigateOnce(Type type)
    {

        if (frame.Content == null || frame.Content.GetType() != type)
        {
            frame.Navigate(type, null, new EntranceNavigationTransitionInfo());
        }
    }
}
