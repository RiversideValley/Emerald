using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;


namespace Emerald.WinUI.Models
{
    public interface ITask
    {
        public string Content { get; set; }
        public object UniqueThings { get; set; }
        public DateTime Time { get; set; }
        public int ID { get; set; }
        public InfoBarSeverity Severity { get; set; }
        public ObservableCollection<UIElement> CustomControls { get; set; }

    }
    public class StringTask : Model, ITask 
    {
        private string _Content;
        public string Content { get => _Content; set => Set(ref _Content, value); }
        
        public DateTime Time { get; set; }
        
        public int ID { get; set; }
        
        private InfoBarSeverity _Severty;
        public InfoBarSeverity Severity { get => _Severty; set => Set(ref _Severty, value); }
        
        public object UniqueThings { get; set; }

        public ObservableCollection<UIElement> _CustomControls;
        public ObservableCollection<UIElement> CustomControls { get => _CustomControls; set => Set(ref _CustomControls, value); }

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
