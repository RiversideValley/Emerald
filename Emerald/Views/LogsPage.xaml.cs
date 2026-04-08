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

public sealed partial class LogsPage : Page
{
    public LogsPageViewModel ViewModel { get; }
    private Emerald.CoreX.Runtime.GameSession? _observedSession;

    public LogsPage()
    {
        ViewModel = Ioc.Default.GetService<LogsPageViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.InitializeCommand.ExecuteAsync(e.Parameter);
        HookSelectedSession();
        ScrollToLatestEntry();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        UnhookSelectedSession();
        base.OnNavigatedFrom(e);
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModel.SelectedSession))
        {
            HookSelectedSession();
            ScrollToLatestEntry();
        }
    }

    private void HookSelectedSession()
    {
        UnhookSelectedSession();

        if (ViewModel.SelectedSession == null)
        {
            return;
        }

        _observedSession = ViewModel.SelectedSession;
        _observedSession.Entries.CollectionChanged += SelectedEntries_CollectionChanged;
    }

    private void UnhookSelectedSession()
    {
        if (_observedSession == null)
        {
            return;
        }

        _observedSession.Entries.CollectionChanged -= SelectedEntries_CollectionChanged;
        _observedSession = null;
    }

    private void SelectedEntries_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems == null || e.NewItems.Count == 0 || !ViewModel.AutoScroll)
        {
            return;
        }

        ScrollToLatestEntry();
    }

    private void ScrollToLatestEntry()
    {
        if (!ViewModel.AutoScroll || ViewModel.SelectedEntries.Count == 0)
        {
            return;
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            var latest = ViewModel.SelectedEntries.LastOrDefault();
            if (latest != null)
            {
                LogEntriesListView.ScrollIntoView(latest);
            }
        });
    }

    private void CopyLogs_Click(object sender, RoutedEventArgs e)
    {
        var logText = ViewModel.GetSelectedSessionClipboardText();
        if (string.IsNullOrWhiteSpace(logText))
        {
            return;
        }

        var package = new DataPackage();
        package.SetText(logText);
        Clipboard.SetContent(package);
    }

    private async void OpenLogsFolder_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedSession == null)
        {
            return;
        }

        try
        {
            var folderPath = Path.Combine(ViewModel.SelectedSession.GamePath, "logs");
            if (!Directory.Exists(folderPath))
            {
                folderPath = ViewModel.SelectedSession.GamePath;
            }

            await Launcher.LaunchFolderAsync(await StorageFolder.GetFolderFromPathAsync(folderPath));
        }
        catch (Exception ex)
        {
            this.Log().LogError(ex, "Failed to open log folder");
        }
    }
}
