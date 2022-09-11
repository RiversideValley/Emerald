using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using SDLauncher.UWP.Enums;

namespace SDLauncher.UWP.Enums
{
    public enum AccountType
    {
        Offline,
        Microsoft
    }
}
namespace SDLauncher.UWP.Helpers
{
    public class Account : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged = delegate { };
        List<string> PicList = new List<string>();
        private string userName;
        public string UserName { get { return userName; } set { userName = value; OnPropertyChanged(); } }
        public string ProfilePicture { get; set; }
        public AccountType Type { get; set; }
        public string TypeIconGlyph { get; set; }
        public string AccessToken { get; set; }
        public string UUID { get; set; }
        public int Count { get; set; }
        public bool Last { get; set; }
        // For app UI
        private Visibility isCheckboxVsible;
        public Visibility IsCheckboxVsible { get { return isCheckboxVsible; } set { isCheckboxVsible = value; OnPropertyChanged(); } }
        private bool sChecked;
        public bool IsChecked { get { return sChecked; } set { sChecked = value; OnPropertyChanged(); } }

        public void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
        public Account(string username, AccountType type, string accesstoken, string uuid, int count, bool last)
        {
            IsCheckboxVsible = Visibility.Collapsed;
            IsChecked = false;
            UserName = username;
            Type = type;
            AccessToken = accesstoken;
            UUID = uuid;
            Count = count;
            Last = last;
            if (UUID != null)
            {
                ProfilePicture = "https://minotar.net/avatar/" + UUID;
            }
            else
            {
                ProfilePicture = "https://minotar.net/avatar/MHF_Steve" + UUID;
            }
            TypeIconGlyph = Type == AccountType.Offline ? "\xF384" : "\xEC05";
        }
    }
}
