using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace Emerald.Core.Tasks
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
        public static int AddTask(Core.Localized name)
        {
            AllTaksCount++;
            TaskAddRequested(null, new TaskAddRequestedEventArgs(name.ToString(), AllTaksCount));
            return AllTaksCount;
        }
        public static void CompleteTask(int ID, bool success = true)
        {
            TaskCompleteRequested(null, new TaskCompletedEventArgs(ID, success));
        }
        public static void CompleteTask(int ID, bool success = true,string message = null)
        {
            TaskCompleteRequested(null, new TaskCompletedEventArgs(ID, success,message));
        }
    }
    public class TaskCompletedEventArgs : EventArgs
    {
        public int ID { get; private set; }
        public bool Success { get; private set; }
        public string Message { get; private set; }
        public TaskCompletedEventArgs(int iD, bool success,string message = null)
        {
            ID = iD;
            Success = success;
            Message = message;
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
