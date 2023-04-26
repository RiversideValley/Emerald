using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Emerald.WinUI.UserControls
{
    public sealed partial class TaskView : UserControl
    {
        private int lastID = 0;

        private ObservableCollection<Models.Task> Tasks { get; set; } = new();
        public Models.Task[] AllTasks { get => Tasks.ToArray(); }

        public TaskView()
        {
            InitializeComponent();
            Tasks.CollectionChanged += (_, _) =>
                txtEmpty.Visibility = Tasks.Any() ? Visibility.Collapsed : Visibility.Visible;
            lv.ItemsSource = Tasks;
        }
        public int AddProgressTask(string content, int progress = 0, InfoBarSeverity severity = InfoBarSeverity.Informational, bool IsIndeterminate = false, object UniqueThings = null, ObservableCollection<UIElement> customCOntrols = null)
        {
            var l = new Models.ProgressTask(content, DateTime.Now, lastID, progress, severity, IsIndeterminate, customCOntrols);
            lastID++;
            Tasks.Add(l);
            return l.ID;
        }
        public void ClearAll()
        {
            for (int i = Tasks.Count - 1; i >= 0; i--)
            {
                Tasks.RemoveAt(i);
            }
        }
        public int AddStringTask(string content, InfoBarSeverity severity = InfoBarSeverity.Informational, ObservableCollection<UIElement> customCOntrols = null)
        {
            var l = new Models.Task(content, DateTime.Now, lastID, severity, customCOntrols);
            lastID++;
            Tasks.Add(l);
            return l.ID;
        }
        public bool RemoveTask(int ID)
        {
            var l = Tasks.FirstOrDefault(l => l.ID == ID);
            try
            {
                Tasks.Remove(l);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.ToString());
                return false;
            }
        }
        public bool ChangeContent(int ID, string content)
        {
            try
            {
                Tasks.FirstOrDefault(l => l.ID == ID).Content = content;
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.ToString());
                return false;
            }
        }
        public bool ChangeDescription(int ID, string description)
        {
            try
            {
                Tasks.FirstOrDefault(l => l.ID == ID).Description = description;
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.ToString());
                return false;
            }
        }
        public bool ChangeCustomControls(int ID, ObservableCollection<UIElement> controls)
        {
            try
            {
                Tasks.FirstOrDefault(l => l.ID == ID).CustomControls = controls;
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.ToString());
                return false;
            }
        }
        public bool ChangeProgress(int ID, int progress)
        {
            try
            {
                ((Models.ProgressTask)Tasks.FirstOrDefault(l => l.ID == ID)).Progress = progress;
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.ToString());
                return false;
            }
        }
        public void Refresh()
        {
            lv.ItemsSource = null;
            lv.ItemsSource = Tasks;
        }
        public bool ChangeIndeterminate(int ID, bool isIndeterminate)
        {
            try
            {
                ((Models.ProgressTask)Tasks.FirstOrDefault(l => l.ID == ID)).IsIndeterminate = isIndeterminate;
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.ToString());
                return false;
            }
        }
        public bool ChangeSeverty(int ID, InfoBarSeverity severity)
        {
            try
            {
                Tasks.FirstOrDefault(l => l.ID == ID).Severity = severity;
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.ToString());
                return false;
            }
        }

        private void btnIcon_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            ((sender as Grid).DataContext as Models.Task).RemoveButtonVisibility = Visibility.Visible;
        }

        private void btnIcon_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            ((sender as Grid).DataContext as Models.Task).RemoveButtonVisibility = Visibility.Collapsed;
        }

        private void btnRemoveTask_Click(object sender, RoutedEventArgs e)
        {
            Tasks.Remove((sender as Button).DataContext as Models.Task);
        }

        private void delete_Invoked(SwipeItem sender, SwipeItemInvokedEventArgs args)
        {
            Tasks.Remove(args.SwipeControl.DataContext as Models.Task);
        }
    }
}
