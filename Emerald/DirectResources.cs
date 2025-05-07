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
           if( OperatingSystem.IsWindows())
            return "Windows";
            else if (OperatingSystem.IsLinux())
                return "Linux";
            else if (OperatingSystem.IsMacOS())
                return "OSX";
           else
                return "Unknown";
        }
    }
    public static string LocalDataPath
    {
        get
        {
#if WINDOWS
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Emerald");
#else
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Emerald");
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
