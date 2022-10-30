using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;

namespace Emerald.WinUI.Models
{
    public interface ITask
    {
        public string Content { get; set; }
        public string Description { get; set; }
        public object UniqueThings { get; set; }
        public DateTime Time { get; set; }
        public int ID { get; set; }
        public InfoBarSeverity Severity { get; set; }
        public ObservableCollection<UIElement> CustomControls { get; set; }
        public Visibility RemoveButtonVisibility { get; set; }
        public Visibility IconVisibility { get; }
    }
    public class StringTask : Model, ITask
    {
        private string _Content;
        public string Content { get => _Content; set => Set(ref _Content, value); }

        private string _Description;
        public string Description { get => _Description; set => Set(ref _Description, value); }

        public DateTime Time { get; set; }

        public int ID { get; set; }

        private InfoBarSeverity _Severty;
        public InfoBarSeverity Severity { get => _Severty; set => Set(ref _Severty, value); }

        public object UniqueThings { get; set; }

        public ObservableCollection<UIElement> _CustomControls;
        public ObservableCollection<UIElement> CustomControls { get => _CustomControls; set => Set(ref _CustomControls, value); }

        public Visibility _RemoveButtonVisibility = Visibility.Collapsed;
        public Visibility RemoveButtonVisibility { get => _RemoveButtonVisibility; set => Set(ref _RemoveButtonVisibility, value); }

        public Visibility IconVisibility { get => RemoveButtonVisibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible; }

        public StringTask(string content, DateTime time, int iD, InfoBarSeverity severity, object uniqueThings = null, ObservableCollection<UIElement> customCOntrols = null)
        {
            Content = content;
            Time = time;
            ID = iD;
            this.Severity = severity;
            UniqueThings = uniqueThings;
            CustomControls = customCOntrols;
            MainWindow.TaskView.ActualThemeChanged += (_, _) => this.InvokePropertyChanged();
        }
    }

    public class ProgressTask : Model, ITask
    {
        public object UniqueThings { get; set; }

        private string _Content;
        public string Content { get => _Content; set => Set(ref _Content, value); }

        private string _Description;
        public string Description { get => _Description; set => Set(ref _Description, value); }

        public DateTime Time { get; set; }

        public int ID { get; set; }

        private int _Progress;
        public int Progress { get => _Progress; set => Set(ref _Progress, value); }

        private bool _IsIndeterminate;
        public bool IsIndeterminate { get => _IsIndeterminate; set => Set(ref _IsIndeterminate, value); }

        public Visibility ProgressVisibility { get => Progress != 100 || IsIndeterminate ? Visibility.Visible : Visibility.Collapsed; }

        private InfoBarSeverity _Severty;
        public InfoBarSeverity Severity { get => _Severty; set => Set(ref _Severty, value); }

        public ObservableCollection<UIElement> _CustomControls;
        public ObservableCollection<UIElement> CustomControls { get => _CustomControls; set => Set(ref _CustomControls, value); }

        public Visibility _RemoveButtonVisibility = Visibility.Collapsed;
        public Visibility RemoveButtonVisibility { get => _RemoveButtonVisibility; set => Set(ref _RemoveButtonVisibility, value); }

        public Visibility IconVisibility { get => RemoveButtonVisibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible; }

        public ProgressTask(string content, DateTime time, int iD, int progress = 0, InfoBarSeverity severity = InfoBarSeverity.Informational, bool isIndeterminate = false, object uniquethings = null, ObservableCollection<UIElement> customCOntrols = null)
        {
            Content = content;
            Time = time;
            ID = iD;
            Progress = progress;
            IsIndeterminate = isIndeterminate;
            UniqueThings = uniquethings;
            Severity = severity;
            CustomControls = customCOntrols;
            MainWindow.TaskView.ActualThemeChanged += (_, _) => this.InvokePropertyChanged();
        }
    }
}
