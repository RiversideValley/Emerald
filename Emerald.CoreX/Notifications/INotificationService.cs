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

    /// <summary>
    /// Creates a new notification with the specified parameters and returns its ID along with an optional cancellation token.
    /// </summary>
    /// <param name="title">The title of the notification.</param>
    /// <param name="message">The message content of the notification. Optional.</param>
    /// <param name="progress">The progress value of the notification, ranging from 0 to 1. Optional.</param>
    /// <param name="isIndeterminate">Indicates whether the progress is indeterminate. Optional.</param>
    /// <param name="isCancellable">Indicates whether the notification can be cancelled. Optional.</param>
    /// <returns>A tuple containing the unique ID of the created notification and an optional cancellation token.</returns>
    (string Id, CancellationToken? CancellationToken) Create(
        string title,
        string message = null,
        double progress = 0,
        bool isIndeterminate = false,
        bool isCancellable = false);

    /// <summary>
    /// Updates the properties of an existing notification based on the specified parameters.
    /// </summary>
    /// <param name="id">The unique ID of the notification to update. If null, no specific notification will be targeted.</param>
    /// <param name="title">The updated title of the notification. Optional.</param>
    /// <param name="message">The updated message content of the notification. Optional.</param>
    /// <param name="progress">The updated progress value of the notification, ranging from 0 to 1. Optional.</param>
    /// <param name="isIndeterminate">Indicates whether the progress is set to indeterminate. Optional.</param>
    void Update(string? id = null, string? title = null, string? message = null, double? progress = null, bool? isIndeterminate = null);

    /// <summary>
    /// Completes a notification with the specified ID, indicating success or failure, and includes
    /// an optional message or exception for additional context.
    /// </summary>
    /// <param name="id">The unique ID of the notification to complete.</param>
    /// <param name="success">Indicates whether the operation related to the notification was successful.</param>
    /// <param name="message">An optional message describing the completion result. Defaults to null.</param>
    /// <param name="ex">An optional exception related to the completion, typically used in the case of a failure. Defaults to null.</param>
    void Complete(string id, bool success, string message = null, Exception ex = null);

    /// <summary>
    /// Creates a warning notification with the specified title and message, optionally setting a duration for its display.
    /// </summary>
    /// <param name="title">The title of the warning notification.</param>
    /// <param name="message">The message content of the warning notification.</param>
    /// <param name="duration">The duration for which the notification will be displayed. Optional.</param>
    /// <returns>The unique ID of the created warning notification.</returns>
    string Warning(string title, string message, TimeSpan? duration = null);

    /// <summary>
    /// Displays an informational notification with the specified title, message, and optional duration.
    /// </summary>
    /// <param name="title">The title of the informational notification.</param>
    /// <param name="message">The message content of the notification.</param>
    /// <param name="duration">The duration for which the notification is displayed. Optional. If not specified, a default duration is used.</param>
    /// <returns>The unique identifier of the created notification.</returns>
    string Info(string title, string message, TimeSpan? duration = null);

    /// <summary>
    /// Displays an error notification with the specified title and message, and optionally includes a duration and an exception.
    /// </summary>
    /// <param name="title">The title of the error notification.</param>
    /// <param name="message">The detailed message for the error notification.</param>
    /// <param name="duration">The duration for which the error notification will be displayed. Optional.</param>
    /// <param name="ex">An exception object containing additional error details. Optional.</param>
    /// <returns>The unique identifier of the created error notification.</returns>
    string Error(string title, string message, TimeSpan? duration = null, Exception? ex = null);

    /// <summary>
    /// Removes an active notification identified by its unique ID.
    /// </summary>
    /// <param name="id">The unique identifier of the notification to remove.</param>
    void RemoveNotification(string id);

    /// <summary>
    /// Cancels an active notification with the specified ID if it is cancellable.
    /// </summary>
    /// <param name="id">The unique ID of the notification to be cancelled.</param>
    void Cancel(string id);
}
