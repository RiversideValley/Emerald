using Microsoft.UI.Xaml;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Emerald.WinUI.Helpers.Settings.JSON
{
    public class JSON : Models.Model
    {
        public string Serialize() => JsonConvert.SerializeObject(this, Formatting.Indented);
        
    }
    public class Settings : JSON
    {
        public static Settings CreateNew()
        {
            var s = new Settings()
            {
                App = new App
                {
                    Discord = new Discord(),
                    Appearance = new Appearance()
                    {
                        MicaTintColor = ((int)Enums.MicaTintColor.NoColor),
                        Theme = ((int)ElementTheme.Default)
                    }
                },
                Minecraft = new Minecraft
                {
                    JVM = new JVM(),
                    Downloader = new Downloader()
                }
            };
            return s;
        }
        public string APIVersion { get; set; } = "1.0";
        public Minecraft Minecraft { get; set; }
        public App App { get; set; }
    }
    public class Minecraft : JSON
    {
        public int RAM { get; set; }
        public Account[] Accounts { get; set; }
        public Downloader Downloader { get; set; }
        public JVM JVM { get; set; }
    }
    public class Account : JSON
    {
        public string Type { get; set; }
        public string Username { get; set; }
        public string AccessToken { get; set; }
        public string UUID { get; set; }
        public bool LastAccessed { get; set; }
    }

    public class Downloader : JSON
    {
        public bool HashCheck { get; set; }
        public bool AssetsCheck { get; set; }
    }

    public class JVM : JSON
    {
        public string[] Arguments { get; set; }
        public int ScreenWidth { get; set; }
        public int ScreenHeight { get; set; }
        public bool FullScreen { get; set; }
        public bool GameLogs { get; set; }
    }

    public class App : JSON
    {
        public Appearance Appearance { get; set; }
        public bool Tips { get; set; }
        public bool AutoLogin { get; set; }
        public Discord Discord { get; set; }
        public bool AutoClose { get; set; }
        public bool HideOnLaunch { get; set; }
    }

    public class Discord : JSON
    {
    }

    public class Appearance : JSON
    {
        private int _Theme;
        public int Theme { get => _Theme; set => Set(ref _Theme, value,nameof(Theme)); }

        private int _MicaTintColor;
        public int MicaTintColor { get => _MicaTintColor; set => Set(ref _MicaTintColor, value, nameof(MicaTintColor)); }

        private int _MicaType = 0;
        public int MicaType { get => _MicaType; set => Set(ref _MicaType, value, nameof(_MicaType)); }

        private (int A, int R, int G, int B)? _CustomMicaTintColor;
        public (int A, int R, int G, int B)? CustomMicaTintColor { get => _CustomMicaTintColor; set => Set(ref _CustomMicaTintColor, value, nameof(CustomMicaTintColor)); }

    }
}