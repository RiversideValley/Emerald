using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using CommonServiceLocator;
using CommunityToolkit.Mvvm.DependencyInjection;
using Emerald.CoreX.Helpers;
using Emerald.CoreX.Models;
using Emerald.CoreX.Notifications;
using Emerald.CoreX.Services;
using Emerald.Helpers;
using Emerald.Models;
using Emerald.Views;
using Emerald.Views.Settings;
using Emerald.Views.Store;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.UI;

namespace Emerald;

/// <summary>
/// Hosts the main shell navigation, appearance initialization, and top-level page routing.
/// </summary>
public sealed partial class MainPage : Page
{
    private readonly Services.SettingsService SS;
    private readonly IAccountService _accountService;
    private readonly INotificationService _notificationService;
    private readonly HashSet<EAccount> _trackedAccounts = [];
    private readonly Dictionary<string, NotificationType> _notificationTypeSnapshot = new(StringComparer.Ordinal);
    private readonly ObservableCollection<TaskToastItem> _taskToasts = [];

    private SquareNavigationViewItem? _accountsNavigationItem;
    private SquareNavigationViewItem? _tasksNavigationItem;
    private SquareNavigationViewItem? _lastNonTaskNavigationItem;
    private InfoBadge? _tasksInfoBadge;
    private Flyout? _tasksFlyout;
    private int _pendingTaskUpdates;
    private bool _isTasksFlyoutOpen;

    public ObservableCollection<TaskToastItem> TaskToasts => _taskToasts;

    public MainPage()
    {
        SS = Ioc.Default.GetService<Services.SettingsService>()
             ?? throw new InvalidOperationException("Settings service is not available.");
        _accountService = Ioc.Default.GetService<IAccountService>()
                          ?? throw new InvalidOperationException("Account service is not available.");
        _notificationService = Ioc.Default.GetService<INotificationService>()
                               ?? throw new InvalidOperationException("Notification service is not available.");

        InitializeComponent();
        Loaded += MainPage_Loaded;
        NavView.ItemInvoked += MainNavigationView_ItemInvoked;

        _accountService.Accounts.CollectionChanged += Accounts_CollectionChanged;
        SyncTrackedAccountHandlers();

        foreach (var notification in _notificationService.ActiveNotifications)
        {
            TrackNotification(notification);
        }

        _notificationService.ActiveNotifications.CollectionChanged += ActiveNotifications_CollectionChanged;

        this.Log().LogInformation("Main page initialized.");
    }

    private void MainNavigationView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (!args.IsSettingsInvoked && NavView.SelectedItem is SquareNavigationViewItem item)
        {
            this.Log().LogDebug("Navigation view item invoked: {Tag}.", item.Tag);
            item.InvokePropertyChanged();
        }
    }

    /// <summary>
    /// Applies theme and window appearance settings to the active shell.
    /// </summary>
    private void InitializeAppearance()
    {
        this.Log().LogInformation("Initializing shell appearance.");
        SS.Settings.App.Appearance.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is null)
            {
                return;
            }

            this.Log().LogDebug("Applying appearance change for property {PropertyName}.", e.PropertyName);
            TintColor();
            _ = this.GetThemeService().SetThemeAsync((AppTheme)SS.Settings.App.Appearance.Theme);
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
                    var customColor = SS.Settings.App.Appearance.CustomMicaTintColor;
                    MainGrid.Background = new SolidColorBrush
                    {
                        Color = customColor ?? Color.FromArgb(255, 234, 0, 94),
                        Opacity = (double)SS.Settings.App.Appearance.TintOpacity / 100
                    };
                    this.Log().LogDebug("Applied custom Mica tint background. HasCustomColor: {HasCustomColor}.", customColor != null);
                    break;
            }
        }

        TintColor();
        _ = this.GetThemeService().SetThemeAsync((AppTheme)SS.Settings.App.Appearance.Theme);

        var mica = WindowManager.IntializeWindow(App.Current.MainWindow);
