using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace Emerald.CoreX.Notifications;

public class NotificationService : ObservableObject, INotificationService
{
    private ObservableCollection<Notification> _activeNotifications;
    public ReadOnlyObservableCollection<Notification> ActiveNotifications { get; }

    // Dictionary to track notifications by their ID for quick lookup
    private Dictionary<string, Notification> _notificationLookup;

    public NotificationService()
    {
        _activeNotifications = new ();
        ActiveNotifications = new (_activeNotifications);
        _notificationLookup = new Dictionary<string, Notification>();
    }

    // Creates a new task progress notification and returns its ID and CancellationToken
    public (string Id, CancellationToken CancellationToken) Create(
        string title,
        string message = null,
        double progress = 0,
        bool isIndeterminate = false)
    {
        string id = GenerateUniqueId();
        var cts = new CancellationTokenSource();

        var notification = new Notification
        {
            Id = id,
            Title = title,
            Message = message,
            Type = NotificationType.Progress,
            Progress = progress,
            IsIndeterminate = isIndeterminate,
            Timestamp = DateTime.Now,
            CancellationSource = cts,
            IsCancellable = true
        };

        _activeNotifications.Add(notification);
        _notificationLookup.Add(id, notification);

        return (id, cts.Token);
    }

    public void Update(string? id = null, string? title = null, string? message = null, double? progress = null, bool? isIndeterminate = null)
    {
        if (_notificationLookup.TryGetValue(id, out var notification))
        {
            notification.Title = title ?? notification.Title;
            notification.Message = message ?? notification.Message;
            notification.Progress = progress ?? notification.Progress;
            notification.IsIndeterminate = isIndeterminate ?? notification.IsIndeterminate;
        }
    }

    // Mark a task as completed
    public void Complete(string id, bool success, string message = null, Exception ex = null)
    {
        if (_notificationLookup.TryGetValue(id, out var notification))
        {
            notification.Type = success ? NotificationType.Success : NotificationType.Error;
            notification.Progress = success ? 100 : notification.Progress;
            notification.IsCompleted = true;
            notification.IsIndeterminate = false;
            notification.IsCancellable = false;
            notification.Message = message ?? notification.Message;
        }
    }

    // Show a warning notification
    public string Warning(string title, string message, TimeSpan? duration = null)
    {
        string id = GenerateUniqueId();

        var notification = new Notification
        {
            Id = id,
            Title = title,
            Message = message,
            Type = NotificationType.Warning,
            Timestamp = DateTime.Now,
            Duration = duration,
            IsCancellable = false
        };

        _activeNotifications.Add(notification);
        _notificationLookup.Add(id, notification);

        return id;
    }

    // Show a simple info notification
    public string Info(string title, string message, TimeSpan? duration = null)
    {
        string id = GenerateUniqueId();

        var notification = new Notification
        {
            Id = id,
            Title = title,
            Message = message,
            Type = NotificationType.Info,
            Timestamp = DateTime.Now,
            Duration = duration,
            IsCancellable = false
        };

        _activeNotifications.Add(notification);
        _notificationLookup.Add(id, notification);

        return id;
    }

    // Show an error notification
    public string Error(string title, string message, TimeSpan? duration = null)
    {
        string id = GenerateUniqueId();

        var notification = new Notification
        {
            Id = id,
            Title = title,
            Message = message,
            Type = NotificationType.Error,
            Timestamp = DateTime.Now,
            Duration = duration,
            IsCancellable = false
        };

        _activeNotifications.Add(notification);
        _notificationLookup.Add(id, notification);

        return id;
    }

    // Manually remove a notification
    public void RemoveNotification(string id)
    {
        if (_notificationLookup.TryGetValue(id, out var notification))
        {
            _activeNotifications.Remove(notification);
            _notificationLookup.Remove(id);

            // Dispose cancellation token if it exists
            notification.CancellationSource?.Dispose();
        }
    }

    // Cancel a task (if it supports cancellation)
    public void Cancel(string id)
    {
        if (_notificationLookup.TryGetValue(id, out var notification) &&
            notification.IsCancellable &&
            notification.CancellationSource != null &&
            !notification.CancellationSource.IsCancellationRequested)
        {
            notification.CancellationSource.Cancel();
            notification.Message = "Canceling...";
        }
    }

    // Helper to generate unique IDs for notifications
    private string GenerateUniqueId()
    {
        return Guid.NewGuid().ToString();

    }
}
