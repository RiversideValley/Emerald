using Microsoft.UI.Xaml;
using Newtonsoft.Json;

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
