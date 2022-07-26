using CmlLib.Core.Downloader;
using System;
using System.Collections.Generic;
using System.Text;

namespace SDLauncher.Core.Args
{
    public class UIChangeRequestedEventArgs : EventArgs
    {
        public bool UI { get; set; }
        public UIChangeRequestedEventArgs(bool ui)
        {
            this.UI = ui;
        }
    }
    public class StatusChangedEventArgs : EventArgs
    {
        public string Status { get; set; }
        public StatusChangedEventArgs(string status)
        {
            this.Status = status;
        }
    }
    public class ProgressChangedEventArgs : EventArgs
    {
        public int? MaxFiles { get; set; }
        //
        public int? CurrentFile { get; set; }
        //
        public int? ProgressPercentage { get; set; }
        public DownloadFileChangedEventArgs DownloadArgs { get; set; }
        public ProgressChangedEventArgs(int? currentfile = null, int? maxfiles = null, int? currentProg = null, DownloadFileChangedEventArgs args = null)
        {
            MaxFiles = maxfiles;
            CurrentFile = currentfile;
            ProgressPercentage = currentProg;
            DownloadArgs = args;
        }
    }
}
