using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Emerald.CoreX.Notifications;
using Emerald.CoreX.Runtime;
using Microsoft.Extensions.Logging;

namespace Emerald.ApiHost.Services;

public sealed class EventHub : IDisposable
{
    private readonly ILogger<EventHub> _logger;
    private readonly IGameRuntimeService _runtimeService;
    private readonly INotificationService _notificationService;
    private readonly ConcurrentDictionary<Guid, WebSocket> _sockets = new();
    private readonly ConcurrentDictionary<string, GameSession> _trackedSessions = new();
    private readonly ConcurrentDictionary<string, Notification> _trackedNotifications = new();

    public EventHub(
        ILogger<EventHub> logger,
        IGameRuntimeService runtimeService,
        INotificationService notificationService)
    {
        _logger = logger;
        _runtimeService = runtimeService;
        _notificationService = notificationService;

        // Hook initial active sessions and notifications
        HookSessions();
        HookNotifications();
    }

    public async Task HandleSocketAsync(WebSocket socket)
    {
        var id = Guid.NewGuid();
        _sockets[id] = socket;
        _logger.LogInformation("WebSocket client connected. ID: {Id}. Active: {Count}", id, _sockets.Count);

        try
        {
            var buffer = new byte[1024 * 4];
            while (socket.State == WebSocketState.Open)
            {
                // Keep-alive or handle incoming messages if needed.
                // We mainly broadcast from C# -> Swift.
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug("WebSocket connection exception for ID {Id}: {Message}", id, ex.Message);
        }
        finally
        {
            _sockets.TryRemove(id, out _);
            _logger.LogInformation("WebSocket client disconnected. ID: {Id}. Active: {Count}", id, _sockets.Count);
        }
    }

    private void HookSessions()
    {
        _runtimeService.Sessions.CollectionChanged += OnSessionsChanged;
        foreach (var session in _runtimeService.Sessions)
        {
            TrackSession(session);
        }
    }

    private void OnSessionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (GameSession session in e.NewItems)
            {
                TrackSession(session);
            }
        }
        if (e.OldItems != null)
        {
            foreach (GameSession session in e.OldItems)
            {
                UntrackSession(session);
            }
        }
    }

    private void TrackSession(GameSession session)
    {
        var key = session.GamePath;
        if (!_trackedSessions.TryAdd(key, session)) return;

        session.PropertyChanged += OnSessionPropertyChanged;
        session.Entries.CollectionChanged += OnSessionEntriesChanged;

        // Broadcast initial session registration
        Broadcast("SessionStateChanged", new
        {
            GamePath = session.GamePath,
            DisplayName = session.DisplayName,
            State = session.RunStateText,
            ProcessId = session.ProcessId,
            ExitCode = session.ExitCode,
            HasCrashReport = session.HasCrashReport,
            CrashReportPath = session.CrashReportPath
        });
    }

    private void UntrackSession(GameSession session)
    {
        var key = session.GamePath;
        if (_trackedSessions.TryRemove(key, out var oldSession))
        {
            oldSession.PropertyChanged -= OnSessionPropertyChanged;
            oldSession.Entries.CollectionChanged -= OnSessionEntriesChanged;
        }
    }

    private void OnSessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not GameSession session) return;

        if (e.PropertyName is nameof(GameSession.State) 
            or nameof(GameSession.ProcessId) 
            or nameof(GameSession.ExitCode) 
            or nameof(GameSession.HasCrashReport) 
            or nameof(GameSession.CrashReportPath))
        {
            Broadcast("SessionStateChanged", new
            {
                GamePath = session.GamePath,
                DisplayName = session.DisplayName,
                State = session.RunStateText,
                ProcessId = session.ProcessId,
                ExitCode = session.ExitCode,
                HasCrashReport = session.HasCrashReport,
                CrashReportPath = session.CrashReportPath
            });
        }
    }

    private void OnSessionEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems == null) return;
        if (sender is not ObservableCollection<GameLogEntry> entries) return;

        // Find which session these entries belong to
        var session = _trackedSessions.Values.FirstOrDefault(s => s.Entries == entries);
        if (session == null) return;

        foreach (GameLogEntry entry in e.NewItems)
        {
            Broadcast("LogEntryReceived", new
            {
                GamePath = session.GamePath,
                Timestamp = entry.Timestamp,
                Level = entry.Level.ToString(),
                Source = entry.Source.ToString(),
                Message = entry.Message,
                RawPayload = entry.RawPayload,
                IsSynthetic = entry.IsSynthetic
            });
        }
    }

    private void HookNotifications()
    {
        _notificationService.ActiveNotifications.CollectionChanged += OnNotificationsChanged;
        foreach (var notification in _notificationService.ActiveNotifications)
        {
            TrackNotification(notification);
        }
    }

    private void OnNotificationsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (Notification notification in e.NewItems)
            {
                TrackNotification(notification);
            }
        }
        if (e.OldItems != null)
        {
            foreach (Notification notification in e.OldItems)
            {
                UntrackNotification(notification);
            }
        }
    }

    private void TrackNotification(Notification notification)
    {
        if (!_trackedNotifications.TryAdd(notification.Id, notification)) return;

        notification.PropertyChanged += OnNotificationPropertyChanged;

        // Broadcast initial notification state
        Broadcast("NotificationStateChanged", new
        {
            Id = notification.Id,
            Title = notification.Title,
            Message = notification.Message,
            Type = notification.Type.ToString(),
            Progress = notification.Progress,
            IsIndeterminate = notification.IsIndeterminate,
            IsCompleted = notification.IsCompleted,
            Timestamp = notification.Timestamp,
            IsCancellable = notification.IsCancellable
        });
    }

    private void UntrackNotification(Notification notification)
    {
        if (_trackedNotifications.TryRemove(notification.Id, out var oldNotification))
        {
            oldNotification.PropertyChanged -= OnNotificationPropertyChanged;

            Broadcast("NotificationRemoved", new
            {
                Id = notification.Id
            });
        }
    }

    private void OnNotificationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not Notification notification) return;

        Broadcast("NotificationStateChanged", new
        {
            Id = notification.Id,
            Title = notification.Title,
            Message = notification.Message,
            Type = notification.Type.ToString(),
            Progress = notification.Progress,
            IsIndeterminate = notification.IsIndeterminate,
            IsCompleted = notification.IsCompleted,
            Timestamp = notification.Timestamp,
            IsCancellable = notification.IsCancellable
        });
    }

    private void Broadcast<T>(string eventType, T data)
    {
        if (_sockets.IsEmpty) return;

        var payload = JsonSerializer.Serialize(new
        {
            Event = eventType,
            Timestamp = DateTimeOffset.UtcNow,
            Data = data
        }, new JsonSerializerOptions
        {
            WriteIndented = false,
            Converters = { new JsonStringEnumConverter() }
        });

        var bytes = Encoding.UTF8.GetBytes(payload);
        var segment = new ArraySegment<byte>(bytes);

        _ = Task.Run(async () =>
        {
            foreach (var kvp in _sockets)
            {
                var socket = kvp.Value;
                if (socket.State != WebSocketState.Open) continue;

                try
                {
                    await socket.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("Failed to send WebSocket broadcast to client {Id}: {Message}", kvp.Key, ex.Message);
                }
            }
        });
    }

    public void Dispose()
    {
        _runtimeService.Sessions.CollectionChanged -= OnSessionsChanged;
        foreach (var session in _trackedSessions.Values)
        {
            session.PropertyChanged -= OnSessionPropertyChanged;
            session.Entries.CollectionChanged -= OnSessionEntriesChanged;
        }

        _notificationService.ActiveNotifications.CollectionChanged -= OnNotificationsChanged;
        foreach (var notification in _trackedNotifications.Values)
        {
            notification.PropertyChanged -= OnNotificationPropertyChanged;
        }

        foreach (var socket in _sockets.Values)
        {
            try { socket.Dispose(); } catch { }
        }
    }
}
