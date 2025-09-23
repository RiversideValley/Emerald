using Emerald.WinUI.Helpers.Settings.JSON;
using System.Text.Json;
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
        public static async Task<T> GetSerializedFromSettings<T>(string key, T def)
        {
            string json;
            try
            {
                if(key == "Settings")
                    json = await FileIO.ReadTextAsync(ApplicationData.Current.LocalFolder.GetFileAsync("settings.json").AsTask().Result);
                else
                    json = ApplicationData.Current.RoamingSettings.Values[key] as string;

                return JsonSerializer.Deserialize<T>(json);
            }
            catch
            {
                json = JsonSerializer.Serialize(def);

                if(key != "Settings")
                    ApplicationData.Current.RoamingSettings.Values[key] = json;

                return def;
            }
        }
        public static async Task LoadData()
        {
            
            Settings = await GetSerializedFromSettings("Settings", JSON.Settings.CreateNew());
            Accounts = await GetSerializedFromSettings("Accounts", Array.Empty<Account>());

            if (Settings.APIVersion != DirectResoucres.SettingsAPIVersion)
            {
                APINoMatch?.Invoke(null, ApplicationData.Current.RoamingSettings.Values["Settings"] as string);
                ApplicationData.Current.RoamingSettings.Values["Settings"] = JSON.Settings.CreateNew().Serialize();
                Settings = JsonSerializer.Deserialize<JSON.Settings>(ApplicationData.Current.RoamingSettings.Values["Settings"] as string);
            }
        }

        public static async Task CreateBackup(string system)
        {
            string json = await FileIO.ReadTextAsync(await ApplicationData.Current.LocalFolder.CreateFileAsync("backups.json", CreationCollisionOption.OpenIfExists));
            var l = json.IsNullEmptyOrWhiteSpace() ? new Backups() : JsonSerializer.Deserialize<Backups>(json);

            var bl = l.AllBackups == null ? new List<SettingsBackup>() : l.AllBackups.ToList();
            bl.Add(new SettingsBackup() { Time = DateTime.Now, Backup = system });
            l.AllBackups = bl.ToArray();
            json = l.Serialize();

            await FileIO.WriteTextAsync(await ApplicationData.Current.LocalFolder.CreateFileAsync("backups.json", CreationCollisionOption.OpenIfExists), json);
        }

        public static async Task DeleteBackup(int Index)
        {
            string json = await FileIO.ReadTextAsync(await ApplicationData.Current.LocalFolder.CreateFileAsync("backups.json", CreationCollisionOption.OpenIfExists));
            var l = json.IsNullEmptyOrWhiteSpace() ? new Backups() : JsonSerializer.Deserialize<Backups>(json);

            var bl = l.AllBackups == null ? new List<SettingsBackup>() : l.AllBackups.ToList();
            bl.RemoveAt(Index);
            l.AllBackups = bl.ToArray();
            json = l.Serialize();

            await FileIO.WriteTextAsync(await ApplicationData.Current.LocalFolder.CreateFileAsync("backups.json", CreationCollisionOption.OpenIfExists), json);
        }

        public static async Task DeleteBackup(DateTime time)
        {
            string json = await FileIO.ReadTextAsync(await ApplicationData.Current.LocalFolder.CreateFileAsync("backups.json", CreationCollisionOption.OpenIfExists));
            var l = json.IsNullEmptyOrWhiteSpace() ? new Backups() : JsonSerializer.Deserialize<Backups>(json);

            var bl = l.AllBackups == null ? new List<SettingsBackup>() : l.AllBackups.ToList();
            bl.Remove(x => x.Time == time);
            l.AllBackups = bl.ToArray();
            json = l.Serialize();

            await FileIO.WriteTextAsync(await ApplicationData.Current.LocalFolder.CreateFileAsync("backups.json", CreationCollisionOption.OpenIfExists), json);
        }
        public static async Task RenameBackup(DateTime time, string name)
        {
            string json = await FileIO.ReadTextAsync(await ApplicationData.Current.LocalFolder.CreateFileAsync("backups.json", CreationCollisionOption.OpenIfExists));
            var l = json.IsNullEmptyOrWhiteSpace() ? new Backups() : JsonSerializer.Deserialize<Backups>(json);

            var bl = l.AllBackups == null ? new List<SettingsBackup>() : l.AllBackups.ToList();
            bl.FirstOrDefault(x => x.Time == time).Name = name;
            l.AllBackups = bl.ToArray();
            json = l.Serialize();

            await FileIO.WriteTextAsync(await ApplicationData.Current.LocalFolder.CreateFileAsync("backups.json", CreationCollisionOption.OpenIfExists), json);
        }

        public static async Task<List<SettingsBackup>> GetBackups()
        {
            string json = await FileIO.ReadTextAsync(await ApplicationData.Current.LocalFolder.CreateFileAsync("backups.json", CreationCollisionOption.OpenIfExists));
            var l = json.IsNullEmptyOrWhiteSpace() ? new Backups() : JsonSerializer.Deserialize<Backups>(json);

            return l.AllBackups == null ? new List<SettingsBackup>() : l.AllBackups.ToList();
        }

        public static void SaveData(string _settings = null)
        {
            Settings.LastSaved = DateTime.Now;
            ApplicationData.Current.LocalFolder.CreateFileAsync("settings.json", CreationCollisionOption.OpenIfExists)
                .AsTask()
                .Wait();

            FileIO.WriteTextAsync(ApplicationData.Current.LocalFolder.GetFileAsync("settings.json").AsTask().Result,
                _settings ?? JsonSerializer.Serialize(Settings))
                .AsTask()
                .Wait();

            ApplicationData.Current.RoamingSettings.Values["Accounts"] = JsonSerializer.Serialize(Accounts);
        }
    }
}
