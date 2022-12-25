using CommunityToolkit.WinUI.Helpers;
using Windows.UI;

namespace Emerald.WinUI
{

    public static class DirectResoucres
    {
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
