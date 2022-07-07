using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CmlLib.Core.VersionLoader;
using CmlLib.Core.Auth;
using SDLauncher_UWP.Helpers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;
using SDLauncher_UWP.Views;
using Windows.Storage;
namespace SDLauncher_UWP
{
    public static class vars
    {//some of these are not used (outdated)


        //Events for the vars, a noob way 
        public static event EventHandler ThemeUpdated = delegate { };
        public static event EventHandler BackgroundUpdatd = delegate { };
        public static event EventHandler SessionChanged = delegate { };
        public static event EventHandler VerSelctorChanged = delegate { };


        //Glacier Client

        public static string GlacierClientVersion = "";
        public static async Task<bool> GlacierExists()
        {
            try
            {
                var f = await StorageFolder.GetFolderFromPathAsync(Launcher.Launcher.MinecraftPath.Versions);
                await f.CreateFolderAsync("Glacier Client", CreationCollisionOption.FailIfExists);

                return true;
            }
            catch
            {
                return false;
            }
        }

        //App
        public static bool closing = false;
        private static ElementTheme? theme = ElementTheme.Default;
        public static ElementTheme? Theme { get { return theme; } set { theme = value; ThemeUpdated(theme, new EventArgs()); } }
        private static BitmapImage bg;
        public static BitmapImage BackgroundImage { get { return bg; } set { bg = value; BackgroundUpdatd(bg, new EventArgs()); } }
        public static string BackgroundImagePath = "";
        private static bool cusbg = false;
        public static bool CustomBackground { get { return cusbg; } set { cusbg = value; BackgroundUpdatd(bg, new EventArgs()); } }
        public static bool ShowTips = true;
        public static bool AdminLaunch = true;
        public static bool GameLogs = false;
        public static bool autoLog = false;
        public static bool AutoClose = false;
        public static int LoadedRam = 1024;
        public static SDLauncher Launcher;
        public static RPCHelper SDRPC;
        private static VerSelectors verSelectors = VerSelectors.Normal;
        public static VerSelectors VerSelectors { get { return verSelectors; } set { verSelectors = value; VerSelctorChanged(verSelectors, new EventArgs()); } }
        public static ObservableCollection<Account> Accounts = new ObservableCollection<Account>();
        public static int AccountsCount;
        public static int? CurrentAccountCount;

        //CMLLIB
        public static MSession session { get { return ss; } set { ss = value; SessionChanged(ss, new EventArgs()); } }
        public static int MinRam;
        public static int JVMScreenWidth = 0;
        public static int JVMScreenHeight = 0;
        public static bool FullScreen = false;
        public static List<string> JVMArgs = new List<string>();
        public static int CurrentRam = 0;
        //
        public static long SliderRamMax;
        public static long SliderRamMin;
        public static long SliderRamValue;
        //
        public static bool AssestsCheck = true;
        public static bool HashCheck = true;
        //
        public static bool IsFixedDiscord = false;
        private static MSession ss;
    }
}
