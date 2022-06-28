using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using System.Collections.ObjectModel;
using Windows.UI;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace SDLauncher_UWP.UserControls
{
    public sealed partial class TaskListView : UserControl
    {
        public ObservableCollection<Task> TasksCompleted { get;private set; }
        public ObservableCollection<Task> CurrentTasks { get;private set; }
        private int WholeTaskCount = 0;
        
        public TaskListView()
        {
            this.InitializeComponent();
            this.TasksCompleted = new ObservableCollection<Task>();
            this.CurrentTasks = new ObservableCollection<Task>();
            RefreshTasks();
        }
        public int AddTask(string name,int? ID = null)
        {
            if (ID == null)
            {
                WholeTaskCount++;
                CurrentTasks.Add(new Task(name, WholeTaskCount));
                RefreshTasks();
                return WholeTaskCount;
            }
            else
            {
                CurrentTasks.Add(new Task(name, (int)ID));
                RefreshTasks();
                return (int)ID;
            }
        }
        public bool CompleteTask(int id,bool success)
        {
            try
            {
                var item = from t in CurrentTasks where t.ID.Equals(id) select t;
                if (item != null)
                {
                    var itm = item.FirstOrDefault();
                    if (itm != null)
                    {
                        CurrentTasks.Remove(itm);
                        itm.DateAdded = DateTime.Now;
                        if (!success)
                        {
                            itm.BorderBrush = new SolidColorBrush(Colors.Red);
                        }
                        else
                        {
                            itm.BorderBrush = new SolidColorBrush(Colors.Green);
                        }
                        TasksCompleted.Add(itm);
                        RefreshTasks();
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }
        public void RefreshTasks()
        {
            var emptyTasks = new List<Task>();
            foreach (var item in CurrentTasks)
            {
                if (item.ID == 1000)
                {
                    emptyTasks.Add(item);
                }
            }
            foreach (var item in emptyTasks)
            {
                CurrentTasks.Remove(item);
            }
            //
            emptyTasks = new List<Task>();
            foreach (var item in TasksCompleted)
            {
                if (item.ID == 1000)
                {
                    emptyTasks.Add(item);
                }
            }
            foreach (var item in emptyTasks)
            {
                TasksCompleted.Remove(item);
            }
            //Add Empty Tasks
            if (CurrentTasks.Count < 1)
            {
                CurrentTasks.Add(new Task("Empty", 1000) { RingVisibility = Visibility.Collapsed});
            }
            //
            if (TasksCompleted.Count < 1)
            {
                TasksCompleted.Add(new Task("Empty", 1000));
            }
            lvRuning.ItemsSource = CurrentTasks;
            lvDone.ItemsSource = TasksCompleted;
        }
    }
    public class Task : INotifyPropertyChanged
    {

        public event PropertyChangedEventHandler PropertyChanged = delegate { };
        private Visibility ringVisibility = Visibility.Visible;
        public string Name { get; set; }
        public Visibility RingVisibility { get { return ringVisibility; }set { ringVisibility = value;OnPropertyChanged(); } }
        public int ID { get; private set; }
        public DateTime DateAdded { get; set; }
        public Brush BorderBrush { get { return BBrush; } set { BBrush = value; OnPropertyChanged(); } }

        private Brush BBrush;

        public Task(string name, int iD, Brush borderBrush = null)
        {
            Name = name;
            DateAdded = DateTime.Now;
            ID = iD;
            BorderBrush = borderBrush;
        }
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
