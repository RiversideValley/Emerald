using Microsoft.Windows.ApplicationModel.Resources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Emerald.WinUI.Helpers
{
    public static class Extentions
    {
        public static string ToLocalizedString(this string resourceKey)
        {
            try
            {
                var s = new ResourceLoader().GetString(resourceKey);
                return string.IsNullOrEmpty(s) ? resourceKey : s;
            }
            catch
            {
                return resourceKey;
            }
        }
        public static string ToLocalizedString(this Core.Localized resourceKey)
        {
            try
            {
                var s = new ResourceLoader().GetString(resourceKey.ToString());
                return string.IsNullOrEmpty(s) ? resourceKey.ToString() : s;
            }
            catch
            {
                return resourceKey.ToString();
            }
        }
        public static Models.Account ToAccount(this CmlLib.Core.Auth.MSession session)
        {
            bool isOffline = session.UUID == "user_uuid";
            return new Models.Account(session.Username, isOffline ? Enums.AccountType.Offline : Enums.AccountType.Microsoft, isOffline ? null : session.AccessToken, isOffline ? null : session.UUID, MainWindow.HomePage.AccountsPage.AllCount++, false);
        }
    }
}
