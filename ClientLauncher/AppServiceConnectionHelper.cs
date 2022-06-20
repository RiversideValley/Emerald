using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
namespace ClientLauncher
{
    public class AppServiceConnectionHelper
    {
        //public AppServiceConnection Connection { get; set; }
        //public TypedEventHandler<AppServiceConnection, AppServiceClosedEventArgs> ServiceClosed { get; private set; }
        //private async void InitializeAppServiceConnection()
        //{
        //    Connection = new AppServiceConnection();
        //    Connection.AppServiceName = "SampleInteropService";
        //    Connection.PackageFamilyName = Package.Current.Id.FamilyName;
        //    Connection.RequestReceived += Connection_RequestReceived;
        //    Connection.ServiceClosed += Connection_ServiceClosed;

        //    AppServiceConnectionStatus status = await Connection.OpenAsync();
        //    if (status != AppServiceConnectionStatus.Success)
        //    {
        //        // something went wrong ...
        //        MessageBox.Show(status.ToString());
        //        this.IsEnabled = false;
        //    }
        //}
        //private void ServiceClosed(AppServiceConnection connection, AppServiceClosedEventArgs args)
        //{

        //}
        //public AppServiceConnectionHelper()
        //{
        //    InitializeAppServiceConnection();
        //}
    }
}
