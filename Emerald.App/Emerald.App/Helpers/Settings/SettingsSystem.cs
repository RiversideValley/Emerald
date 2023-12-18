using Emerald.WinUI.Helpers.Settings.JSON;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;

namespace Emerald.WinUI.Helpers.Settings
{
    public static class SettingsSystem
    {
        public static JSON.Settings Settings { get; private set; }
        public static Account[] Accounts { get; set; }

        public static event EventHandler<string>? APINoMatch;
        public static T GetSerializedFromSettings<T>(string key, T def)
        {
            string json;
            try
            {

                json = ApplicationData.Current.RoamingSettings.Values[key] as string;
                return JsonConvert.DeserializeObject<T>(json);
            }
            catch
            {
                json = JsonConvert.SerializeObject(def);
                ApplicationData.Current.RoamingSettings.Values[key] = json;
                return def;
            }
        }
        public static void LoadData()
        {
            Settings = GetSerializedFromSettings("Settings", JSON.Settings.CreateNew());
            Accounts = GetSerializedFromSettings("Accounts", Array.Empty<Account>());

            if (Settings.APIVersion != DirectResoucres.SettingsAPIVersion)
            {
                APINoMatch?.Invoke(null, ApplicationData.Current.RoamingSettings.Values["Settings"] as string);
                ApplicationData.Current.RoamingSettings.Values["Settings"] = JSON.Settings.CreateNew().Serialize();
                Settings = JsonConvert.DeserializeObject<JSON.Settings>(ApplicationData.Current.RoamingSettings.Values["Settings"] as string);
            }
        }

        public static async Task CreateBackup(string system)
        {
            string json = await FileIO.ReadTextAsync(await ApplicationData.Current.LocalFolder.CreateFileAsync("backups.json", CreationCollisionOption.OpenIfExists));
            var l = json.IsNullEmptyOrWhiteSpace() ? new Backups() : JsonConvert.DeserializeObject<Backups>(json);

            var bl = l.AllBackups == null ? new List<SettingsBackup>() : l.AllBackups.ToList();
            bl.Add(new SettingsBackup() { Time = DateTime.Now, Backup = system });
            l.AllBackups = bl.ToArray();
            json = l.Serialize();

            await FileIO.WriteTextAsync(await ApplicationData.Current.LocalFolder.CreateFileAsync("backups.json", CreationCollisionOption.OpenIfExists), json);
        }

        public static async Task DeleteBackup(int Index)
        {
            string json = await FileIO.ReadTextAsync(await ApplicationData.Current.LocalFolder.CreateFileAsync("backups.json", CreationCollisionOption.OpenIfExists));
            var l = json.IsNullEmptyOrWhiteSpace() ? new Backups() : JsonConvert.DeserializeObject<Backups>(json);

            var bl = l.AllBackups == null ? new List<SettingsBackup>() : l.AllBackups.ToList();
            bl.RemoveAt(Index);
            l.AllBackups = bl.ToArray();
            json = l.Serialize();

            await FileIO.WriteTextAsync(await ApplicationData.Current.LocalFolder.CreateFileAsync("backups.json", CreationCollisionOption.OpenIfExists), json);
        }

        public static async Task DeleteBackup(DateTime time)
        {
            string json = await FileIO.ReadTextAsync(await ApplicationData.Current.LocalFolder.CreateFileAsync("backups.json", CreationCollisionOption.OpenIfExists));
            var l = json.IsNullEmptyOrWhiteSpace() ? new Backups() : JsonConvert.DeserializeObject<Backups>(json);

            var bl = l.AllBackups == null ? new List<SettingsBackup>() : l.AllBackups.ToList();
            bl.Remove(x => x.Time == time);
            l.AllBackups = bl.ToArray();
            json = l.Serialize();

            await FileIO.WriteTextAsync(await ApplicationData.Current.LocalFolder.CreateFileAsync("backups.json", CreationCollisionOption.OpenIfExists), json);
        }
        public static async Task RenameBackup(DateTime time, string name)
        {
            string json = await FileIO.ReadTextAsync(await ApplicationData.Current.LocalFolder.CreateFileAsync("backups.json", CreationCollisionOption.OpenIfExists));
            var l = json.IsNullEmptyOrWhiteSpace() ? new Backups() : JsonConvert.DeserializeObject<Backups>(json);

            var bl = l.AllBackups == null ? new List<SettingsBackup>() : l.AllBackups.ToList();
            bl.FirstOrDefault(x => x.Time == time).Name = name;
            l.AllBackups = bl.ToArray();
            json = l.Serialize();

            await FileIO.WriteTextAsync(await ApplicationData.Current.LocalFolder.CreateFileAsync("backups.json", CreationCollisionOption.OpenIfExists), json);
        }

        public static async Task<List<SettingsBackup>> GetBackups()
        {
            string json = await FileIO.ReadTextAsync(await ApplicationData.Current.LocalFolder.CreateFileAsync("backups.json", CreationCollisionOption.OpenIfExists));
            var l = json.IsNullEmptyOrWhiteSpace() ? new Backups() : JsonConvert.DeserializeObject<Backups>(json);

            return l.AllBackups == null ? new List<SettingsBackup>() : l.AllBackups.ToList();
        }

        public static void SaveData()
        {
            Settings.LastSaved = DateTime.Now;
            ApplicationData.Current.RoamingSettings.Values["Settings"] = Settings.Serialize();
            ApplicationData.Current.RoamingSettings.Values["Accounts"] = JsonConvert.SerializeObject(Accounts);
        }
    }
}