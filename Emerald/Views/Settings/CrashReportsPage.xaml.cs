using System.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using Emerald.CoreX.Helpers;
using Emerald.Helpers;
using Emerald.Services;
using Emerald.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using LocalMessageBoxButtons = Emerald.Helpers.Enums.MessageBoxButtons;
using LocalMessageBoxResults = Emerald.Helpers.Enums.MessageBoxResults;

namespace Emerald.Views.Settings;

public sealed partial class CrashReportsPage : Page
{
    public CrashReportsPageViewModel ViewModel { get; }

    public CrashReportsPage()
    {
        ViewModel = Ioc.Default.GetRequiredService<CrashReportsPageViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        var reportId = e.Parameter as string;
        ViewModel.Refresh(reportId);
        _ = RefreshAfterNativeDiagnosticsAsync(reportId);
    }

    private async Task RefreshAfterNativeDiagnosticsAsync(string? reportId)
    {
        try
        {
            await Task.Run(ViewModel.EnrichNativeDiagnostics);
            DispatcherQueue.TryEnqueue(() => ViewModel.Refresh(reportId));
        }
        catch (Exception exception)
        {
            this.Log().LogDebug(exception, "Native crash evidence refresh was unavailable.");
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModel.SelectedReport))
        {
            ReportsListView.SelectedItem = ViewModel.SelectedReport;
        }
    }

    private void CopyReport_Click(object sender, RoutedEventArgs e)
    {
        var text = ViewModel.SelectedReportText;
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        try
        {
            var package = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            package.SetText(text);
            Clipboard.SetContent(package);
        }
        catch (Exception exception)
        {
            this.Log().LogWarning(exception, "Could not copy the crash report to the clipboard.");
        }
    }

    private async void ReportOnGitHub_Click(object sender, RoutedEventArgs e)
    {
        var draft = ViewModel.GetSelectedGitHubDraft();
        if (draft is null)
        {
            return;
        }

        try
        {
            var package = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            package.SetText(draft.FullReport);
            Clipboard.SetContent(package);
            ViewModel.AcknowledgeSelected();

            if (!Uri.TryCreate(draft.Url, UriKind.Absolute, out var uri)
                || !await Launcher.LaunchUriAsync(uri))
            {
                await MessageBox.Show("Error".Localize(), "CouldNotOpenGitHubReport".Localize(), LocalMessageBoxButtons.Ok);
            }
        }
        catch (Exception exception)
        {
            this.Log().LogWarning(exception, "Could not prepare a GitHub crash report.");
        }
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.HasSelectedReport)
        {
            return;
        }

        var result = await MessageBox.Show(
            "DeleteCrashReport".Localize(),
            "DeleteCrashReportDescription".Localize(),
            LocalMessageBoxButtons.YesNo);
        if (result == LocalMessageBoxResults.Yes)
        {
            ViewModel.DeleteSelected();
        }
    }

    private async void DeleteAll_Click(object sender, RoutedEventArgs e)
    {
        var result = await MessageBox.Show(
            "DeleteAllCrashReports".Localize(),
            "DeleteAllCrashReportsDescription".Localize(),
            LocalMessageBoxButtons.YesNo);
        if (result == LocalMessageBoxResults.Yes)
        {
            ViewModel.DeleteAll();
        }
    }

    private void OpenLogs_Click(object sender, RoutedEventArgs e)
        => PlatformFolderLauncher.TryOpen(Path.GetDirectoryName(ViewModel.ApplicationLogPath));

    private void OpenNativeDiagnostics_Click(object sender, RoutedEventArgs e)
        => PlatformFolderLauncher.TryOpen(Path.GetDirectoryName(ViewModel.SelectedNativeDiagnosticsPath));
}
