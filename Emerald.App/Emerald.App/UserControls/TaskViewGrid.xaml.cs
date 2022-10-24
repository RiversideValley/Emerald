using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.ComponentModel;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Emerald.WinUI.UserControls
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
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
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private TaskView _TaskView;
        public TaskView TaskView { get => _TaskView; set => Set(ref _TaskView, value); }
        public TaskViewGrid(TaskView taskView)
        {
            this.InitializeComponent();
            TaskView = taskView;
        }

        private void bntClearAll_Click(object sender, RoutedEventArgs e)
        {
            ClearAllClicked?.Invoke(this, new());
        }
    }
}
