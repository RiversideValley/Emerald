using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Xml.Dom;
using Windows.Storage;
using Windows.Foundation;
using System.Xml;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using CmlLib.Core.Auth;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.ComponentModel;
using SDLauncher.UWP.Helpers;

namespace SDLauncher.UWP.Helpers
{
    public static class SettingsManager
    {
        public static Rootobject SettingsData = Rootobject.CreateNew();
        public static async Task SaveSettings(StorageFile file = null)
        {
            StorageFile final;
            if (file != null)
            {
                final = file;
            }
            else
            {
                final = await ApplicationData.Current.RoamingFolder.CreateFileAsync("settings.json", CreationCollisionOption.ReplaceExisting);
            }
            await FileIO.WriteTextAsync(final, await SerializeSettings());
        }
        public static async Task<string> SerializeSettings()
        {
            SettingsData = Rootobject.CreateNew();
            SettingsData.Settings.App.Appearance.Theme = ((ElementTheme)vars.Theme).ToString();
            SettingsData.Settings.App.Appearance.UseCustomBackgroundImage = vars.CustomBackground;
            SettingsData.Settings.App.Appearance.CustomBackgroundImagePath = vars.BackgroundImagePath;
            SettingsData.Settings.App.AutoLogin = vars.autoLog;
            SettingsData.Settings.App.VersionsSeletor.Style = vars.VerSelectors.ToString();
            SettingsData.Settings.App.AutoClose = vars.AutoClose;
            SettingsData.Settings.App.Tips = vars.ShowTips;
            SettingsData.Settings.App.Discord.IsPinned = vars.IsFixedDiscord;
            //
            SettingsData.Settings.Minecraft.RAM = vars.CurrentRam;
            SettingsData.Settings.Minecraft.GlacierClient.Exists = await vars.GlacierExists();
            SettingsData.Settings.Minecraft.GlacierClient.Version = vars.GlacierClientVersion;
            SettingsData.Settings.Minecraft.Downloader.AssetsCheck = vars.AssestsCheck;
            SettingsData.Settings.Minecraft.Downloader.HashCheck = vars.HashCheck;
            SettingsData.Settings.Minecraft.JVM.FullScreen = vars.FullScreen;
            SettingsData.Settings.Minecraft.JVM.ScreenWidth = vars.JVMScreenWidth;
            SettingsData.Settings.Minecraft.JVM.ScreenHeight = vars.JVMScreenHeight;
            SettingsData.Settings.Minecraft.JVM.GameLogs = vars.GameLogs;
            SettingsData.Settings.Minecraft.JVM.Arguments = vars.JVMArgs.ToArray();
            var accs = new List<Account>();
            foreach (var item in vars.Accounts)
            {
                accs.Add(new Account { AccessToken = item.AccessToken, UUID = item.UUID, Type = item.Type, Username = item.UserName, LastAccessed = item.Last });
            }
            SettingsData.Settings.Minecraft.Accounts = accs.ToArray();
            return Newtonsoft.Json.JsonConvert.SerializeObject(SettingsData, Newtonsoft.Json.Formatting.Indented);
        }
        public static async Task<bool> LoadSettings()
        {
            try
            {
                var storagefile = await ApplicationData.Current.RoamingFolder.GetFileAsync("settings.json");
                var text = await FileIO.ReadTextAsync(storagefile);
                DeserializeSettings(text);
                return true;
            }
            catch
            {
                return false;
            }
        }
        public static void DeserializeSettings(string text)
        {
            SettingsData = Newtonsoft.Json.JsonConvert.DeserializeObject<Rootobject>(text);

            //
            vars.Theme = (ElementTheme)Enum.Parse(typeof(ElementTheme), SettingsData.Settings.App.Appearance.Theme);
            vars.CustomBackground = SettingsData.Settings.App.Appearance.UseCustomBackgroundImage;
            vars.BackgroundImagePath = SettingsData.Settings.App.Appearance.CustomBackgroundImagePath;
            vars.autoLog = SettingsData.Settings.App.AutoLogin;
            vars.VerSelectors = (Views.VerSelectors)Enum.Parse(typeof(Views.VerSelectors), SettingsData.Settings.App.VersionsSeletor.Style);
            vars.AutoClose = SettingsData.Settings.App.AutoClose;
            vars.ShowTips = SettingsData.Settings.App.Tips;
            vars.IsFixedDiscord = SettingsData.Settings.App.Discord.IsPinned;
            //
            vars.GlacierClientVersion = SettingsData.Settings.Minecraft.GlacierClient.Version;
            vars.CurrentRam = SettingsData.Settings.Minecraft.RAM;
            vars.AssestsCheck = SettingsData.Settings.Minecraft.Downloader.AssetsCheck;
            vars.HashCheck = SettingsData.Settings.Minecraft.Downloader.HashCheck;
            vars.FullScreen = SettingsData.Settings.Minecraft.JVM.FullScreen;
            vars.JVMScreenWidth = SettingsData.Settings.Minecraft.JVM.ScreenWidth;
            vars.JVMScreenHeight = SettingsData.Settings.Minecraft.JVM.ScreenHeight;
            vars.GameLogs = SettingsData.Settings.Minecraft.JVM.GameLogs;
            if (SettingsData.Settings.Minecraft.JVM.Arguments != null) { vars.JVMArgs = SettingsData.Settings.Minecraft.JVM.Arguments.ToList(); }
            var accs = new ObservableCollection<Helpers.Account>();
            foreach (var item in SettingsData.Settings.Minecraft.Accounts)
            {
                accs.Add(new Helpers.Account(item.Username, item.Type, item.AccessToken, item.UUID, accs.Count + 1, item.LastAccessed));
            }
            vars.Accounts = accs;
            vars.AccountsCount = accs.Count;
        }
        public class Rootobject
        {
            public static Rootobject CreateNew()
            {
                var s = new Rootobject();
                s.Settings = new Settings();
                s.Settings.App = new App();
                s.Settings.App.Discord = new Discord();
                s.Settings.App.Appearance = new Appearance();
                s.Settings.App.VersionsSeletor = new Versionsseletor();
                s.Settings.Minecraft = new Minecraft();
                s.Settings.Minecraft.JVM = new JVM();
                s.Settings.Minecraft.GlacierClient = new GlacierClient();
                s.Settings.Minecraft.Downloader = new Downloader();
                return s;
            }
            public Settings Settings { get; set; }
        }

        public class Settings
        {
            public Minecraft Minecraft { get; set; }
            public App App { get; set; }
        }

        public class Minecraft
        {
            public int RAM { get; set; }
            public Account[] Accounts { get; set; }
            public Downloader Downloader { get; set; }
            public GlacierClient GlacierClient { get; set; }
            public JVM JVM { get; set; }
        }
        public class GlacierClient
        {
            public string Version { get; set; }
            public bool Exists { get; set; }
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
            public Versionsseletor VersionsSeletor { get; set; }
            public Discord Discord { get; set; }
            public bool AutoClose { get; set; }
        }

        public class Appearance
        {
            public string CustomBackgroundImagePath { get; set; }
            public bool UseCustomBackgroundImage { get; set; }
            public string Theme { get; set; }
        }

        public class Versionsseletor
        {
            public string Style { get; set; }
        }

        public class Discord
        {
            public bool IsPinned { get; set; }
        }


    }
}