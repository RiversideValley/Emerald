using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;

namespace Emerald.CoreX.Notifications;

public class NotificationService : ObservableObject, INotificationService
{
    private readonly ILogger<NotificationService> _logger;
    public ObservableCollection<Notification> ActiveNotifications { get; private set; }

    public NotificationService(ILogger<NotificationService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ActiveNotifications = new();
    }

    public (string Id, CancellationToken? CancellationToken) Create(
        string title,
        string message = null,
        double progress = 0,
        bool isIndeterminate = false,
        bool isCancellable = false)
    {
        string id = GenerateUniqueId();
        var cts = isCancellable? new CancellationTokenSource() : null;

        var notification = new Notification
        {
            Id = id,
            Title = title,
            Message = message,
            Type = NotificationType.Progress,
            Progress = progress,
            IsIndeterminate = isIndeterminate,
            Timestamp = DateTime.Now,
            CancellationSource = cts
        };

        ActiveNotifications.Add(notification);
        _logger.LogInformation("Created notification with ID: {Id}, Title: {Title}", id, title);

        return (id, cts?.Token);
    }

    public void Update(string? id = null, string? title = null, string? message = null, double? progress = null, bool? isIndeterminate = null)
    {
        var notification = ActiveNotifications.FirstOrDefault(n => n.Id == id);
        if (notification != null)
        {
            notification.Title = title ?? notification.Title;
            notification.Message = message ?? notification.Message;
            notification.Progress = progress ?? notification.Progress;
            notification.IsIndeterminate = isIndeterminate ?? notification.IsIndeterminate;

            _logger.LogInformation("Updated notification with ID: {Id}", id);
        }
        else
        {
            _logger.LogWarning("Failed to update notification. ID: {Id} not found.", id);
        }
    }

    public void Complete(string id, bool success, string message = null, Exception ex = null)
    {
        var notification = ActiveNotifications.FirstOrDefault(n => n.Id == id);
        if (notification != null)
        {
            notification.Type = success ? NotificationType.Success : NotificationType.Error;
            notification.Progress = success ? 100 : notification.Progress;
            notification.IsCompleted = true;
            notification.IsIndeterminate = false;
            notification.Message = message ?? notification.Message;
            notification.Exception = ex;

            _logger.LogInformation("Completed notification with ID: {Id}, Success: {Success}", id, success);
        }
        else
        {
            _logger.LogWarning("Failed to complete notification. ID: {Id} not found.", id);
        }
    }

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
            Duration = duration
        };

        ActiveNotifications.Add(notification);
        _logger.LogInformation("Created warning notification with ID: {Id}, Title: {Title}", id, title);

        return id;
    }

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
        };

        ActiveNotifications.Add(notification);
        _logger.LogInformation("Created info notification with ID: {Id}, Title: {Title}", id, title);

        return id;
    }

    public string Error(string title, string message, TimeSpan? duration = null, Exception? ex = null)
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
            Exception = ex
        };

        ActiveNotifications.Add(notification);
        _logger.LogError(ex, "Created error notification with ID: {Id}, Title: {Title}", id, title);

        return id;
    }

    public void RemoveNotification(string id)
    {
        var notification = ActiveNotifications.FirstOrDefault(n => n.Id == id);
        if (notification != null)
        {
            ActiveNotifications.Remove(notification);
            notification.CancellationSource?.Dispose();

            _logger.LogInformation("Removed notification with ID: {Id}", id);
        }
        else
        {
            _logger.LogWarning("Failed to remove notification. ID: {Id} not found.", id);
        }
    }

    public void Cancel(string id)
    {
        var notification = ActiveNotifications.FirstOrDefault(n => n.Id == id);
        if (notification != null &&
            notification.IsCancellable &&
            notification.CancellationSource != null &&
            !notification.CancellationSource.IsCancellationRequested)
        {
            notification.CancellationSource.Cancel();
            notification.Message = "Canceling...";

            _logger.LogInformation("Canceled notification with ID: {Id}", id);
        }
        else
        {
            _logger.LogWarning("Failed to cancel notification. ID: {Id} not found or not cancellable.", id);
        }
    }

    private string GenerateUniqueId()
    {
        return Guid.NewGuid().ToString();
    }
}
