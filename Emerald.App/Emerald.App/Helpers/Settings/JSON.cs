using Microsoft.UI.Xaml;
using Newtonsoft.Json;
using System;

namespace Emerald.WinUI.Helpers.Settings.JSON
{
    public class JSON : Models.Model
    {
        public string Serialize() => JsonConvert.SerializeObject(this, Formatting.Indented);

    }

    public class SettingsBackup : JSON
    {
        public string Backup { get; set; }
        public DateTime Time { get; set; }
    }


    public class Backups : JSON
    {
        public SettingsBackup[] AllBackups { get; set; } = Array.Empty<SettingsBackup>();
        public string APIVersion { get; private set; } = "1.0";
    }

    public class Settings : JSON
    {
        public static Settings CreateNew() => new()
        {
            App = new()
            {
                Discord = new(),
                Appearance = new()
                {
                    MicaTintColor = ((int)Enums.MicaTintColor.NoColor),
                    Theme = ((int)ElementTheme.Default)
                }
            },
            Minecraft = new()
            {
                MCVerionsConfiguration = new(),
                JVM = new(),
                Downloader = new()
            }
        };
        public string APIVersion { get; set; } = "1.1";
        public Minecraft Minecraft { get; set; }
        public App App { get; set; }
    }
    public class Minecraft : JSON
    {
        public int RAM { get; set; }
        public Account[] Accounts { get; set; }
        public Downloader Downloader { get; set; }
        public MCVerionsConfiguration MCVerionsConfiguration { get; set; }
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
    public class MCVerionsConfiguration : JSON
    {
        private bool _Release = true;
        public bool Release
        {
            get => _Release;
            set => Set(ref _Release, value);
        }
        private bool _Custom = false;
        public bool Custom
        {
            get => _Custom;
            set => Set(ref _Custom, value);
        }
        private bool _OldBeta = false;
        public bool OldBeta
        {
            get => _OldBeta;
            set => Set(ref _OldBeta, value);
        }
        private bool _OldAlpha = false;
        public bool OldAlpha
        {
            get => _OldAlpha;
            set => Set(ref _OldAlpha, value);
        }
        private bool _Snapshot = false;
        public bool Snapshot
        {
            get => _Snapshot;
            set => Set(ref _Snapshot, value);
        }
    }
    public class Appearance : JSON
    {
        private int _Theme;
        public int Theme { get => _Theme; set => Set(ref _Theme, value, nameof(Theme)); }

        private int _MicaTintColor;
        public int MicaTintColor { get => _MicaTintColor; set => Set(ref _MicaTintColor, value, nameof(MicaTintColor)); }

        private int _MicaType = 0;
        public int MicaType { get => _MicaType; set => Set(ref _MicaType, value, nameof(_MicaType)); }

        private (int A, int R, int G, int B)? _CustomMicaTintColor;
        public (int A, int R, int G, int B)? CustomMicaTintColor { get => _CustomMicaTintColor; set => Set(ref _CustomMicaTintColor, value, nameof(CustomMicaTintColor)); }

    }
}