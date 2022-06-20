using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Media.Imaging;
using SDLauncher_UWP.Helpers;

namespace SDLauncher_UWP.DataTemplates
{
    public class ServerTemplate : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged = delegate { };
        //server
        private string server;
        public string Server { get => server; private set { server = value; OnPropertyChanged(); UpdateStatus(); } }
        //Vers
        private string ver;
        public string Versions { get => ver; set { ver = value; OnPropertyChanged(); } }
        //port
        private int port;
        public int Port { get => port; private set { port = value; OnPropertyChanged(); UpdateStatus(); } }
        //max players
        private int maxPlayers;
        public int MaxPlayers { get => maxPlayers; private set { maxPlayers = value; OnPropertyChanged(); } }
        //online players
        private int minPlayers;
        public int OnlinePlayers { get => minPlayers; private set { minPlayers = value; OnPropertyChanged(); } }
        //favicon
        private string favicon;
        public string Favicon { get => favicon; private set { favicon = value; OnPropertyChanged(); } }
        //MOTD
        private string motd;
        public string MOTD { get => motd; private set { motd = value; OnPropertyChanged(); } }

        public ServerTemplate(string server, int port)
        {
            Server = server;
            Port = port;
            UpdateStatus();
        }

        public async void UpdateStatus()
        {
            try
            {
                var status = JSONConverter.ConvertToServerStatus(await Util.DownloadText("https://api.mcstatus.io/status/java/" + Server.Trim() + ":" + Port.ToString()));
                if (status.online)
                {
                    Server = status.host;
                    Port = status.port;
                    MaxPlayers = status.response.players.max;
                    Versions = status.response.version.name;
                    OnlinePlayers = status.response.players.online;
                    Favicon = status.response.favicon;
                }
            }
            catch (Exception)
            {

            }
        }
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
