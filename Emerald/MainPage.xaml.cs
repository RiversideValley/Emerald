using System.Collections.Specialized;
using System.ComponentModel;
using System.Security.Cryptography.X509Certificates;
using Windows.ApplicationModel.Core;
using CommonServiceLocator;
using CommunityToolkit.Mvvm.DependencyInjection;
using Emerald.CoreX.Helpers;
using Emerald.CoreX.Installers;
using Emerald.CoreX.Models;
using Emerald.CoreX.Services;
using Emerald.Helpers;
using Emerald.Models;
using Emerald.Views;
using Emerald.Views.Settings;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.UI;
using Emerald.CoreX.Versions;

namespace Emerald;

/// <summary>
/// Hosts the main shell navigation, appearance initialization, and top-level page routing.
/// </summary>
public sealed partial class MainPage : Page
{
    private readonly Services.SettingsService SS;
    private readonly IAccountService _accountService;
    private readonly HashSet<EAccount> _trackedAccounts = new();
    private SquareNavigationViewItem? _accountsNavigationItem;

    public MainPage()
    {
        SS = Ioc.Default.GetService<Services.SettingsService>();
        _accountService = Ioc.Default.GetService<IAccountService>();
        this.InitializeComponent();
        this.Loaded += MainPage_Loaded;
        NavView.ItemInvoked += MainNavigationView_ItemInvoked;
        _accountService.Accounts.CollectionChanged += Accounts_CollectionChanged;
        SyncTrackedAccountHandlers();
        this.Log().LogInformation("Main page initialized.");
    }

    private void MainNavigationView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (!args.IsSettingsInvoked && NavView.SelectedItem is SquareNavigationViewItem itm)
        {
            this.Log().LogDebug("Navigation view item invoked: {Tag}.", itm.Tag);
            itm.InvokePropertyChanged();
        }
    }

    /// <summary>
    /// Applies theme and window appearance settings to the active shell.
    /// </summary>
    void InitializeAppearance()
    {
        this.Log().LogInformation("Initializing shell appearance.");
        SS.Settings.App.Appearance.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != null)
            {
                this.Log().LogDebug("Applying appearance change for property {PropertyName}.", e.PropertyName);
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
                    this.Log().LogDebug("Cleared custom Mica tint background.");
                    break;
                case Helpers.Settings.Enums.MicaTintColor.AccentColor:
                    MainGrid.Background = new SolidColorBrush((Color)Application.Current.Resources["SystemAccentColor"])
                    {
                        Opacity = (double)SS.Settings.App.Appearance.TintOpacity / 100
                    };
                    this.Log().LogDebug("Applied accent Mica tint background. Opacity: {Opacity}.", SS.Settings.App.Appearance.TintOpacity);
                    break;
                case Helpers.Settings.Enums.MicaTintColor.CustomColor:
                    var c = SS.Settings.App.Appearance.CustomMicaTintColor;
                    MainGrid.Background = new SolidColorBrush()
                    {
                        Color = c ?? Color.FromArgb(255, 234, 0, 94),
                        Opacity = (double)SS.Settings.App.Appearance.TintOpacity / 100
                    };
                    this.Log().LogDebug("Applied custom Mica tint background. HasCustomColor: {HasCustomColor}.", c != null);
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
            this.Log().LogInformation("Mica backdrop initialized for the main window.");
            mica.MicaController.Kind = (MicaKind)SS.Settings.App.Appearance.MicaType;
            SS.Settings.App.Appearance.PropertyChanged += (s, e)
                => mica.MicaController.Kind = (MicaKind)SS.Settings.App.Appearance.MicaType;
        }
        else
        {
            this.Log().LogDebug("Mica backdrop was not initialized for the main window.");
        }
