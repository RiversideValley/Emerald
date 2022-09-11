using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;
using Windows.UI.Popups;

namespace Emerald.UWP.Helpers
{
    public class AppServiceHandler : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged = delegate { };
        private string mainLog;
        public string MainLog
        {
            get { return mainLog; }
            set { mainLog = value; OnPropertyChanged(); }
        }
       public AppServiceHandler()
       {
            App.Connection.RequestReceived += Connection_RequestReceived;
        }
        public void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        private void Connection_RequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            string LittleLog = (string)args.Request.Message["Log"];
            mainLog += LittleLog;
        }
    }
}
