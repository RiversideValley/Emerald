using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace Emerald.CoreX.Notifications;
public interface INotificationService
{
    ObservableCollection<Notification> ActiveNotifications { get; }

    // Updated to return both ID and cancellation token
    (string Id, CancellationToken? CancellationToken) Create(
        string title,
        string message = null,
        double progress = 0,
        bool isIndeterminate = false,
        bool isCancellable = false);

    void Update(string? id = null, string? title = null, string? message = null, double? progress = null, bool? isIndeterminate = null);
    void Complete(string id, bool success, string message = null, Exception ex = null);
    string Warning(string title, string message, TimeSpan? duration = null);
    string Info(string title, string message, TimeSpan? duration = null);
    string Error(string title, string message, TimeSpan? duration = null, Exception? ex = null);
    void RemoveNotification(string id);
    void Cancel(string id);
}
