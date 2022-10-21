using Microsoft.UI.Xaml;
using Newtonsoft.Json;
namespace Emerald.WinUI.Helpers.Settings.JSON
{
    public class Root
    {
        public static Root CreateNew()
        {
            var s = new Root
            {
                Settings = new Settings
                {
                    App = new App
                    {
                        Discord = new Discord(),
                        Appearance = new Appearance()
                    },
                    Minecraft = new Minecraft
                    {
                        JVM = new JVM(),
                        Downloader = new Downloader()
                    }
                }
            };
            return s;
        }
        public Settings Settings { get; set; }
    }
    public class Settings
    {
        public string APIVersion { get; set; } = "1.0";
        public Minecraft Minecraft { get; set; }
        public App App { get; set; }
    }
    public class Minecraft
    {
        public int RAM { get; set; }
        public Account[] Accounts { get; set; }
        public Downloader Downloader { get; set; }
        public JVM JVM { get; set; }
    }
    public class Account
    {
        public string Type { get; set; }
        public string Username { get; set; }
        public string AccessToken { get; set; }
        public string UUID { get; set; }
        public bool LastAccessed { get; set; }
    }

    public class Downloader
    {
        public bool HashCheck { get; set; }
        public bool AssetsCheck { get; set; }
    }

    public class JVM
    {
        public string[] Arguments { get; set; }
        public int ScreenWidth { get; set; }
        public int ScreenHeight { get; set; }
        public bool FullScreen { get; set; }
        public bool GameLogs { get; set; }
    }


    public class App
    {
        public Appearance Appearance { get; set; }
        public bool Tips { get; set; }
        public bool AutoLogin { get; set; }
        public Discord Discord { get; set; }
        public bool AutoClose { get; set; }
        public bool HideOnLaunch { get; set; }
    }

    public class Discord
    {
    }

    public class Appearance
    {
        public ElementTheme Theme { get; set; }
    }
}

namespace Emerald.WinUI.Helpers.Settings
{
    public static class SettingsSystem
    {
        public static string Serialize()
        {
            return JsonConvert.SerializeObject(JSON.Root.CreateNew(), Formatting.Indented);
        }
    }
}
