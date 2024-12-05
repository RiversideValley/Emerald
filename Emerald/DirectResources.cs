using CommunityToolkit.WinUI.Helpers;
using Emerald.Helpers;
using System.Reflection;
using System.Runtime.InteropServices;
using Windows.UI;

namespace Emerald;

public static class DirectResoucres
{
    public static int MaxRAM
        => (Extensions.GetMemoryGB() ?? 192) * 1024; //switches maximum ram if failed, I couldn't find the max ram for MC.

    public static int MinRAM
        => 512;

    //Used this thing for major setting changes because older settings could crash the program
    public static string SettingsAPIVersion
        => "1.3";

    public static string Platform
    {
        get
        {
#if WINDOWS
            return "Windows";
#elif MACCATALYST
            return "OSX";
#elif DESKTOP
            return "Skia";
#else
            return "Unknown";
#endif
        }
    }
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
        => Assembly.GetExecutingAssembly().GetName().Version.ToString();

    public static Color LayerFillColorDefaultColor
        => (Color)App.Current.Resources["LayerFillColorDefault"];
}
