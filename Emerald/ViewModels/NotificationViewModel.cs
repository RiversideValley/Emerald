using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Emerald.CoreX.Notifications;
using Emerald.Helpers;
using Microsoft.UI.Xaml.Controls;
using System;

namespace Emerald.ViewModels;

public class NotificationViewModel : ObservableObject, IDisposable
{
    private readonly Notification _model;
    private readonly INotificationService _service;

    public string Id => _model.Id;
    public string Title => _model.Title;
    public string Message => _model.Message;
    public NotificationType Type => _model.Type;
    public double Progress => _model.Progress;
    public bool IsIndeterminate => _model.IsIndeterminate;
    public bool IsCancellable => _model.IsCancellable;
    public bool IsCompleted => _model.IsCompleted;
    public Exception Exception => _model.Exception;
    public bool IsErrorWithException => Exception != null;
    public IRelayCommand CancelCommand { get; }
    public IRelayCommand DismissCommand { get; }
    public IRelayCommand ViewErrorCommand { get; }

    public NotificationViewModel(Notification model, INotificationService service)
    {
        _model = model;
        _service = service;

        CancelCommand = new RelayCommand(OnCancel, () => IsCancellable);
        DismissCommand = new RelayCommand(OnDismiss);
        ViewErrorCommand = new RelayCommand(OnViewError, () => Type == NotificationType.Error && Exception != null);

        _model.PropertyChanged += (_, __) => OnModelChanged();
    }

    private void OnCancel()
    {
        _service.Cancel(_model.Id);
    }

    private void OnDismiss()
    {
        _service.RemoveNotification(Id);
    }

    private async void OnViewError()
    {
       await MessageBox.Show("Error", Exception.ToString() + "\nStackTrace: " + Exception.StackTrace, Helpers.Enums.MessageBoxButtons.Ok);

    }

    private void OnModelChanged()
    {
        OnPropertyChanged(string.Empty);
    }

    public void Dispose()
    {
        _model.PropertyChanged -= (_, __) => OnModelChanged();
    }
}