#if WINDOWS
        if (mica != null)
        {
            this.Log().LogInformation("Mica backdrop initialized for the main window.");
            mica.MicaController.Kind = (MicaKind)SS.Settings.App.Appearance.MicaType;
            SS.Settings.App.Appearance.PropertyChanged += (_, _)
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
    private void InitializeNavView()
    {
        this.Log().LogInformation("Initializing main navigation view.");

        var homeNavigationItem = new SquareNavigationViewItem("Home".Localize())
        {
            Thumbnail = "ms-appx:///Assets/NavigationViewIcons/home.png",
            FontIconGlyph = "\xE80F",
            Tag = "Home",
            SolidFontIconGlyph = "\xEA8A",
            IsSelected = true
        };
        NavView.MenuItems.Add(homeNavigationItem);

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

        _tasksInfoBadge = new InfoBadge
        {
            Value = 0,
            Visibility = Visibility.Collapsed
        };
        _tasksNavigationItem = new SquareNavigationViewItem("Tasks".Localize())
        {
            Thumbnail = "ms-appx:///Assets/NavigationViewIcons/tasks.png",
            Tag = "Tasks",
            FontIconGlyph = "\xE9D5",
            SolidFontIconGlyph = "\xE9D5",
            InfoBadge = _tasksInfoBadge,
            IsSelected = false
        };
        NavView.FooterMenuItems.Add(_tasksNavigationItem);

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

        NavView.SelectedItem = homeNavigationItem;
        _lastNonTaskNavigationItem = homeNavigationItem;

        NavView.Header = new NavViewHeader
        {
            HeaderText = GetNavigationHeaderText(homeNavigationItem),
            HeaderMargin = GetNavViewHeaderMargin()
        };
        NavView.DisplayModeChanged += (_, _) =>
        {
            if (NavView.Header is NavViewHeader header)
            {
                header.HeaderMargin = GetNavViewHeaderMargin();
            }
        };

        Navigate(homeNavigationItem);
    }

    private async void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        this.Log().LogInformation("Main page loaded.");
        Emerald.Helpers.WindowManager.SetTitleBar(App.Current.MainWindow, AppTitleBar);

        InitializeAppearance();
        InitializeNavView();
        await LoadAccountsAsync();
        Loaded -= MainPage_Loaded;
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

        NavView.IsPaneToggleButtonVisible = false;
        return new Thickness(-30, -20, 0, 10);
    }

    private void navView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        Navigate(NavView.SelectedItem as SquareNavigationViewItem, invokedContainer: args.InvokedItemContainer as FrameworkElement);
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
    private void Navigate(SquareNavigationViewItem? item, object? parameter = null, FrameworkElement? invokedContainer = null)
    {
        if (item == null)
        {
            this.Log().LogWarning("Skipping navigation because the navigation item was null.");
            return;
        }

        this.Log().LogInformation("Navigating shell to {Tag}.", item.Tag);

        if (string.Equals(item.Tag as string, "Tasks", StringComparison.Ordinal))
        {
            if (SS.Settings.App.Tasks.CompactMode)
            {
                ShowTasksFlyout(invokedContainer);
                MarkTasksAsSeen();
                RestoreSelectionAfterCompactTasks();
                return;
            }

            HideTasksFlyout();
            NavigateOnce(typeof(TasksPage), parameter);
            MarkTasksAsSeen();
            UpdateHeader(item);
            return;
        }

        HideTasksFlyout();
        _lastNonTaskNavigationItem = item;

        switch (item.Tag)
        {
            case "Home":
                NavigateOnce(typeof(GamesPage), parameter);
                break;
            case "Accounts":
                NavigateOnce(typeof(AccountsPage), parameter);
                break;
            case "Logs":
                NavigateOnce(typeof(LogsPage), parameter, forceNavigate: parameter != null);
                break;
            case "Store":
                NavigateOnce(typeof(ModrinthStorePage), parameter, forceNavigate: parameter != null);
                break;
            default:
                NavigateOnce(typeof(SettingsPage), parameter);
                break;
        }

        UpdateHeader(item);
    }

    /// <summary>
    /// Called by task surfaces when the compact mode setting changes.
    /// </summary>
    public void HandleTasksModeChanged(bool compactModeEnabled)
    {
        if (compactModeEnabled)
        {
            if (frame.Content is TasksPage)
            {
                var fallback = _lastNonTaskNavigationItem
                               ?? NavView.MenuItems.OfType<SquareNavigationViewItem>().FirstOrDefault();
                if (fallback != null)
                {
                    NavView.SelectedItem = fallback;
                    Navigate(fallback);
                }
            }

            NavigateToTag("Tasks");
            return;
        }

        HideTasksFlyout();
        NavigateToTag("Tasks");
    }

    /// <summary>
    /// Navigates the shared frame only when the target page is different or navigation is forced.
    /// </summary>
    private void NavigateOnce(Type type, object? parameter = null, bool forceNavigate = false)
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

        if (ReferenceEquals(NavView.SelectedItem, _accountsNavigationItem))
        {
            UpdateHeader(_accountsNavigationItem);
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
            _ => item.Name
        };
    }

    private void UpdateHeader(SquareNavigationViewItem item)
    {
        if (NavView.Header is not NavViewHeader header)
        {
            return;
        }

        header.HeaderText = GetNavigationHeaderText(item);
        header.HeaderMargin = GetNavViewHeaderMargin();
    }

    private void EnsureTasksFlyout()
    {
        if (_tasksFlyout != null)
        {
            return;
        }

        var tasksPanel = new UserControls.TasksPanelControl
        {
            IsCompactHost = true,
            Width = 380,
            MinHeight = 280,
            MaxHeight = 520
        };

        _tasksFlyout = new Flyout
        {
            Content = tasksPanel,
            Placement = FlyoutPlacementMode.Right
        };
        _tasksFlyout.Closed += TasksFlyout_Closed;
    }

    private void ShowTasksFlyout(FrameworkElement? invokedContainer)
    {
        EnsureTasksFlyout();
        if (_tasksFlyout == null)
        {
            return;
        }

        var target = invokedContainer ?? NavView;
        _tasksFlyout.ShowAt(target, new FlyoutShowOptions
        {
            Placement = FlyoutPlacementMode.Right,
            ShowMode = FlyoutShowMode.Standard
        });
        _isTasksFlyoutOpen = true;
    }

    private void HideTasksFlyout()
    {
        if (_tasksFlyout?.IsOpen == true)
        {
            _tasksFlyout.Hide();
        }

        _isTasksFlyoutOpen = false;
    }

    private void TasksFlyout_Closed(object? sender, object e)
    {
        _isTasksFlyoutOpen = false;
    }

    private void RestoreSelectionAfterCompactTasks()
    {
        var fallback = _lastNonTaskNavigationItem
                       ?? NavView.MenuItems.OfType<SquareNavigationViewItem>().FirstOrDefault();

        if (fallback == null)
        {
            return;
        }

        NavView.SelectedItem = fallback;
        UpdateHeader(fallback);
    }

    private void ActiveNotifications_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (Notification notification in e.NewItems)
            {
                TrackNotification(notification);
                HandleTaskAdded(notification);
            }
        }

        if (e.OldItems != null)
        {
            foreach (Notification notification in e.OldItems)
            {
                UntrackNotification(notification);
            }
        }
    }

    private void TrackNotification(Notification notification)
    {
        if (!string.IsNullOrWhiteSpace(notification.Id))
        {
            _notificationTypeSnapshot[notification.Id] = notification.Type;
        }

        notification.PropertyChanged += Notification_PropertyChanged;
    }

    private void UntrackNotification(Notification notification)
    {
        notification.PropertyChanged -= Notification_PropertyChanged;

        if (!string.IsNullOrWhiteSpace(notification.Id))
        {
            _notificationTypeSnapshot.Remove(notification.Id);
        }
    }

    private void Notification_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not Notification notification || e.PropertyName != nameof(Notification.Type))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(notification.Id))
        {
            return;
        }

        if (!_notificationTypeSnapshot.TryGetValue(notification.Id, out var previousType))
        {
            _notificationTypeSnapshot[notification.Id] = notification.Type;
            return;
        }

        if (previousType == notification.Type)
        {
            return;
        }

        _notificationTypeSnapshot[notification.Id] = notification.Type;
        HandleTaskSeverityChanged(notification, previousType);
    }

    private void HandleTaskAdded(Notification notification)
    {
        ShowTaskToast(notification, severityChanged: false, previousType: null);
        RegisterPendingTaskUpdate();
    }

    private void HandleTaskSeverityChanged(Notification notification, NotificationType previousType)
    {
        ShowTaskToast(notification, severityChanged: true, previousType: previousType);
        RegisterPendingTaskUpdate();
    }

    private void ShowTaskToast(Notification notification, bool severityChanged, NotificationType? previousType)
    {
        var title = string.IsNullOrWhiteSpace(notification.Title)
            ? "Tasks".Localize()
            : notification.Title;

        var message = notification.Message;
        if (severityChanged && string.IsNullOrWhiteSpace(message) && previousType != null)
        {
            message = $"{previousType} -> {notification.Type}";
        }

        _taskToasts.Insert(0, new TaskToastItem(
            Guid.NewGuid().ToString("N"),
            title,
            message,
            notification.Type));
    }

    private void RegisterPendingTaskUpdate()
    {
        if (IsTasksCurrentlyVisible())
        {
            return;
        }

        _pendingTaskUpdates++;
        UpdateTasksInfoBadge();
    }

    private bool IsTasksCurrentlyVisible()
        => _isTasksFlyoutOpen || frame.Content is TasksPage;

    private void MarkTasksAsSeen()
    {
        _pendingTaskUpdates = 0;
        UpdateTasksInfoBadge();
    }

    private void UpdateTasksInfoBadge()
    {
        if (_tasksInfoBadge == null)
        {
            return;
        }

        if (_pendingTaskUpdates <= 0)
        {
            _tasksInfoBadge.Value = 0;
            _tasksInfoBadge.Visibility = Visibility.Collapsed;
            return;
        }

        _tasksInfoBadge.Value = _pendingTaskUpdates;
        _tasksInfoBadge.Visibility = Visibility.Visible;
    }

    private async void TaskToastBorder_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Border border || border.DataContext is not TaskToastItem toast || toast.IsAnimating)
        {
            return;
        }

        toast.IsAnimating = true;
        border.Opacity = 0;

        await AnimateOpacityAsync(border, 1, TimeSpan.FromMilliseconds(200));
        await Task.Delay(TimeSpan.FromSeconds(3));
        await AnimateOpacityAsync(border, 0, TimeSpan.FromMilliseconds(350));

        _taskToasts.Remove(toast);
    }

    private static Task AnimateOpacityAsync(UIElement target, double to, TimeSpan duration)
    {
        var completion = new TaskCompletionSource<bool>();
        var storyboard = new Storyboard();
        var animation = new DoubleAnimation
        {
            To = to,
            Duration = duration,
            EnableDependentAnimation = true
        };

        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, "Opacity");
        storyboard.Children.Add(animation);
        storyboard.Completed += (_, _) => completion.TrySetResult(true);
        storyboard.Begin();

        return completion.Task;
    }
}
