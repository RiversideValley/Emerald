using Emerald.Core;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Emerald.WinUI.Models
{
    public class StoreItem : Model
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public string Description { get; private set; }
        public async Task<string> BigDescription()
        {
            var mod = await App.Current.Launcher.Labrinth.GetProject(ProjectID);
            return await Util.DownloadText(mod.Body_Url);
        }
        public string ProjectID { get; set; }
        public string Author { get; set; }
        public int Followers { get; set; }
        public string FollowersString { get { return Followers.KiloFormat(); } }
        public int TotalDownloads { get; set; }
        public string TotalDownloadsString { get { return TotalDownloads.KiloFormat(); } }

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
            this.Name = hit.Title;
            this.Description = hit.Description;
            this.Icon = new BitmapImage(new Uri(hit.Icon_url));
            this.TotalDownloads = hit.Downloads;
            this.SupportedVers = hit.Versions;
            this.ProjectID = hit.Project_ID;
            this.sampleImages = hit.Gallery.ToList();
            this.Author = hit.Author;
            this.Followers = hit.Follows;
        }
    }
}
