using Microsoft.Windows.ApplicationModel.Resources;

namespace Emerald.WinUI.Helpers
{
    public static class Extentions
    {
        public static string ToLocalizedString(this string resourceKey, string resw = null)
        {
            try
            {
                string s;
                if (resw == null)
                {
                    s = new ResourceLoader().GetString(resourceKey);
                }
                else
                {
                    s = new Windows.ApplicationModel.Resources. ResourceLoader(resw).GetString(resourceKey);
                }
                return string.IsNullOrEmpty(s) ? resourceKey : s;
            }
            catch
            {
                return resourceKey.ToString();
            }
        }
        public static string ToLocalizedString(this Core.Localized resourceKey, string resw = null)
        {
            try
            {
                string s;
                if (resw == null)
                {
                    s = new ResourceLoader().GetString(resourceKey.ToString());
                }
                else
                {
                    s = new ResourceLoader(resw).GetString(resourceKey.ToString());
                }
                return string.IsNullOrEmpty(s) ? resourceKey.ToString() : s;
            }
            catch
            {
                return resourceKey.ToString();
            }
        }
        public static bool IsNullEmptyOrWhiteSpace(this string str)
        {
            return string.IsNullOrEmpty(str) || string.IsNullOrWhiteSpace(str);
        }
        public static Models.Account ToAccount(this CmlLib.Core.Auth.MSession session)
        {
            bool isOffline = session.UUID == "user_uuid";
            return new Models.Account(session.Username, isOffline ? null : session.AccessToken, isOffline ? null : session.UUID, MainWindow.HomePage.AccountsPage.AllCount++, false);
        }
    }
}