#endif
    }

    /// <summary>
    /// Populates the main navigation view and selects the default route.
    /// </summary>
    void InitializeNavView()
    {
        this.Log().LogInformation("Initializing main navigation view.");
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
        _accountsNavigationItem = new SquareNavigationViewItem(GetAccountsNavigationItemName())
        {
            Thumbnail = "ms-appx:///Assets/NavigationViewIcons/store.png",
            Tag = "Accounts",
            FontIconGlyph = "\xE77B",
            SolidFontIconGlyph = "\xE77B",
            IsSelected = false
        };
        NavView.FooterMenuItems.Add(_accountsNavigationItem);
        NavView.FooterMenuItems.Add(new SquareNavigationViewItem("Settings".Localize())
        {
            Thumbnail = "ms-appx:///Assets/NavigationViewIcons/settings.png",
            Tag = "Settings",
            FontIconGlyph = "\xE713",
            SolidFontIconGlyph = "\xE713",
            IsSelected = false
        });

        NavView.SelectedItem = NavView.MenuItems[0];

        NavView.Header = new NavViewHeader() { HeaderText = GetNavigationHeaderText(NavView.SelectedItem as SquareNavigationViewItem), HeaderMargin = GetNavViewHeaderMargin() };
        NavView.DisplayModeChanged += (_, _) => (NavView.Header as NavViewHeader).HeaderMargin = GetNavViewHeaderMargin();
        Navigate(NavView.SelectedItem as SquareNavigationViewItem);
    }

    private async void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        this.Log().LogInformation("Main page loaded.");
        Emerald.Helpers.WindowManager.SetTitleBar(App.Current.MainWindow, AppTitleBar);

        InitializeAppearance();
        InitializeNavView();
        await LoadAccountsAsync();
        this.Loaded -= MainPage_Loaded;
    }

    /// <summary>
    /// Returns the header margin that matches the current navigation view display mode.
    /// </summary>
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
    private void navView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        Navigate(NavView.SelectedItem as SquareNavigationViewItem);
    }

    /// <summary>
    /// Navigates to the item whose tag matches the supplied value.
    /// </summary>
    public void NavigateToTag(string tag, object? parameter = null)
    {
        var items = NavView.MenuItems.Cast<object>().Concat(NavView.FooterMenuItems.Cast<object>());
        var target = items
            .OfType<SquareNavigationViewItem>()
            .FirstOrDefault(item => string.Equals(item.Tag as string, tag, StringComparison.OrdinalIgnoreCase));

        if (target == null)
        {
            this.Log().LogWarning("Could not navigate because no navigation item matched tag {Tag}.", tag);
            return;
        }

        this.Log().LogInformation("Navigating to tag {Tag}.", tag);
        NavView.SelectedItem = target;
        Navigate(target, parameter);
    }

    /// <summary>
    /// Navigates to the page represented by the supplied navigation item.
    /// </summary>
    private void Navigate(SquareNavigationViewItem itm, object? parameter = null)
    {
        if (itm == null)
        {
            this.Log().LogWarning("Skipping navigation because the navigation item was null.");
            return;
        }

        this.Log().LogInformation("Navigating shell to {Tag}.", itm.Tag);

        switch (itm.Tag)
        {
            case "Tasks":
                frame.Content = new Page { Content = new UserControls.NotificationListControl() };
                break;
            case "Home":
                NavigateOnce(typeof(GamesPage), parameter);
                break;
            case "Accounts":
                NavigateOnce(typeof(AccountsPage), parameter);
                break;
            case "Logs":
                NavigateOnce(typeof(LogsPage), parameter, forceNavigate: parameter != null);
                break;
            default:
                NavigateOnce(typeof(SettingsPage), parameter);
                break;
        }
        (NavView.Header as NavViewHeader).HeaderText = GetNavigationHeaderText(itm);
        (NavView.Header as NavViewHeader).HeaderMargin = GetNavViewHeaderMargin();
    }

    /// <summary>
    /// Navigates the shared frame only when the target page is different or navigation is forced.
    /// </summary>
    private void NavigateOnce(System.Type type, object? parameter = null, bool forceNavigate = false)
    {
        if (forceNavigate || frame.Content == null || frame.Content.GetType() != type)
        {
            this.Log().LogDebug("Navigating frame to {PageType}. ForceNavigate: {ForceNavigate}.", type.Name, forceNavigate);
            frame.Navigate(type, parameter, new EntranceNavigationTransitionInfo());
        }
    }

    private async Task LoadAccountsAsync()
    {
        try
        {
            await _accountService.LoadAllAccountsAsync();
            UpdateAccountsNavigationItem();
        }
        catch (Exception ex)
        {
            this.Log().LogWarning(ex, "Failed to preload accounts for shell navigation.");
        }
    }

    private void Accounts_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SyncTrackedAccountHandlers();
        UpdateAccountsNavigationItem();
    }

    private void Account_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(EAccount.IsSelected) or nameof(EAccount.Name))
        {
            UpdateAccountsNavigationItem();
        }
    }

    private void SyncTrackedAccountHandlers()
    {
        var currentAccounts = _accountService.Accounts.ToHashSet();

        foreach (var trackedAccount in _trackedAccounts.Where(account => !currentAccounts.Contains(account)).ToList())
        {
            trackedAccount.PropertyChanged -= Account_PropertyChanged;
            _trackedAccounts.Remove(trackedAccount);
        }

        foreach (var account in currentAccounts.Where(account => !_trackedAccounts.Contains(account)))
        {
            account.PropertyChanged += Account_PropertyChanged;
            _trackedAccounts.Add(account);
        }
    }

    private void UpdateAccountsNavigationItem()
    {
        if (_accountsNavigationItem == null)
        {
            return;
        }

        _accountsNavigationItem.Name = GetAccountsNavigationItemName();

        if (ReferenceEquals(NavView.SelectedItem, _accountsNavigationItem) && NavView.Header is NavViewHeader header)
        {
            header.HeaderText = GetNavigationHeaderText(_accountsNavigationItem);
            header.HeaderMargin = GetNavViewHeaderMargin();
        }
    }

    private string GetAccountsNavigationItemName()
    {
        var selectedAccount = _accountService.GetSelectedAccount();
        return string.IsNullOrWhiteSpace(selectedAccount?.Name)
            ? "Account".Localize()
            : selectedAccount.Name;
    }

    private string GetNavigationHeaderText(SquareNavigationViewItem? item)
    {
        if (item == null)
        {
            return string.Empty;
        }

        return item.Tag switch
        {
            "Accounts" => "Accounts".Localize(),
            "Tasks" => (NavView.Header as NavViewHeader)?.HeaderText ?? item.Name,
            _ => item.Name
        };
    }
}
