using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.ComponentModel;

namespace Emerald.WinUI.UserControls
{
    public sealed partial class TaskViewGrid : Grid, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public event EventHandler? ClearAllClicked;

        public void Set<T>(ref T obj, T value, string name = null)
        {
            obj = value;
            InvokePropertyChanged(name);
        }

        public void InvokePropertyChanged(string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private TaskView _TaskView;
        public TaskView TaskView { get => _TaskView; set => Set(ref _TaskView, value); }

        public TaskViewGrid(TaskView taskView)
        {
            InitializeComponent();
            TaskView = taskView;
        }

        private void bntClearAll_Click(object sender, RoutedEventArgs e)
        {
            ClearAllClicked?.Invoke(this, new());
        }
    }
}
