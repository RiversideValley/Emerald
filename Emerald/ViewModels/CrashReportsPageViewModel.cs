using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Emerald.CoreX.CrashHandling;
using Emerald.CoreX.Helpers;
using Emerald.Helpers;
using Emerald.Services;

namespace Emerald.ViewModels;

public sealed class CrashReportsPageViewModel : ObservableObject
{
    private readonly CrashCoordinator _crashCoordinator;
    private CrashReportListItem? _selectedReport;

    public CrashReportsPageViewModel(CrashCoordinator crashCoordinator)
        => _crashCoordinator = crashCoordinator;

    public ObservableCollection<CrashReportListItem> Reports { get; } = [];

    public CrashReportListItem? SelectedReport
    {
        get => _selectedReport;
        set
        {
            if (SetProperty(ref _selectedReport, value))
            {
                OnPropertyChanged(nameof(SelectedRecord));
                OnPropertyChanged(nameof(SelectedReportText));
                OnPropertyChanged(nameof(SelectedReportPath));
                OnPropertyChanged(nameof(SelectedNativeDiagnosticsPath));
                OnPropertyChanged(nameof(HasSelectedNativeDiagnostics));
                OnPropertyChanged(nameof(HasSelectedReport));
            }
        }
    }

    public CrashRecord? SelectedRecord => SelectedReport?.Record;
    public bool HasReports => Reports.Count > 0;
    public bool HasSelectedReport => SelectedRecord is not null;
    public bool HasSelectedNativeDiagnostics => !string.IsNullOrWhiteSpace(SelectedNativeDiagnosticsPath);
    public string SelectedReportText => SelectedRecord is null ? string.Empty : CrashReportFormatter.ToText(SelectedRecord);
    public string? SelectedReportPath => SelectedRecord?.ReportPath;
    public string? SelectedNativeDiagnosticsPath => SelectedRecord?.NativeDiagnosticsPath;
    public string ReportsPath => _crashCoordinator.ReportsPath;
    public string ApplicationLogPath => _crashCoordinator.ApplicationLogPath;

    public void Refresh(string? reportId = null)
    {
        var records = _crashCoordinator.GetReports();
        Reports.Clear();
        foreach (var record in records)
        {
            Reports.Add(new CrashReportListItem(record));
        }

        SelectedReport = reportId is null
            ? Reports.FirstOrDefault()
            : Reports.FirstOrDefault(item => string.Equals(item.Record.Id, reportId, StringComparison.Ordinal))
              ?? Reports.FirstOrDefault();
        OnPropertyChanged(nameof(HasReports));
    }

    public GitHubIssueDraft? GetSelectedGitHubDraft()
        => SelectedRecord is null
            ? null
            : new GitHubCrashIssueComposer("https://github.com/RiversideValley/Emerald").Compose(SelectedRecord);

    public void EnrichNativeDiagnostics()
        => _crashCoordinator.EnrichNativeDiagnostics();

    public bool AcknowledgeSelected()
        => SelectedRecord is not null && _crashCoordinator.Acknowledge(SelectedRecord.Id);

    public bool DeleteSelected()
    {
        var record = SelectedRecord;
        if (record is null || !_crashCoordinator.Delete(record.Id))
        {
            return false;
        }

        Refresh();
        return true;
    }

    public int DeleteAll()
    {
        var deleted = _crashCoordinator.DeleteAll();
        Refresh();
        return deleted;
    }
}

public sealed class CrashReportListItem
{
    public CrashReportListItem(CrashRecord record)
        => Record = record;

    public CrashRecord Record { get; }

    public string Title
        => Record.Kind == CrashRecordKind.ManagedCrash
            ? "ManagedCrash".Localize()
            : "UnexpectedShutdown".Localize();

    public string OccurredText
        => Record.OccurredUtc.ToLocalTime().ToString("g");

    public string Summary
        => Record.Exception?.Type
           ?? Record.NativeDiagnosticsStatus
           ?? "CrashDetailsUnavailable".Localize();

    public string Status
        => Record.IsAcknowledged
            ? string.Empty
            : "New".Localize();
}
