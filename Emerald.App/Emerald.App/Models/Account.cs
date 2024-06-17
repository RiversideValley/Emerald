using CommunityToolkit.Mvvm.ComponentModel;
using Emerald.Core;
using Emerald.WinUI.Enums;
using Emerald.WinUI.Helpers;

namespace Emerald.WinUI.Models
{
    public partial class Account : Model
    {

        [ObservableProperty]
        private string _UserName;

        public string AccessToken { get; set; }

        public string UUID { get; set; }

        public string ClientToken { get; set; }

        public int Count { get; set; }

        public bool Last { get; set; }

        // For app UI
        public string TypeIconGlyph { get => IsOffline ? "\xF384" : "\xEC05"; }

        public string ProfilePicture { get => !IsOffline ? "https://minotar.net/avatar/" + UUID : "https://minotar.net/avatar/MHF_Steve"; }

        public string BodyPicture { get => !IsOffline ? "https://minotar.net/body/" + UUID : "https://minotar.net/body/MHF_Steve"; }

        public string Skin { get => !IsOffline ? "https://minotar.net/skin/" + UUID : "https://minotar.net/skin/MHF_Steve"; }

        public AccountType Type { get => IsOffline ? AccountType.Offline : AccountType.Microsoft; }

        [ObservableProperty]
        private bool _CheckBoxLoaded;

        public string TypeString
            => IsOffline ? Localized.OfflineAccount.Localize() : Localized.MicrosoftAccount.Localize();

        [ObservableProperty]
        private bool _IsChecked;

        public bool IsOffline => string.IsNullOrWhiteSpace(AccessToken);


        public Account(string username, string accesstoken, string uuid, int count, bool last, string clientToken = null)
        {
            CheckBoxLoaded = false;
            IsChecked = false;
            UserName = username;
            AccessToken = accesstoken;
            UUID = uuid;
            Count = count;
            Last = last;
            ClientToken = clientToken;
        }
    }
}
