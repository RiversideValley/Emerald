using Emerald.Core;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using Windows.Security.Credentials;
namespace Emerald.WinUI.Helpers
{
    public static class WindowsHello
    {
        private static DateTime LastSucessedTime = DateTime.MaxValue;

        public static async Task<bool> IsAvailable()
            => await KeyCredentialManager.IsSupportedAsync();

        public static async Task<bool> Authenticate()
        {
            var d = new ProgressBar() { IsIndeterminate = true }.ToContentDialog(Localized.AuthWindowshello.Localize());

            if (await IsAvailable())
            {
                _ = d.ShowAsync();
                var keyCreationResult = await KeyCredentialManager.RequestCreateAsync("Riverside.Emerald", KeyCredentialCreationOption.ReplaceExisting);
                d.Hide();

                if (keyCreationResult.Status == KeyCredentialStatus.Success)
                    LastSucessedTime = DateTime.Now;

                return keyCreationResult.Status == KeyCredentialStatus.Success;
            }
            else
            {
                return false;
            }
        }

        public static bool IsRecentlyAuthenticated(int Minutes) =>
           LastSucessedTime != DateTime.MaxValue && (DateTime.Now.Date == LastSucessedTime.Date) && (LastSucessedTime.AddMinutes(Minutes) >= DateTime.Now);
    }
}
