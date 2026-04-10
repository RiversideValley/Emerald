using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using Microsoft.UI.Dispatching;

namespace Emerald.CoreX.Notifications;

public class DispatchedNotificationService : CoreX.Notifications.INotificationService
{
    private readonly CoreX.Notifications.INotificationService _inner;
    private readonly DispatcherQueue _dispatcher;

    // Expose the inner collection directly — it's read-only so safe to pass through
    public ObservableCollection<Notification> ActiveNotifications => _inner.ActiveNotifications;

    public DispatchedNotificationService(
        CoreX.Notifications.INotificationService inner,
        DispatcherQueue dispatcher)
    {
        _inner = inner;
        _dispatcher = dispatcher;
    }

    // For methods with return values — must block until UI thread completes
    private T RunOnUI<T>(Func<T> action)
    {
        if (_dispatcher.HasThreadAccess)
            return action(); // already on UI thread, run directly

        var tcs = new TaskCompletionSource<T>();
        _dispatcher.TryEnqueue(() =>
        {
            try { tcs.SetResult(action()); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        return tcs.Task.GetAwaiter().GetResult();
    }

    // For void methods — fire and forget on UI thread
    private void RunOnUI(Action action)
    {
        if (_dispatcher.HasThreadAccess)
            action();
        else
            _dispatcher.TryEnqueue(() => action());
    }

    public (string Id, CancellationToken? CancellationToken) Create(
        string title, string message = null, double progress = 0,
        bool isIndeterminate = false, bool isCancellable = false)
        => RunOnUI(() => _inner.Create(title, message, progress, isIndeterminate, isCancellable));

    public void Update(string? id = null, string? title = null, string? message = null,
        double? progress = null, bool? isIndeterminate = null)
        => RunOnUI(() => _inner.Update(id, title, message, progress, isIndeterminate));

    public void Complete(string id, bool success, string message = null, Exception ex = null)
        => RunOnUI(() => _inner.Complete(id, success, message, ex));

    public string Warning(string title, string message, TimeSpan? duration = null)
        => RunOnUI(() => _inner.Warning(title, message, duration));

    public string Info(string title, string message, TimeSpan? duration = null)
        => RunOnUI(() => _inner.Info(title, message, duration));

    public string Error(string title, string message, TimeSpan? duration = null, Exception? ex = null)
        => RunOnUI(() => _inner.Error(title, message, duration, ex));

    public void RemoveNotification(string id)
        => RunOnUI(() => _inner.RemoveNotification(id));

    public void Cancel(string id)
        => RunOnUI(() => _inner.Cancel(id));
}
