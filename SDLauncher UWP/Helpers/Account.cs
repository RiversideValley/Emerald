using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;

namespace SDLauncher_UWP.Helpers
{
    public class Account : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged = delegate { };
        List<string> PicList = new List<string>();
        private string userName;
        public string UserName { get { return userName; } set { userName = value; OnPropertyChanged(); } }
        public string ProfilePicture { get; set; }
        public string Type { get; set; }
        public string TypeIconGlyph { get; set; }
        public string AccessToken { get; set; }
        public string UUID { get; set; }
        public int Count { get; set; }
        public int ProfileAvatarID { get; set; }
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
        public Account(string username, string type, string accesstoken, string uuid, int count, bool last, int? pic = null)
        {
            IsCheckboxVsible = Visibility.Collapsed;
            IsChecked = false;
            PicList.Add("https://raw.githubusercontent.com/Chaniru22/SDLauncher/main/Pictures/steve.png");
            PicList.Add("https://raw.githubusercontent.com/Chaniru22/SDLauncher/main/Pictures/NoobSteve.png");
            PicList.Add("https://raw.githubusercontent.com/Chaniru22/SDLauncher/main/Pictures/alex.png");
            if (pic == null)
            {
                Random r = new Random();
                int index = r.Next(PicList.Count);
                ProfilePicture = PicList[index];
                ProfileAvatarID = index;
            }
            else
            {
                ProfilePicture = PicList[(int)pic];
                ProfileAvatarID = (int)pic;
            }
            UserName = username;
            Type = type;
            AccessToken = accesstoken;
            UUID = uuid;
            Count = count;
            Last = last;
            if (UUID != "null")
            {
                ProfilePicture = "https://minotar.net/avatar/" + UUID;
                ProfileAvatarID = 3;
            }
            TypeIconGlyph = Type == "Offline" ? "\xF384" : "\xEC05";
        }
    }
}
