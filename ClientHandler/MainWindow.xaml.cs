using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Windows.ApplicationModel;
using Windows.ApplicationModel.AppService;
using System.IO;
using Wpf.Ui.Controls;
using Microsoft.Win32;
namespace ClientHandler
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : UiWindow
    {
        string logs = "";
        public MainWindow()
        {
            InitializeComponent();
            Program.ProcessUtil.OutputReceived += ProcessUtil_OutputReceived;
            Program.ProcessUtil.Exited += ProcessUtil_Exited;
            Program.ProcessUtil.StartWithEvents();
        }

        private void ProcessUtil_Exited(object? sender, EventArgs e)
        {
            this.Dispatcher.Invoke(async delegate
            {
                btnKillGame.IsEnabled = false;
                Snackbar? bar = new Snackbar();
                g.Children.Add(bar);
                await bar.ShowAsync("Minecraft Closed", "You can close this window now!");
                g.Children.Remove(bar);
                bar = null;
            });
        }


        private void ProcessUtil_OutputReceived(object? sender, string e)
        {
            var x = new Windows.Foundation.Collections.ValueSet();
            x.Add("Log", e);
            try
            {
                _ = AppServiceHandler.Connection?.SendMessageAsync(x);
            }
            catch { }
            this.Dispatcher.Invoke(delegate
            {
                logs += e;
                txtLogs.AppendText(e);
                txtLogs.ScrollToEnd();
            });
        }

        private void btnKillGame_Click(object sender, RoutedEventArgs e)
        {
            Program.ProcessUtil?.Process.Kill();
        }

        private void btnSaveLogs_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Title = "Save Log as";
            sfd.Filter = "All files|*.";
            if (sfd.ShowDialog() == true)
            {
                var sw = File.CreateText(sfd.FileName);
                sw.Write(logs);
                sw.Close();
            }
        }
    }
}
