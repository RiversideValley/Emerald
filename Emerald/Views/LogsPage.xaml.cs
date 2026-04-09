using System.ComponentModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.DependencyInjection;
using Emerald.CoreX.Helpers;
using Emerald.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.System;

namespace Emerald.Views;

/// <summary>
/// Displays tracked runtime sessions and the currently selected session's visible log entries.
/// </summary>
public sealed partial class LogsPage : Page
{
    public LogsPageViewModel ViewModel { get; }

    public LogsPage()
    {
        ViewModel = Ioc.Default.GetService<LogsPageViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        ViewModel.VisibleEntries.CollectionChanged += VisibleEntries_CollectionChanged;
        this.Log().LogInformation("Logs page initialized.");
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        this.Log().LogInformation("Navigated to logs page.");
        await ViewModel.InitializeCommand.ExecuteAsync(e.Parameter);
        ScrollToLatestEntry();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        this.Log().LogInformation("Navigated away from logs page.");
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModel.SelectedSession))
        {
            this.Log().LogDebug(
                "Logs page observed selected session change. Session: {SessionName}.",
                ViewModel.SelectedSession?.DisplayName ?? "<none>");
            ScrollToLatestEntry();
        }
    }

    private void VisibleEntries_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems == null || e.NewItems.Count == 0 || !ViewModel.AutoScroll)
        {
            return;
        }

        this.Log().LogDebug("Auto-scrolling logs page after {NewItemCount} new visible entry item(s).", e.NewItems.Count);
        ScrollToLatestEntry();
    }

    /// <summary>
    /// Scrolls the log list to the newest visible entry when auto-scroll is active.
    /// </summary>
    private void ScrollToLatestEntry()
    {
        if (!ViewModel.AutoScroll || ViewModel.VisibleEntries.Count == 0)
        {
            return;
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            var latest = ViewModel.VisibleEntries.LastOrDefault();
            if (latest != null)
            {
                this.Log().LogDebug("Scrolling logs view to the latest visible entry.");
                LogEntriesListView.ScrollIntoView(latest);
            }
        });
    }

    private void CopyLogs_Click(object sender, RoutedEventArgs e)
    {
        var logText = ViewModel.GetSelectedSessionClipboardText();
        if (string.IsNullOrWhiteSpace(logText))
        {
            this.Log().LogDebug("Skipping log copy because the selected session has no clipboard text.");
            return;
        }

        var package = new DataPackage();
        package.SetText(logText);
        Clipboard.SetContent(package);
        this.Log().LogInformation("Copied selected session logs to the clipboard.");
    }

    private async void OpenLogsFolder_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedSession == null)
        {
            this.Log().LogDebug("Skipping log folder open because no session is selected.");
            return;
        }

        try
        {
            var folderPath = Path.Combine(ViewModel.SelectedSession.GamePath, "logs");
            if (!Directory.Exists(folderPath))
            {
                folderPath = ViewModel.SelectedSession.GamePath;
            }

            this.Log().LogInformation("Opening log folder for {SessionName}. Path: {FolderPath}.", ViewModel.SelectedSession.DisplayName, folderPath);
            await Launcher.LaunchFolderAsync(await StorageFolder.GetFolderFromPathAsync(folderPath));
        }
        catch (Exception ex)
        {
            this.Log().LogError(ex, "Failed to open log folder");
        }
    }
}
