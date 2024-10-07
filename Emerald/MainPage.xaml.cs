using Emerald.Uno.Helpers;
using Emerald.Uno.Models;

namespace Emerald;

public sealed partial class MainPage : Page
{
    public MainPage()
    {
        this.InitializeComponent();
        this.Loaded += MainPage_Loaded;
    }

    private void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
#if WINDOWS
        Emerald.Uno.Helpers.WindowManager.SetTitleBar(App.Current.MainWindow, AppTitleBar);
#endif
NavView.MenuItems.Add(new SquareNavigationViewItem("Home".Localize()){
 FontIconGlyph = "\xE80F",
 SolidFontIconGlyph = "\xEA8A",
 IsSelected = true,
 ShowFontIcons = true
});
NavView.MenuItems.Add(new SquareNavigationViewItem("Store".Localize()){
 FontIconGlyph = "\xE7BF",
 SolidFontIconGlyph = "\xE7BF",
 IsSelected = false,
 ShowFontIcons = true
});
NavView.MenuItems.Add(new SquareNavigationViewItem("News".Localize()){
 FontIconGlyph = "\xF57E",
 SolidFontIconGlyph = "\xF57E",
 IsSelected = false,
 ShowFontIcons = true
});

NavView.FooterMenuItems.Add(new SquareNavigationViewItem("Tasks".Localize()){
 FontIconGlyph = "\xE9D5",
 SolidFontIconGlyph = "\xE9D5",
 IsSelected = false,
 ShowFontIcons = true
});
NavView.FooterMenuItems.Add(new SquareNavigationViewItem("Logs".Localize()){
 FontIconGlyph = "\xE756",
 SolidFontIconGlyph = "\xE756",
 IsSelected = false,
 ShowFontIcons = true
});
NavView.FooterMenuItems.Add(new SquareNavigationViewItem("Settings".Localize()){
 FontIconGlyph = "\xE713",
 SolidFontIconGlyph = "\xE713",
 IsSelected = false,
 ShowFontIcons = true
});

NavView.SelectedItem = NavView.MenuItems[0];
    }
}
