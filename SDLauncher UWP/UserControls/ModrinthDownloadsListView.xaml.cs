using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using SDLauncher_UWP.Helpers;
using System.ComponentModel;
using ByteSizeLib;
using System.Collections.ObjectModel;
// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace SDLauncher_UWP.UserControls
{
    public sealed partial class ModrinthDownloadsListView : UserControl, INotifyPropertyChanged
    {
        public event TypedEventHandler<ModrinthDownloadsListView, LabrinthResults.DownloadManager.File> DownloadRequested = delegate { };
            public event PropertyChangedEventHandler PropertyChanged = delegate { };
    
        private List<LabrinthResults.DownloadManager.DownloadLink> Mainsource;
        private ObservableCollection<DownloadLink> templateSource;
        public ObservableCollection<DownloadLink> TemplateSource { get { return templateSource; } set { templateSource = value; this.PropertyChanged(this, new PropertyChangedEventArgs(nameof(TemplateSource))); } }
        public ModrinthDownloadsListView()
        {
            this.InitializeComponent();
        }

        public List<LabrinthResults.DownloadManager.DownloadLink> ItemsSource
        {
            get { return Mainsource; }
            set { 
                Mainsource = value;
                var  s = new ObservableCollection<DownloadLink>();
                foreach (var item in Mainsource)
                {
                    s.Add(new DownloadLink(item));
                }
                TemplateSource = s;
                this.PropertyChanged(this, new PropertyChangedEventArgs(null));
            }
        }

        private void btnDownload_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                var s = from DownloadLink link in templateSource where link.ID == btn.Tag.ToString() select link;
                if (s != null)
                {
                    var file = from t in s.FirstOrDefault().MainLink.files where t.primary = true select t;

                    this.DownloadRequested(this, file.FirstOrDefault());
                }
            }
        }
        private void lview_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            foreach (var item in TemplateSource)
            {
                if (lview.SelectedItem != null)
                {
                    if (item.ID == (lview.SelectedItem as DownloadLink).ID)
                    {
                        item.ShowMoreInfo = Visibility.Visible;
                    }
                    else
                    {
                        item.ShowMoreInfo = Visibility.Collapsed;
                    }
                }
            }
        }
    }
    public class DownloadLink : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged = delegate { };
        public enum versionType
        {
            Release,
            Beta,
            Alpha
        }
        public LabrinthResults.DownloadManager.DownloadLink MainLink { get; set; }
        public string VersionName => MainLink.name;
        public string FileName
        {
            get
            {
                try
                {
                    var file = from t in MainLink.files where t.primary == true select t;
                    string filename = "";
                    if (file != null)
                    {
                        filename = file.First().filename;
                    }
                    else
                    {
                        filename = "";
                    }
                    return filename;
                }
                catch
                {
                    return "";
                }
}
        }
        public string VersionNumber => MainLink.version_number;
        public string Downloads => MainLink.downloads.KiloFormat();
        public string ID => MainLink.id;
        public versionType VersionType => (versionType)Enum.Parse(typeof(versionType), MainLink.version_type, true);
        public string DisplaySize
        {
            get {
                int size = 0;
                try
                {
                    foreach (var item in MainLink.files)
                    {
                        if (item.primary)
                        {
                            size += item.size;
                        }
                    }
                }
                catch { }
                var s = ByteSize.FromBytes(size);
                return decimal.Round((decimal)s.MegaBytes,1,MidpointRounding.AwayFromZero).ToString();
                    }
        }

        private Visibility showMoreInfo;
        public Visibility ShowMoreInfo { get { return showMoreInfo; } set { showMoreInfo = value; this.PropertyChanged(this, new PropertyChangedEventArgs(null)); } }
        public DownloadLink(LabrinthResults.DownloadManager.DownloadLink mainLink)
        {
            MainLink = mainLink;
            ShowMoreInfo = Visibility.Collapsed;
        }

    }
}
