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
using Wpf.Ui.Controls;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
namespace ClientHandler
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : UiWindow
    {
        public MainWindow()
        {
            InitializeComponent();
            Program.ProcessUtil.OutputReceived += ProcessUtil_OutputReceived;
            Program.ProcessUtil.Exited += ProcessUtil_Exited;
            Program.ProcessUtil.StartWithEvents();
        }

        private void ProcessUtil_Exited(object? sender, EventArgs e)
        {
            Application.Current.Shutdown();
        }


        private void ProcessUtil_OutputReceived(object? sender, string e)
        {
            var x = new Windows.Foundation.Collections.ValueSet();
            x.Add("Log", e);
            try
            {
                _ = AppServiceHandler.Connection.SendMessageAsync(x);
            }
            catch { }
            this.Dispatcher.Invoke(delegate
            {
                txtLogs.AppendText(e);
            });
        }

        private void btnKillGame_Click(object sender, RoutedEventArgs e)
        {
            Program.ProcessUtil.Process.Kill();
        }

        private void btnSaveLogs_Click(object sender, RoutedEventArgs e)
        {
        }
    }
}
