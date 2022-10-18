using Emerald.WinUI.Enums;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Emerald.WinUI.Enums
{
    public enum AccountType
    {
        Offline,
        Microsoft
    }
}
namespace Emerald.WinUI.Models
{
    public class Account : Model
    {
        private string userName;
        public string UserName { get => userName; set => Set(ref userName, value); }

        public string ProfilePicture { get => UUID != null ? "https://minotar.net/avatar/" + UUID : "https://minotar.net/avatar/MHF_Steve" + UUID; }
        public AccountType Type { get; set; }
        public string TypeIconGlyph { get => Type == AccountType.Offline ? "\xF384" : "\xEC05"; }
        public string AccessToken { get; set; }
        public string UUID { get; set; }
        public int Count { get; set; }
        public bool Last { get; set; }
        // For app UI
        private Visibility _IsCheckboxVsibility;
        public Visibility CheckboxVsibility { get => _IsCheckboxVsibility; set => Set(ref _IsCheckboxVsibility, value); }

        private bool _IsChecked;
        public bool IsChecked { get => _IsChecked; set => Set(ref _IsChecked, value); }

        public Account(string username, AccountType type, string accesstoken, string uuid, int count, bool last)
        {
            CheckboxVsibility = Visibility.Collapsed;
            IsChecked = false;
            UserName = username;
            Type = type;
            AccessToken = accesstoken;
            UUID = uuid;
            Count = count;
            Last = last;
        }
    }
}