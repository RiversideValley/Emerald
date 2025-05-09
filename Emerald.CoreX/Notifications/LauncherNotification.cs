using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace Emerald.CoreX.Notifications;
public partial class Notification : ObservableObject
{
    [ObservableProperty] private string _id;
    [ObservableProperty] private string _title;
    [ObservableProperty] private string _message;
    [ObservableProperty] private NotificationType _type;
    [ObservableProperty] private double _progress;
    [ObservableProperty] private bool _isIndeterminate;
    [ObservableProperty] private bool _isCompleted;

    [ObservableProperty] private Exception _exception;


    public DateTime Timestamp { get; set; }
    public TimeSpan? Duration { get; set; }
    public bool IsCancellable => CancellationSource != null && !CancellationSource.IsCancellationRequested;
    public CancellationTokenSource? CancellationSource { get; set; }
}
