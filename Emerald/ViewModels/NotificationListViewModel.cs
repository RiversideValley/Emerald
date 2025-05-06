using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Emerald.CoreX.Notifications;

namespace Emerald.ViewModels;

public class NotificationListViewModel : ObservableObject
{
    private readonly INotificationService _service;
    public ObservableCollection<NotificationViewModel> Notifications { get; } = new();

    public NotificationListViewModel(INotificationService notificationService)
    {
        _service = notificationService;

        // Load existing
        foreach (var n in _service.ActiveNotifications)
            Add(n);

        _service.ActiveNotifications.CollectionChanged += ActiveNotifications_CollectionChanged;
    }

    private void ActiveNotifications_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
            foreach (Notification n in e.NewItems)
                Add(n);
        if (e.OldItems != null)
            foreach (Notification n in e.OldItems)
                Remove(n);
    }

    private void Add(Notification model)
    {
        Notifications.Add(new NotificationViewModel(model, _service));
    }

    private void Remove(Notification model)
    {
        var vm = Notifications.FirstOrDefault(x => x.Id == model.Id);
        if (vm != null)
            Notifications.Remove(vm);
    }
}
