using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Windows.System.Diagnostics;

namespace Emerald.WinUI.Helpers
{
    public static class Extensions
    {
        private static readonly ConcurrentDictionary<string, string> cachedResources = new();

        public static void ShowAt(this TeachingTip tip, FrameworkElement element, TeachingTipPlacementMode placement = TeachingTipPlacementMode.Auto, bool closeWhenClick = true, bool addToMainGrid = true)
        {
            if (addToMainGrid)
                (App.Current.MainWindow.Content as Grid).Children.Add(tip);

            tip.Target = element;
            tip.PreferredPlacement = placement;
            tip.IsOpen = true;
            if (closeWhenClick)
            {
                tip.ActionButtonClick += (_, _) => tip.IsOpen = false;
                tip.CloseButtonClick += (_, _) => tip.IsOpen = false;
            }
        }

        public static int GetMemoryGB()
        {
            SystemMemoryUsageReport systemMemoryUsageReport = SystemDiagnosticInfo.GetForCurrentSystem().MemoryUsage.GetReport();

            long memkb = Convert.ToInt64(systemMemoryUsageReport.TotalPhysicalSizeInBytes);
            return Convert.ToInt32(memkb / Math.Pow(1024, 3));
        }

        public static string KiloFormat(this int num)
        {
            if (num >= 100000000)
                return (num / 1000000).ToString("#,0M");

            if (num >= 10000000)
                return (num / 1000000).ToString("0.#") + "M";

            if (num >= 100000)
                return (num / 1000).ToString("#,0K");

            if (num >= 1000)
                return (num / 100).ToString("0.#") + "K";

            return num.ToString("#,0");
        }

        public static ContentDialog ToContentDialog(this UIElement content, string title, string closebtnText = null, ContentDialogButton defaultButton = ContentDialogButton.Close)
        {
            ContentDialog dialog = new()
            {
                XamlRoot = App.Current.MainWindow.Content.XamlRoot,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
                Title = title,
                CloseButtonText = closebtnText,
                DefaultButton = defaultButton,
                Content = content,
                RequestedTheme = (ElementTheme)Settings.SettingsSystem.Settings.App.Appearance.Theme
            };
            return dialog;
        }

        public static int Remove<T>(this ObservableCollection<T> coll, Func<T, bool> condition)
        {
            var itemsToRemove = coll.Where(condition).ToList();

            foreach (var itemToRemove in itemsToRemove)
            {
                coll.Remove(itemToRemove);
            }
            return itemsToRemove.Count;
        }

        public static int Remove<T>(this List<T> coll, Func<T, bool> condition)
        {
            var itemsToRemove = coll.Where(condition).ToList();

            foreach (var itemToRemove in itemsToRemove)
            {
                coll.Remove(itemToRemove);
            }

            return itemsToRemove.Count;
        }
        public static void AddRange<T>(this ObservableCollection<T> cll, IEnumerable<T> items)
        {
            foreach (var item in items)
                cll.Add(item);
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

        public static string Localize(this string resourceKey)
        {
            try
            {
                ResourceManager rl;
                string s;

                if (cachedResources.TryGetValue(resourceKey, out var value))
                    return value;

                rl = new ResourceManager();

                ResourceMap resourcesTree = rl.MainResourceMap.TryGetSubtree("Resources");
                value = resourcesTree?.TryGetValue(resourceKey)?.ValueAsString;
                cachedResources[resourceKey] = value ?? resourceKey;
                s = value;

                return string.IsNullOrEmpty(s) ? resourceKey : s;
            }
            catch
            {
                return resourceKey;
            }
        }

        public static string Localize(this Core.Localized resourceKey) =>
             resourceKey.ToString().Localize();

        public static bool IsNullEmptyOrWhiteSpace(this string str) =>
            string.IsNullOrEmpty(str) || string.IsNullOrWhiteSpace(str);


        public static Models.Account ToAccount(this CmlLib.Core.Auth.MSession session, bool plusCount = true)
        {
            bool isOffline = session.AccessToken == "access_token";
            return new Models.Account(session.Username, isOffline ? null : session.AccessToken, isOffline ? null : session.UUID, plusCount ? MainWindow.HomePage.AccountsPage.AllCount++ : 0, false, session.ClientToken);
        }

        public static CmlLib.Core.Auth.MSession ToMSession(this Models.Account account)
        {
            bool isOffline = account.UUID == null;
            return new CmlLib.Core.Auth.MSession(account.UserName, isOffline ? "access_token" : account.AccessToken, isOffline ? Guid.NewGuid().ToString().Replace("-", "") : account.UUID) { ClientToken  = account.ClientToken };
        }
    }
}
