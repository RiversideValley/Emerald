using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace SDLauncher.Core.Tasks
{
    public static class TasksHelper
    {
        public static event EventHandler<TaskAddRequestedEventArgs> TaskAddRequested = delegate { };
        public static event EventHandler<TaskCompletedEventArgs> TaskCompleteRequested = delegate { };
        private static int AllTaksCount { get; set; } = 0;
        public static int AddTask(string name)
        {
            AllTaksCount++;
            TaskAddRequested(null, new TaskAddRequestedEventArgs(name, AllTaksCount));
            return AllTaksCount;
        }
        public static void CompleteTask(int ID, bool success = true)
        {
            TaskCompleteRequested(null, new TaskCompletedEventArgs(ID, success));
        }
    }
    public class TaskCompletedEventArgs : EventArgs
    {
        public int ID { get; private set; }
        public bool Success { get; private set; }
        public TaskCompletedEventArgs(int iD, bool success)
        {
            ID = iD;
            Success = success;
        }
    }
    public class TaskAddRequestedEventArgs : EventArgs
    {
        public int TaskID { get; private set; }
        public string Name { get; private set; }
        public TaskAddRequestedEventArgs(string name, int taskID)
        {
            TaskID = taskID;
            Name = name;
        }
    }
}
