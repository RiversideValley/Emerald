using Microsoft.UI.Xaml;
using Newtonsoft.Json;
using System;
using Windows.Storage;
using System.Threading.Tasks;

namespace Emerald.WinUI.Helpers.Settings
{
    public static class SettingsSystem
    {
        public static JSON.Settings Settings { get; private set; }
        public static async Task LoadData()
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
        }
        public static void SaveData()
        {
            var json = Settings.Serialize();
            ApplicationData.Current.RoamingSettings.Values["Settings"] = json;
        }
    }
}
