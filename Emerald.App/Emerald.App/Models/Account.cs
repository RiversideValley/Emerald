using Emerald.WinUI.Enums;
using Microsoft.UI.Xaml;

namespace Emerald.WinUI.Models
{
    public class Account : Model
    {
        private string userName;
        public string UserName { get => userName; set => Set(ref userName, value); }

        public string ProfilePicture { get => Type != AccountType.Offline ? "https://minotar.net/avatar/" + UUID : "https://minotar.net/avatar/MHF_Steve"; }
        public AccountType Type { get => UUID == null ? AccountType.Offline : AccountType.Microsoft; }
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

        public Account(string username, string accesstoken, string uuid, int count, bool last)
        {
            CheckboxVsibility = Visibility.Collapsed;
            IsChecked = false;
            UserName = username;
            AccessToken = accesstoken;
            UUID = uuid;
            Count = count;
            Last = last;
        }
    }
}