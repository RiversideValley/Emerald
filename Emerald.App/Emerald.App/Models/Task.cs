using CommunityToolkit.Mvvm.ComponentModel;
using Emerald.WinUI.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;

namespace Emerald.WinUI.Models
{
    public partial class Task : Model
    {
        [ObservableProperty]
        private string _Content;

        [ObservableProperty]
        private string _Description;

        public DateTime Time { get; set; }

        public int ID { get; set; }

        [ObservableProperty]
        private InfoBarSeverity _Severity;

        [ObservableProperty]
        private ObservableCollection<UIElement> _CustomControls;

        [ObservableProperty]
        public Visibility _RemoveButtonVisibility = Visibility.Collapsed;

        public Visibility IconVisibility => RemoveButtonVisibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;

        public bool HasDescription() => !Description.IsNullEmptyOrWhiteSpace();
        public Task(string content, DateTime time, int iD, InfoBarSeverity severity, ObservableCollection<UIElement> customCOntrols = null)
        {
            Content = content;
            Time = time;
            ID = iD;
            Severity = severity;
            CustomControls = customCOntrols;
            (App.Current.MainWindow.Content as FrameworkElement).ActualThemeChanged += (_, _) => InvokePropertyChanged();
        }
    }

    public partial class ProgressTask : Task
    {
        [ObservableProperty]
        private int _Progress;

        [ObservableProperty]
        private bool _IsIndeterminate;

        public Visibility ProgressVisibility => RemoveButtonVisibility == Visibility.Visible ? Visibility.Collapsed : (Progress != 100 || IsIndeterminate ? Visibility.Visible : Visibility.Collapsed);

        public Visibility ProgressTextVisibility => !IsIndeterminate ? Visibility.Visible : Visibility.Collapsed;

        public Visibility IconVisibility => RemoveButtonVisibility == Visibility.Visible || ProgressVisibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        public ProgressTask(string content, DateTime time, int iD, int progress = 0, InfoBarSeverity severity = InfoBarSeverity.Informational, bool isIndeterminate = false, ObservableCollection<UIElement> customCOntrols = null) : base(content, time, iD, severity, customCOntrols)
        {
            IsIndeterminate = isIndeterminate;
            Progress = progress;
            PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(RemoveButtonVisibility) || e.PropertyName == nameof(Progress) || e.PropertyName == nameof(IsIndeterminate))
                    InvokePropertyChanged();
            };
        }
    }
}
