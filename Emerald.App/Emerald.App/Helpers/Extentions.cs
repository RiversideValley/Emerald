using Microsoft.Windows.ApplicationModel.Resources;
using System.Collections.ObjectModel;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Emerald.WinUI.Helpers
{
    public static class Extentions
    {
        public static int Remove<T>(
        this ObservableCollection<T> coll, Func<T, bool> condition)
        {
            var itemsToRemove = coll.Where(condition).ToList();

            foreach (var itemToRemove in itemsToRemove)
            {
                coll.Remove(itemToRemove);
            }

            return itemsToRemove.Count;
        }
        public static string ToBinaryString(this string str)
        {
            var binary = "";
            foreach (char ch in str)
            {
                binary += Convert.ToString((int)ch, 2);
            }
            return binary;
        }
        public static string ToMD5(this string s)
        {
            StringBuilder sb = new();

            using (MD5 md5 = MD5.Create())
            {
                byte[] hashValue = md5.ComputeHash(Encoding.UTF8.GetBytes(s));

                foreach (byte b in hashValue)
                {
                    sb.Append($"{b:X2}");
                }
            }

            return sb.ToString();
        }
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
