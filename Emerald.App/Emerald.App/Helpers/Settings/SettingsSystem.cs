using Emerald.WinUI.Helpers.Settings.JSON;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Storage;

namespace Emerald.WinUI.Helpers.Settings
{
#pragma warning disable CS8601
    public static class SettingsSystem
    {
        public static JSON.Settings Settings { get; private set; }
        public static event EventHandler<string>? APINoMatch;
        public static void LoadData()
        {
            string json;
            try
            {
                json = ApplicationData.Current.RoamingSettings.Values["Settings"] as string;
            }
            catch
            {
                json = JSON.Settings.CreateNew().Serialize();
                ApplicationData.Current.RoamingSettings.Values["Settings"] = json;
            }
            if (json.IsNullEmptyOrWhiteSpace())
            {
                json = JSON.Settings.CreateNew().Serialize();
                ApplicationData.Current.RoamingSettings.Values["Settings"] = json;
            }
            Settings = JsonConvert.DeserializeObject<JSON.Settings>(json);
            if (Settings.APIVersion != "1.2")
            {
                APINoMatch?.Invoke(null, json);
                json = JSON.Settings.CreateNew().Serialize();
                ApplicationData.Current.RoamingSettings.Values["Settings"] = json;
                Settings = JsonConvert.DeserializeObject<JSON.Settings>(json);
            }
        }
        public static bool CreateBackup(string system)
        {
            string json;
            var l = new Backups();
            try
            {
                json = ApplicationData.Current.LocalSettings.Values["SettingsBackups"] as string;
            }
            catch
            {
                json = JsonConvert.SerializeObject(new Backups());
                ApplicationData.Current.LocalSettings.Values["SettingsBackups"] = json;
            }
            if (json.IsNullEmptyOrWhiteSpace())
            {
                json = JsonConvert.SerializeObject(new Backups());
                ApplicationData.Current.LocalSettings.Values["SettingsBackups"] = json;
            }
            l = JsonConvert.DeserializeObject<Backups>(json);
            var bl = l.AllBackups == null ? new List<SettingsBackup>() : l.AllBackups.ToList();
            bl.Add(new SettingsBackup() { Time = DateTime.Now, Backup = system });
            l.AllBackups = bl.ToArray();
            json = JsonConvert.SerializeObject(l);
            try
            {
                ApplicationData.Current.LocalSettings.Values["SettingsBackups"] = json;
                return true;
            }
            catch
            {
                return false;
            }
        }
        public static bool DeleteBackup(int Index)
        {
            string json;
            var l = new Backups();
            try
            {
                json = ApplicationData.Current.LocalSettings.Values["SettingsBackups"] as string;
            }
            catch
            {
                json = JsonConvert.SerializeObject(new Backups());
                ApplicationData.Current.LocalSettings.Values["SettingsBackups"] = json;
            }
            if (json.IsNullEmptyOrWhiteSpace())
            {
                json = JsonConvert.SerializeObject(new Backups());
                ApplicationData.Current.LocalSettings.Values["SettingsBackups"] = json;
            }
            l = JsonConvert.DeserializeObject<Backups>(json);
            try
            {
                var bl = l.AllBackups == null ? new List<SettingsBackup>() : l.AllBackups.ToList();
                bl.RemoveAt(Index);
                l.AllBackups = bl.ToArray();
                json = JsonConvert.SerializeObject(l);
                ApplicationData.Current.LocalSettings.Values["SettingsBackups"] = json;
                return true;
            }
            catch
            {
                return false;
            }
        }
        public static bool DeleteBackup(DateTime time)
        {
            string json;
            var l = new Backups();
            try
            {
                json = ApplicationData.Current.LocalSettings.Values["SettingsBackups"] as string;
            }
            catch
            {
                json = JsonConvert.SerializeObject(new Backups());
                ApplicationData.Current.LocalSettings.Values["SettingsBackups"] = json;
            }
            if (json.IsNullEmptyOrWhiteSpace())
            {
                json = JsonConvert.SerializeObject(new Backups());
                ApplicationData.Current.LocalSettings.Values["SettingsBackups"] = json;
            }
            l = JsonConvert.DeserializeObject<Backups>(json);
            try
            {
                var bl = l.AllBackups == null ? new List<SettingsBackup>() : l.AllBackups.ToList();
                bl.Remove(x=> x.Time == time);
                l.AllBackups = bl.ToArray();
                json = JsonConvert.SerializeObject(l);
                ApplicationData.Current.LocalSettings.Values["SettingsBackups"] = json;
                return true;
            }
            catch
            {
                return false;
            }
        }
        public static List<SettingsBackup> GetBackups()
        {
            string json;
            try
            {
                json = ApplicationData.Current.LocalSettings.Values["SettingsBackups"] as string;
            }
            catch
            {
                json = JsonConvert.SerializeObject(new Backups());
                ApplicationData.Current.LocalSettings.Values["SettingsBackups"] = json;
            }
            if (json.IsNullEmptyOrWhiteSpace())
            {
                json = JsonConvert.SerializeObject(new Backups());
                ApplicationData.Current.LocalSettings.Values["SettingsBackups"] = json;
            }
            var l = JsonConvert.DeserializeObject<Backups>(json);
            return l.AllBackups == null ? new List<SettingsBackup>() : l.AllBackups.ToList();
        }
        public static void SaveData()
        {
            var json = Settings.Serialize();
            ApplicationData.Current.RoamingSettings.Values["Settings"] = json;
        }
    }
}