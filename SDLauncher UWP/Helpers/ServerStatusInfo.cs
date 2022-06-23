using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SDLauncher_UWP.Helpers
{
    public class ServerStatusInfo
    {

        public class Root
        {
            public bool online { get; set; }
            public string host { get; set; }
            public int port { get; set; }
            public Response response { get; set; }
        }

        public class Response
        {
            public Version version { get; set; }
            public Players players { get; set; }
            public Motd motd { get; set; }
            public string favicon { get; set; }
            public Srv_Record srv_record { get; set; }
        }

        public class Version
        {
            public string name { get; set; }
            public int protocol { get; set; }
        }

        public class Players
        {
            public int online { get; set; }
            public int max { get; set; }
            public object[] sample { get; set; }
        }

        public class Motd
        {
            public string raw { get; set; }
            public string clean { get; set; }
            public string html { get; set; }
        }

        public class Srv_Record
        {
            public string host { get; set; }
            public int port { get; set; }
        }

    }


}
