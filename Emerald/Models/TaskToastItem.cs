using Emerald.CoreX.Notifications;

namespace Emerald.Models;

public sealed class TaskToastItem
{
    public TaskToastItem(string id, string title, string? message, NotificationType type)
    {
        Id = id;
        Title = title;
        Message = message;
        Type = type;
    }

    public string Id { get; }
    public string Title { get; }
    public string? Message { get; }
    public NotificationType Type { get; }

    public bool HasMessage => !string.IsNullOrWhiteSpace(Message);
    public bool IsAnimating { get; set; }
}
