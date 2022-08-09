using SDLauncher.Core.Args;
using SDLauncher.Core.Store;
using System;
using System.Collections.Generic;
using System.Text;

namespace SDLauncher.Core
{
    public static class MainCore
    {
        public static event EventHandler<StatusChangedEventArgs> StatusChanged = delegate { };
        public static event EventHandler<UIChangeRequestedEventArgs> UIChanged = delegate { };
        public static event EventHandler<ProgressChangedEventArgs> ProgressChanged = delegate { };
        public static SDLauncher Launcher { get; set; }
        public static Labrinth Labrinth { get; set; }
        public static string GlacierClientVersion { get; set; } = "";
        public static void Intialize()
        {
            Launcher = new SDLauncher();
            Launcher.FileOrProgressChanged += ProgressChangedEvent;
            Launcher.StatusChanged += (s, e) => StatusChanged(s, e);
            Launcher.UIChangeRequested += UIChangedEvent;
            //
            Labrinth = new Labrinth();
            Labrinth.ProgressChanged += ProgressChangedEvent;
            Labrinth.MainUIChangeRequested += UIChangedEvent;
            Labrinth.StatusChanged += (s, e) => StatusChanged(Labrinth, new StatusChangedEventArgs(s.ToString()));
            
        }


        private static void UIChangedEvent(object sender, UIChangeRequestedEventArgs e)
        {
            UIChanged(sender, e);
        }

        private static void ProgressChangedEvent(object sender, ProgressChangedEventArgs e)
        {
            ProgressChanged(sender, e);
        }
    }
}
