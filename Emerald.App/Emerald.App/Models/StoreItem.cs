using Emerald.Core;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Version = Emerald.Core.Store.Results.Version;

namespace Emerald.WinUI.Models
{
    public class StoreItem : Model
    {
        public string Name { get; set; }
        public string Description { get; private set; }
        public async Task<string> BigDescriptionAsync()
        {
            var mod = await App.Current.Launcher.Labrinth.GetProject(ProjectID, Name);
            return mod?.Body;
        }
        public string ProjectID { get; set; }
        public string Author { get; set; }
        public int Followers { get; set; }
        public string FollowersString { get { return Followers.KiloFormat(); } }
        public int TotalDownloads { get; set; }
        public string TotalDownloadsString { get { return TotalDownloads.KiloFormat(); } }
        public async Task<List<Version>> GetDownloadVersionsAsync()
            => await App.Current.Launcher.Labrinth.GetVersions(ProjectID, Name);

        public string[] SupportedVers { get; set; }



        private List<string> sampleImages = new List<string>();
        public List<BitmapImage> SampleImages
        {
            get
            {
                var b = new List<BitmapImage>();
                foreach (var item in sampleImages)
                {
                    b.Add(new BitmapImage(new Uri(item)));
                }
                return b;
            }
        }
        public BitmapImage Icon;

        public StoreItem(Core.Store.Results.Hit hit)
        {
            Name = hit.Title;
            Description = hit.Description;
            Icon = new BitmapImage(new Uri(hit.Icon_url));
            TotalDownloads = hit.Downloads;
            SupportedVers = hit.Versions;
            ProjectID = hit.Project_ID;
            sampleImages = hit.Gallery.ToList();
            Author = hit.Author;
            Followers = hit.Follows;
        }
    }
}
