using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace SDLauncher_UWP.UserControls
{
    public sealed partial class TaskListView : UserControl
    {
        public List<Task> TasksCompleted { get;private set; }
        public List<Task> CurrentTasks { get;private set; }
        private int WholeTaskCount = 0;
        
        public TaskListView()
        {
            this.InitializeComponent();
            this.TasksCompleted = new List<Task>();
            this.CurrentTasks = new List<Task>();
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
        public bool CompleteTask(int id)
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
                        TasksCompleted.Add(itm);
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
            lvRuning.ItemsSource = null;
            lvDone.ItemsSource = null;
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
            if (CurrentTasks.Count == 0)
            {
                CurrentTasks.Add(new Task("Empty", 1000));
            }
            if (TasksCompleted.Count == 0)
            {
                TasksCompleted.Add(new Task("Empty", 1000));
            }
            lvRuning.ItemsSource = CurrentTasks;
            lvDone.ItemsSource = TasksCompleted;
        }
    }
    public class Task
    {
        public string Name { get; set; }
        public int ID { get; private set; }
        public DateTime DateAdded { get; set; }
        public Task(string name, int iD)
        {
            Name = name;
            DateAdded = DateTime.Now;
            ID = iD;
        }
    }
}
