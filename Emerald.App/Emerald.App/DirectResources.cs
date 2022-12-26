using CommunityToolkit.WinUI.Helpers;
using Windows.UI;
using Emerald.WinUI.Helpers;
namespace Emerald.WinUI
{

    public static class DirectResoucres
    {
        public static int MaxRAM => Extentions.GetMemoryGB() * 1024;
        public static int MinRAM => 512;
        public static string BuildType
        {
            get
            {
#if DEBUG
                return "DEBUG";
#else
                return "RELEASE";
#endif
            }
        }
        public static string AppVersion => $"{SystemInformation.Instance.ApplicationVersion.Major}.{SystemInformation.Instance.ApplicationVersion.Minor}.{SystemInformation.Instance.ApplicationVersion.Build}.{SystemInformation.Instance.ApplicationVersion.Revision}";
        public static Color LayerFillColorDefaultColor => (Color)App.Current.Resources["LayerFillColorDefault"];
    }
}
