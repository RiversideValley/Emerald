using System;
using System.Linq;
using CommunityToolkit.Mvvm.DependencyInjection;
using Emerald.CoreX.Helpers;
using Emerald.CoreX.Notifications;
using Emerald.Helpers;
using Emerald.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Emerald.UserControls;

public sealed partial class TasksPanelControl : UserControl
{
    private readonly SettingsService _settingsService;
    private readonly INotificationService _notificationService;

    public static readonly DependencyProperty IsCompactHostProperty = DependencyProperty.Register(
        nameof(IsCompactHost),
        typeof(bool),
        typeof(TasksPanelControl),
        new PropertyMetadata(false, OnIsCompactHostChanged));

    public bool IsCompactHost
    {
        get => (bool)GetValue(IsCompactHostProperty);
        set => SetValue(IsCompactHostProperty, value);
    }

    public TasksPanelControl()
    {
        _settingsService = Ioc.Default.GetService<SettingsService>()
            ?? throw new InvalidOperationException("Settings service is not available.");
        _notificationService = Ioc.Default.GetService<INotificationService>()
            ?? throw new InvalidOperationException("Notification service is not available.");

        InitializeComponent();
        Loaded += TasksPanelControl_Loaded;
    }

    private static void OnIsCompactHostChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TasksPanelControl control)
        {
            control.ApplyPresentationMode();
        }
    }

    private void TasksPanelControl_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyPresentationMode();
        UpdateCompactModeButtonLabel();
    }

    private void ApplyPresentationMode()
    {
        if (IsCompactHost)
        {
            HeaderGrid.Padding = new Thickness(12, 12, 12, 8);
            ContentHost.Margin = new Thickness(12, 0, 12, 12);
            TaskIntroTextBlock.Visibility = Visibility.Collapsed;
            return;
        }

        HeaderGrid.Padding = new Thickness(24, 24, 24, 12);
        ContentHost.Margin = new Thickness(24, 0, 24, 24);
        TaskIntroTextBlock.Visibility = Visibility.Visible;
    }

    private void CompactModeButton_Click(object sender, RoutedEventArgs e)
    {
        var compactModeEnabled = !_settingsService.Settings.App.Tasks.CompactMode;
        _settingsService.Settings.App.Tasks.CompactMode = compactModeEnabled;

        UpdateCompactModeButtonLabel();
        NotifyMainPagePresentationChange(compactModeEnabled);
    }

    private void UpdateCompactModeButtonLabel()
    {
        CompactModeButton.Label = _settingsService.Settings.App.Tasks.CompactMode
            ? "SwitchToPageMode".Localize()
            : "SwitchToCompactMode".Localize();
        ToolTipService.SetToolTip(CompactModeButton, CompactModeButton.Label);
    }

    private void ClearAllButton_Click(object sender, RoutedEventArgs e)
    {
        var notificationIds = _notificationService.ActiveNotifications
            .Select(notification => notification.Id)
            .ToList();

        foreach (var id in notificationIds)
        {
            _notificationService.RemoveNotification(id);
        }
    }

    private static void NotifyMainPagePresentationChange(bool compactModeEnabled)
    {
        if (App.Current.MainWindow?.Content is Frame rootFrame
            && rootFrame.Content is MainPage mainPage)
        {
            mainPage.HandleTasksModeChanged(compactModeEnabled);
        }
    }
}
