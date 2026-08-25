using System.Collections.ObjectModel;
using Emerald.CoreX.Installation;
using Emerald.CoreX.Notifications;
using Emerald.CoreX.Services;
using Xunit;

namespace Emerald.CoreX.Tests.Installation;

public sealed class InstallationProgressReporterTests
{
    [Fact]
    public async Task Reporter_CoalescesHighFrequencyUpdates_AndFlushesFinalState()
    {
        var updates = new List<InstallationProgress>();
        var reporter = new InstallationProgressReporter(
            new ImmediateDispatcher(),
            notifications: null,
            new RecordingProgress(updates),
            "Installing",
            "Test installation");

        for (var index = 0; index < 8_000; index++)
        {
            reporter.Report(new("Downloading", $"file-{index}", index, 8_000));
        }

        await reporter.CompleteAsync(new("Complete", "done", 8_000, 8_000), true, "Complete");

        Assert.InRange(updates.Count, 1, 4);
        Assert.Equal(8_000, updates[^1].Completed);
        await reporter.DisposeAsync();
    }

    [Fact]
    public async Task Reporter_CompletesNotificationOnlyOnce()
    {
        var notifications = new RecordingNotificationService();
        var reporter = new InstallationProgressReporter(
            new ImmediateDispatcher(),
            notifications,
            externalProgress: null,
            "Installing",
            "Test installation");

        await reporter.CompleteAsync(new("Complete", "done", 1, 1), true, "Complete");
        await reporter.CompleteAsync(new("Failed", "done", 1, 1), false, "Should be ignored");

        Assert.Equal(1, notifications.CompletionCount);
        await reporter.DisposeAsync();
    }

    private sealed class RecordingProgress(List<InstallationProgress> updates) : IProgress<InstallationProgress>
    {
        public void Report(InstallationProgress value) => updates.Add(value);
    }

    private sealed class ImmediateDispatcher : IUiDispatcher
    {
        public bool HasThreadAccess => true;
        public void Invoke(Action action) => action();
        public Task InvokeAsync(Action action)
        {
            action();
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingNotificationService : INotificationService
    {
        public ObservableCollection<Notification> ActiveNotifications { get; } = [];
        public int CompletionCount { get; private set; }

        public (string Id, CancellationToken? CancellationToken) Create(string title, string message = null!, double progress = 0, bool isIndeterminate = false, bool isCancellable = false)
            => ("progress", null);

        public void Update(string? id = null, string? title = null, string? message = null, double? progress = null, bool? isIndeterminate = null) { }
        public void Complete(string id, bool success, string message = null!, Exception ex = null!) => CompletionCount++;
        public string Warning(string title, string message, TimeSpan? duration = null) => "warning";
        public string Info(string title, string message, TimeSpan? duration = null) => "info";
        public string Error(string title, string message, TimeSpan? duration = null, Exception? ex = null) => "error";
        public void RemoveNotification(string id) { }
        public void Cancel(string id) { }
    }
}
