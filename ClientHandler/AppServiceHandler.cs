using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;
#pragma warning disable CS8602 // Dereference of a possibly null reference.

namespace ClientHandler
{
    public class AppServiceHandler
    {
        public static AppServiceConnection? Connection { get; set; }
        public static async void InitializeAppServiceConnection()
        {
            Connection = new AppServiceConnection();
            Connection.AppServiceName = "SDL Logging System";
            Connection.PackageFamilyName = Package.Current.Id.FamilyName;
            Connection.RequestReceived += Connection_RequestReceived; ;
            Connection.ServiceClosed += Connection_ServiceClosed; ;

            AppServiceConnectionStatus status = await Connection.OpenAsync();
            if (status != AppServiceConnectionStatus.Success)
            {
            }
        }

        public static void Connection_ServiceClosed(AppServiceConnection sender, AppServiceClosedEventArgs args)
        {
        }

        public static void Connection_RequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            foreach (var item in args.Request.Message)
            {
                if(item.Key == "Command")
                {
                    if(item.Value.ToString() == "KillMC")
                    {
                        Program.ProcessUtil.Process.Kill();
                    }
                }
            }
            
        }
    }
}
