namespace Emerald.Core.Tasks
{
    public static class TasksHelper
    {
        public static event EventHandler<TaskEventArgs> TaskAddRequested = delegate { };

        public static event EventHandler<ProgressTaskEventArgs> ProgressTaskEditRequested = delegate { };

        public static event EventHandler<TaskCompletedEventArgs> TaskCompleteRequested = delegate { };

        private static int AllTaksCount { get; set; } = -1;

        public static int AddTask(string name, string message = null)
        {
            AllTaksCount++;
            TaskAddRequested(null, new TaskAddRequestedEventArgs(name, AllTaksCount, message));

            return AllTaksCount;
        }

        public static int AddTask(Localized name, string message = null)
        {
            AllTaksCount++;
            TaskAddRequested(null, new TaskAddRequestedEventArgs(name.ToString(), AllTaksCount, message));

            return AllTaksCount;
        }

        public static int AddProgressTask(Localized name, int value = 0, int maxVal = 100, int minVal = 0, string message = null)
        {
            AllTaksCount++;
            TaskAddRequested(null, new ProgressTaskEventArgs(name.ToString(), AllTaksCount, maxVal, minVal, value, message));

            return AllTaksCount;
        }

        public static int AddProgressTask(string name, int value = 0, int maxVal = 100, int minVal = 0, string message = null)
        {
            AllTaksCount++;
            TaskAddRequested(null, new ProgressTaskEventArgs(name.ToString(), AllTaksCount, maxVal, minVal, value, message));

            return AllTaksCount;
        }

        public static void EditProgressTask(int ID, int value = 0, int maxVal = 100, int minVal = 0, string message = null)
        {
            ProgressTaskEditRequested(null, new ProgressTaskEventArgs(null, ID, maxVal, minVal, value, message));
        }

        public static void CompleteTask(int ID, bool success = true, string message = null)
        {
            TaskCompleteRequested(null, new TaskCompletedEventArgs(ID, success, message));
        }
    }

    public interface TaskEventArgs
    {
        public int ID { get; }
        public string Message { get; }
    }

    public class TaskCompletedEventArgs : EventArgs, TaskEventArgs
    {
        public int ID { get; private set; }
        public bool Success { get; private set; }
        public string Message { get; private set; }
        public TaskCompletedEventArgs(int iD, bool success, string message = null)
        {
            ID = iD;
            Success = success;
            Message = message;
        }
    }

    public class TaskAddRequestedEventArgs : EventArgs, TaskEventArgs
    {
        public int ID { get; private set; }
        public string Name { get; private set; }
        public string Message { get; private set; }
        public TaskAddRequestedEventArgs(string name, int taskID, string message)
        {
            ID = taskID;
            Name = name;
            Message = message;
        }
    }

    public class ProgressTaskEventArgs : EventArgs, TaskEventArgs
    {
        public int ID { get; private set; }

        public int MaxValue { get; private set; }

        public int MinValue { get; private set; }

        public int Value { get; private set; }

        public string Name { get; private set; }

        public string Message { get; private set; }

        public ProgressTaskEventArgs(
            string name,
            int taskID,
            int maxProg = 100,
            int minProg = 0,
            int prog = 0,
            string message = null)
        {
            ID = taskID;
            Name = name;
            MaxValue = maxProg;
            MinValue = minProg;
            Value = prog;
            Message = message;
        }
    }
}
