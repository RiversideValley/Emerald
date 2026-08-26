using System.Threading.Channels;
using Emerald.CoreX.Notifications;
using Emerald.CoreX.Services;

namespace Emerald.CoreX.Installation;

/// <summary>
/// Converts noisy installer callbacks into a small, ordered stream of UI
/// updates. CmlLib can report thousands of file events; rendering each one
/// makes the dispatcher spend all its time repainting the task surface.
/// </summary>
internal sealed class InstallationProgressReporter : IAsyncDisposable, IProgress<InstallationProgress>
{
    private static readonly TimeSpan UiUpdateInterval = TimeSpan.FromMilliseconds(250);

    private readonly Channel<InstallationProgress> _updates = Channel.CreateUnbounded<InstallationProgress>();
    private readonly IUiDispatcher _dispatcher;
    private readonly IProgress<InstallationProgress>? _externalProgress;
    private readonly INotificationService? _notifications;
    private readonly Task _pump;
    private readonly string? _notificationId;
    private int _completionStarted;

    public CancellationToken? NotificationCancellationToken { get; }

    public InstallationProgressReporter(
        IUiDispatcher dispatcher,
        INotificationService? notifications,
        IProgress<InstallationProgress>? externalProgress,
        string title,
        string message)
    {
        _dispatcher = dispatcher;
        _notifications = notifications;
        _externalProgress = externalProgress;

        if (_notifications != null)
        {
            (string id, CancellationToken? cancellationToken) created = default;
            _dispatcher.Invoke(() => created = _notifications.Create(
                title,
                message,
                progress: 0,
                isIndeterminate: true,
                isCancellable: true));
            _notificationId = created.id;
            NotificationCancellationToken = created.cancellationToken;
        }

        _pump = PumpAsync();
    }

    public void Report(InstallationProgress progress)
        => _updates.Writer.TryWrite(progress);

    public async Task CompleteAsync(InstallationProgress finalProgress, bool success, string message, Exception? exception = null)
    {
        // Several exception paths can converge while an installer is unwinding.
        // Only the first terminal result is allowed to complete the notification.
        if (Interlocked.Exchange(ref _completionStarted, 1) != 0)
        {
            await _pump.ConfigureAwait(false);
            return;
        }

        _updates.Writer.TryWrite(finalProgress);
        _updates.Writer.TryComplete();
        await _pump.ConfigureAwait(false);

        if (_notificationId != null && _notifications != null)
        {
            await _dispatcher.InvokeAsync(() => _notifications.Complete(_notificationId, success, message, exception!));
        }
    }

    private async Task PumpAsync()
    {
        while (await _updates.Reader.WaitToReadAsync().ConfigureAwait(false))
        {
            InstallationProgress? latest = null;
            while (_updates.Reader.TryRead(out var update))
            {
                latest = update;
            }

            if (latest != null)
            {
                await PublishAsync(latest).ConfigureAwait(false);
            }

            // Drain events that arrive during the interval on the next pass,
            // keeping visible updates at four per second or less.
            if (!_updates.Reader.Completion.IsCompleted)
            {
                try { await Task.Delay(UiUpdateInterval).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }
            }
        }
    }

    private Task PublishAsync(InstallationProgress progress)
        => _dispatcher.InvokeAsync(() =>
        {
            _externalProgress?.Report(progress);
            if (_notificationId != null && _notifications != null)
            {
                var percentage = progress.TotalBytes > 0
                    ? Math.Clamp(progress.ProcessedBytes * 100d / progress.TotalBytes, 0, 100)
                    : progress.Total <= 0
                    ? (double?)null
                    : Math.Clamp(progress.Completed * 100d / progress.Total, 0, 100);
                var message = progress.CurrentItem == null
                    ? progress.Stage
                    : $"{progress.Stage}: {progress.CurrentItem} ({progress.Completed}/{progress.Total})";
                _notifications.Update(_notificationId, message: message, progress: percentage, isIndeterminate: percentage == null);
            }
        });

    public async ValueTask DisposeAsync()
    {
        _updates.Writer.TryComplete();
        await _pump.ConfigureAwait(false);
    }
}
