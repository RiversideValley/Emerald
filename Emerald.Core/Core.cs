using Emerald.Core.Args;
using Emerald.Core.Store;

namespace Emerald.Core
{
    [Obsolete("Create classes directly")]
    public static class MainCore
    {
        public static event EventHandler<StatusChangedEventArgs> StatusChanged = delegate { };

        public static event EventHandler<UIChangeRequestedEventArgs> UIChanged = delegate { };

        public static event EventHandler<ProgressChangedEventArgs> ProgressChanged = delegate { };

        public static Emerald Launcher { get; set; }

        public static Labrinth Labrinth { get; set; }

        public static string GlacierClientVersion { get; set; } = "";

        public static void Intialize()
        {
            Launcher = new Emerald();
            Launcher.FileOrProgressChanged += ProgressChangedEvent;
            Launcher.StatusChanged += (s, e) => StatusChanged(s, e);
            Launcher.UIChangeRequested += UIChangedEvent;

            Labrinth = new Labrinth();
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
