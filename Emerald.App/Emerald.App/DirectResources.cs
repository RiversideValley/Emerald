using CommunityToolkit.WinUI.Helpers;
using Emerald.WinUI.Helpers;
using System.Runtime.InteropServices;
using Windows.UI;

namespace Emerald.WinUI;

public static class DirectResoucres
{
    public static int MaxRAM
        => Extensions.GetMemoryGB() * 1024;

    public static int MinRAM
        => 512;
    //Used this thing for major setting changes because older settings could crash the program
    public static string SettingsAPIVersion
        => "1.3";

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
    public static Architecture Architecture => RuntimeInformation.ProcessArchitecture;

    public static string AppVersion
        => $"{SystemInformation.Instance.ApplicationVersion.Major}.{SystemInformation.Instance.ApplicationVersion.Minor}.{SystemInformation.Instance.ApplicationVersion.Build}.{SystemInformation.Instance.ApplicationVersion.Revision}";

    public static Color LayerFillColorDefaultColor
        => (Color)App.Current.Resources["LayerFillColorDefault"];
}