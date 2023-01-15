using CmlLib.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Shapes;
using Newtonsoft.Json;
using System;

namespace Emerald.WinUI.Helpers.Settings.JSON
{
    public class JSON : Models.Model
    {
        public string Serialize()
            => JsonConvert.SerializeObject(this, Formatting.Indented);
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
                Path = MinecraftPath.GetOSDefaultPath(),
                RAM = DirectResoucres.MaxRAM / 2,
                MCVerionsConfiguration = new(),
                JVM = new(),
                Downloader = new()
                { 
                    AssetsCheck= true,
                    HashCheck = true
                }
            }
        };

        public string APIVersion { get; set; } = "1.2";

        public Minecraft Minecraft { get; set; }

        public App App { get; set; }
    }

    public class Minecraft : JSON
    {
        public Minecraft()
        {
            JVM.PropertyChanged += (_, _)
                => InvokePropertyChanged();
        }

        [JsonIgnore]
        public double RAMinGB => Math.Round(RAM / Math.Pow(1024, 1), 1);

        private string _Path;
        public string Path
        {
            get => _Path;
            set => Set(ref _Path, value,nameof(Path));
        }

        private int _RAM;
        public int RAM
        {
            get => _RAM;
            set => Set(ref _RAM, value);
        }

        private bool _IsAdmin;
        public bool IsAdmin
        {
            get => _IsAdmin;
            set => Set(ref _IsAdmin, value);
        }

        public Account[] Accounts { get; set; }

        public Downloader Downloader { get; set; }

        public MCVerionsConfiguration MCVerionsConfiguration { get; set; }

        public JVM JVM { get; set; } = new();

        public bool ReadLogs()
            => JVM.GameLogs && !IsAdmin;
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

        private bool _GameLogs;
        public bool GameLogs
        {
            get => _GameLogs;
            set => Set(ref _GameLogs, value, nameof(GameLogs));
        }
    }

    public class App : JSON
    {
        public Appearance Appearance { get; set; }
        public bool AutoLogin { get; set; }
        public Discord Discord { get; set; }
        public NewsFilter NewsFilter { get; set; } = new();
        public bool AutoClose { get; set; }
        public bool HideOnLaunch { get; set; }
        public bool WindowsHello { get; set; }
    }

    public class NewsFilter : JSON
    {
        private bool _Java = true;
        public bool Java
        {
            get => _Java;
            set => Set(ref _Java, value);
        }
        private bool _Bedrock = true;
        public bool Bedrock
        {
            get => _Bedrock;
            set => Set(ref _Bedrock, value);
        }
        private bool _Dungeons = true;
        public bool Dungeons
        {
            get => _Dungeons;
            set => Set(ref _Dungeons, value);
        }
        private bool _Legends = true;
        public bool Legends
        {
            get => _Legends;
            set => Set(ref _Legends, value);
        }
        public bool All
        {
            get => Java && Bedrock && Dungeons && Legends;
            set
            {
                if (value)
                    Java = Bedrock = Dungeons = Legends = true;
            }
        }
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
        private int _NavIconType = 1;
        public int NavIconType { get => _NavIconType; set => Set(ref _NavIconType, value); }

        public bool ShowFontIcons
            => NavIconType == 0;

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
