using CommonServiceLocator;
using Emerald.Helpers;
using Emerald.Models;
using Emerald.Views.Settings;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.UI;

namespace Emerald;

public sealed partial class MainPage : Page
{
    private readonly Services.SettingsService SS;
    public MainPage()
    {
        SS = ServiceLocator.Current.GetInstance<Services.SettingsService>();
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

    void InitializeAppearance()
    {
        SS.Settings.App.Appearance.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != null)
            {
                TintColor();
                this.GetThemeService().SetThemeAsync((AppTheme)SS.Settings.App.Appearance.Theme);
            }
        };
        void TintColor()
        {
            switch ((Helpers.Settings.Enums.MicaTintColor)SS.Settings.App.Appearance.MicaTintColor)
            {
                case Helpers.Settings.Enums.MicaTintColor.NoColor:
                    MainGrid.Background = null;
                    break;
                case Helpers.Settings.Enums.MicaTintColor.AccentColor:
                    MainGrid.Background = new SolidColorBrush((Color)Application.Current.Resources["SystemAccentColor"])
                    {
                        Opacity = (double)SS.Settings.App.Appearance.TintOpacity / 100
                    };
                    break;
                case Helpers.Settings.Enums.MicaTintColor.CustomColor:
                    var c = SS.Settings.App.Appearance.CustomMicaTintColor;
                    MainGrid.Background = new SolidColorBrush()
                    {
                        Color = c ?? Color.FromArgb(255, 234, 0, 94),
                        Opacity = (double)SS.Settings.App.Appearance.TintOpacity / 100
                    };
                    break;
            }
        }
        TintColor();
        this.GetThemeService().SetThemeAsync((AppTheme)SS.Settings.App.Appearance.Theme);

        //Mica (Windows 11)
        var mica = WindowManager.IntializeWindow(App.Current.MainWindow);
#if WINDOWS
        if (mica != null)
        {
            mica.MicaController.Kind = (MicaKind)SS.Settings.App.Appearance.MicaType;
            SS.Settings.App.Appearance.PropertyChanged += (s, e)
                => mica.MicaController.Kind = (MicaKind)SS.Settings.App.Appearance.MicaType;
        }
#endif
    }
    void InitializeNavView()
    {

        NavView.MenuItems.Add(new SquareNavigationViewItem("Home".Localize())
        {
            Thumbnail = "ms-appx:///Assets/NavigationViewIcons/home.png",
            FontIconGlyph = "\xE80F",
            Tag = "Home",
            SolidFontIconGlyph = "\xEA8A",
            IsSelected = true
        });
        NavView.MenuItems.Add(new SquareNavigationViewItem("Store".Localize())
        {
            Thumbnail = "ms-appx:///Assets/NavigationViewIcons/store.png",
            Tag = "Store",
            FontIconGlyph = "\xE7BF",
            SolidFontIconGlyph = "\xE7BF",
            IsSelected = false
        });
        NavView.MenuItems.Add(new SquareNavigationViewItem("News".Localize())
        {
            Thumbnail = "ms-appx:///Assets/NavigationViewIcons/news.png",
            Tag = "News",
            FontIconGlyph = "\xF57E",
            SolidFontIconGlyph = "\xF57E",
            IsSelected = false
        });

        NavView.FooterMenuItems.Add(new SquareNavigationViewItem("Tasks".Localize())
        {
            Thumbnail = "ms-appx:///Assets/NavigationViewIcons/tasks.png",
            Tag = "Tasks",
            FontIconGlyph = "\xE9D5",
            SolidFontIconGlyph = "\xE9D5",
            IsSelected = false
        });
        NavView.FooterMenuItems.Add(new SquareNavigationViewItem("Logs".Localize())
        {
            Thumbnail = "ms-appx:///Assets/NavigationViewIcons/logs.png",
            Tag = "Logs",
            FontIconGlyph = "\xE756",
            SolidFontIconGlyph = "\xE756",
            IsSelected = false
        });
        NavView.FooterMenuItems.Add(new SquareNavigationViewItem("Settings".Localize())
        {
            Thumbnail = "ms-appx:///Assets/NavigationViewIcons/settings.png",
            Tag = "Settings",
            FontIconGlyph = "\xE713",
            SolidFontIconGlyph = "\xE713",
            IsSelected = false
        });

        NavView.SelectedItem = NavView.MenuItems[0];

        NavView.Header = new NavViewHeader() { HeaderText = "Home".Localize(), HeaderMargin = GetNavViewHeaderMargin() };
        NavView.DisplayModeChanged += (_, _) => (NavView.Header as NavViewHeader).HeaderMargin = GetNavViewHeaderMargin();
        Navigate(NavView.SelectedItem as SquareNavigationViewItem);

        MessageBox.Show("```markdown\r\n---\r\ntitle: \"Complex Markdown Test\"\r\nauthor: \"Your Name\"\r\ndate: 2025-02-04\r\n---\r\n\r\n# H1 Heading\r\n\r\nThis is a **bold** text, and this is an *italic* text, plus an inline `code`.\r\n\r\nHere's a link: [OpenAI](https://www.openai.com).\r\n\r\n## H2 with a footnote\r\n\r\nFootnote reference[^1].\r\n\r\n[^1]: This is the footnote text.\r\n\r\n### H3 with a table\r\n\r\n| Column 1 | Column 2 | Column 3 |\r\n|----------|----------|----------|\r\n| A1       | B1       | C1       |\r\n| A2       | B2       | C2       |\r\n\r\n#### H4 with a quote\r\n\r\n> This is a blockquote  \r\n> spanning multiple lines\r\n\r\n##### H5 with a list\r\n\r\n1. First\r\n2. Second\r\n3. Third\r\n\r\n- A bullet\r\n- Another bullet\r\n  - A nested bullet\r\n\r\n###### H6 with tasks\r\n\r\n- [ ] Task one\r\n- [x] Task two\r\n- [ ] Task three\r\n\r\n---\r\n\r\n### Code block with syntax\r\n\r\n```csharp\r\npublic class Example {\r\n    public void Hello() {\r\n        Console.WriteLine(\"Hello, world!\");\r\n    }\r\n}\r\n```\r\n\r\n### Another block with math\r\n\r\n$$\r\n\\alpha + \\beta = \\gamma\r\n$$\r\n\r\n### Definition list\r\n\r\nTerm 1\r\n: Definition A\r\n\r\nTerm 2\r\n: Definition B\r\n\r\n### Custom Container\r\n\r\n::: warning\r\nThis is a custom container block.\r\n:::\r\n\r\n### Image\r\n\r\n![Markdig Logo](https://raw.githubusercontent.com/lunet-io/markdig/master/docfx/images/logo.png)\r\n\r\n### Emoji\r\n\r\nI love :coffee: and :sparkles: !\r\n```");
    }
    private void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
#if WINDOWS
        Emerald.Helpers.WindowManager.SetTitleBar(App.Current.MainWindow, AppTitleBar);
#endif
        InitializeAppearance();
        InitializeNavView();
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
