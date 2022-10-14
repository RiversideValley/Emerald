using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Emerald.WinUI.UserControls
{
    public sealed partial class LogsView : UserControl
    {
        private int lastID = 0;

        private ObservableCollection<Models.Log> Logs { get; set; } = new();
        public List<Models.Log> AllLogs { get => Logs.ToList(); }

        public LogsView()
        {
            this.InitializeComponent();
            lv.ItemsSource = Logs;
        }
        public int AddProgressLog(string content, int progress = 0, InfoBarSeverity severity = InfoBarSeverity.Informational, bool IsIndeterminate = false, object UniqueThings = null, ObservableCollection<UIElement> customCOntrols = null)
        {
            var l = new Models.ProgressLog(content, DateTime.Now, lastID, progress, severity, IsIndeterminate, UniqueThings, customCOntrols);
            lastID++;
            Logs.Add(l);
            return l.ID;
        }
        public object GetUniqueThings(int ID)
        {
            var l = Logs.FirstOrDefault(l => l.ID == ID);
            try
            {
                return l.UniqueThings;
            }
            catch
            {

                return null;
            }
        }
        public int[] SearchByUniqueThingsToString(string uniquethings)
        {
            try
            {
                return Logs.Where(x => x.UniqueThings != null).Where(x => x.UniqueThings.ToString() == uniquethings).Select(x => x.ID).ToArray();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.ToString());
                return Array.Empty<int>();
            }
        }
        public int AddStringLog(string content, InfoBarSeverity severity = InfoBarSeverity.Informational, object uniquethings = null, ObservableCollection<UIElement> customCOntrols = null)
        {
            var l = new Models.StringLog(content, DateTime.Now, lastID, severity, uniquethings, customCOntrols);
            lastID++;
            Logs.Add(l);
            return l.ID;
        }
        public bool RemoveLog(int ID)
        {
            var l = Logs.FirstOrDefault(l => l.ID == ID);
            try
            {
                Logs.Remove(l);
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
                Logs.FirstOrDefault(l => l.ID == ID).Content = content;
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
                Logs.FirstOrDefault(l => l.ID == ID).CustomControls = controls;
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
                ((Models.ProgressLog)Logs.FirstOrDefault(l => l.ID == ID)).Progress = progress;
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
            lv.ItemsSource = Logs;
        }
        public bool ChangeIndeterminate(int ID, bool isIndeterminate)
        {
            try
            {
                ((Models.ProgressLog)Logs.FirstOrDefault(l => l.ID == ID)).IsIndeterminate = isIndeterminate;
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
                Logs.FirstOrDefault(l => l.ID == ID).Severity = severity;
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.ToString());
                return false;
            }
        }
    }
}
