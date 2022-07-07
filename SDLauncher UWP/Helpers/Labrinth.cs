using SDLauncher_UWP.Resources;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Windows.Foundation;
using Windows.Networking.BackgroundTransfer;
using Windows.Storage;
using Windows.UI.Xaml;

namespace SDLauncher_UWP.Helpers
{
    public static class LittleHelp
    {
        public static int AddTask(string name)
        {
            return vars.Launcher.TasksHelper.AddTask(name);
        }
        public static void CompleteTask(int ID,bool IsSuccess = true)
        {
            vars.Launcher.TasksHelper.CompleteTask(ID,IsSuccess);
        }
    }
    public class Labrinth
    {
        public event EventHandler StatusChanged = delegate { };
        public event EventHandler<SDLauncher.UIChangeRequestedEventArgs> UIChangeRequested = delegate { };
        public event EventHandler<SDLauncher.UIChangeRequestedEventArgs> MainUIChangeRequested = delegate { };
        public event EventHandler<SDLauncher.ProgressChangedEventArgs> ProgressChanged = delegate { };
        public HttpClient Client = new HttpClient();
        //ModrinthClient c = new ModrinthClient();
        public void Test()
        {
            Client.BaseAddress = new Uri("");
            Client.DefaultRequestHeaders.Accept.Clear();
            Client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        }
        private void UI(bool UI)
        {
            UIChangeRequested(this, new SDLauncher.UIChangeRequestedEventArgs(UI));
        }
        int DownloadTaskID;
        public async void DownloadMod(LabrinthResults.DownloadManager.File file,CmlLib.Core.MinecraftPath mcPath)
        {
            DownloadTaskID = LittleHelp.AddTask("Download " + file.filename);
            MainUIChangeRequested(this, new SDLauncher.UIChangeRequestedEventArgs(false));
                StorageFolder f = await StorageFolder.GetFolderFromPathAsync(mcPath.BasePath);
               var m =  await f.CreateFolderAsync("mods", CreationCollisionOption.OpenIfExists);
            try
            {
                var mfile = await m.CreateFileAsync(file.filename, CreationCollisionOption.FailIfExists);
                await mfile.DeleteAsync();
                ModrinthDownload(file.url, mcPath.BasePath + @"\mods\", file.filename);
            }
            catch
            {
                var r = await MessageBox.Show("Information", "The file \"" + file.filename + "\" already exists in the mod folder.\nDo you want to download and replace it ?", MessageBoxButtons.YesNo);
                if (r == MessageBoxResults.Yes)
                {
                    ModrinthDownload(file.url, m.Path, file.filename);
                }
                else
                {
                    this.MainUIChangeRequested(this, new SDLauncher.UIChangeRequestedEventArgs(true));
                    LittleHelp.CompleteTask(DownloadTaskID,true);
                }
            }
        }
        private async void ModrinthDownload(string link,string folderdir, string fileName)
        {
            try
            {
                Uri source = new Uri(link);

                var folder = await StorageFolder.GetFolderFromPathAsync(folderdir);
                 var file = await folder.CreateFileAsync(
                    fileName,
                    CreationCollisionOption.ReplaceExisting);
                string path = file.Path;
                file = null;
                try
                {
                    using (var client = new HttpClientDownloadWithProgress(link, path))
                    {
                        client.ProgressChanged += (totalFileSize, totalBytesDownloaded, progressPercentage) =>
                        {
                            StatusChanged("Downloading : " + fileName, new EventArgs());
                            this.ProgressChanged(this, new SDLauncher.ProgressChangedEventArgs(currentProg: Convert.ToInt32(progressPercentage), maxfiles: 100, currentfile: Convert.ToInt32(progressPercentage)));
                            if (progressPercentage == 100)
                            {
                                this.DownloadFileCompleted();
                                client.Dispose();
                                LittleHelp.CompleteTask(DownloadTaskID);
                            }
                        };

                        await client.StartDownload();
                    }
                }
                catch
                {
                    DownloadFileCompleted();
                    LittleHelp.CompleteTask(DownloadTaskID,false);
                }

            }
            catch
            {

            }
        }
        private void DownloadFileCompleted()
        {
            StatusChanged(Localized.Ready, new EventArgs());
            ProgressChanged(this, new SDLauncher.ProgressChangedEventArgs(currentProg:0,currentfile:0));
            MainUIChangeRequested(this, new SDLauncher.UIChangeRequestedEventArgs(true));
        }
        public async Task<LabrinthResults.SearchResult> Search(string name, int? limit = null, LabrinthResults.SearchSortOptions sortOptions = LabrinthResults.SearchSortOptions.Relevance, LabrinthResults.SearchCategories[] categories = null)
        {
            int taskID = 0;
            UI(false);
            if (name == "")
            {
                StatusChanged("Getting Mods",new EventArgs());
               taskID = LittleHelp.AddTask("Get Mods");
            }
            else
            {
               taskID = LittleHelp.AddTask("Search Store");
               StatusChanged("Searching Store", new EventArgs());
            }
            string categouriesString = "";
            if(categories != null)
            {
                if (categories.Count() > 0)
                {
                    categouriesString = string.Join("\"],[\"categories:", categories);
                    categouriesString = ",[\"categories:" + categouriesString + "\"]";
                    categouriesString = categouriesString.ToLower();
                }
            }
            LabrinthResults.SearchResult s = null;
            try
            {
                var q = HttpUtility.UrlEncode(name);
                string l = "";
                if (limit != null)
                {
                    l = "limit=" + limit.ToString();
                }
                else
                {
                    l = "limit=";
                }
                var json = await Util.DownloadText("https://api.modrinth.com/v2/search?query=" + q + "&index=" + sortOptions.ToString().ToLower() + "&facets=[[\"categories:fabric\"]" + categouriesString + ",[\"project_type:mod\"]]&" + l);
                s = JSONConverter.ConvertToLabrinthSearchResult(json);
                StatusChanged(Localized.Ready, new EventArgs());
                UI(true);
                LittleHelp.CompleteTask(taskID);
                return s;
            }
            catch
            {
                StatusChanged(Localized.Ready, new EventArgs());
                UI(true);
                LittleHelp.CompleteTask(taskID, false);
                return null;
            }
        }
        public async Task<LabrinthResults.ModrinthProject> GetProject(string id,bool UIChange = true)
        {
            int taskID = LittleHelp.AddTask("Load Mod");
            if (UIChange)
            {
                UI(false);
            }
            StatusChanged("Loading Mod",new EventArgs());
            LabrinthResults.ModrinthProject s = null;
            try
            {
                var json = await Util.DownloadText("https://api.modrinth.com/v2/project/" + id);
                s = JSONConverter.ConvertToLabrinthProject(json);
                StatusChanged(Localized.Ready, new EventArgs());
                if (UIChange)
                {
                    UI(true);
                }
                LittleHelp.CompleteTask(taskID);
                return s;
            }
            catch
            {
                StatusChanged(Localized.Ready, new EventArgs());
                if (UIChange)
                {
                    UI(true);
                }
                LittleHelp.CompleteTask(taskID,false);
                return null;
            }
        }
        public async Task<List<LabrinthResults.DownloadManager.DownloadLink>> GetVersions(string id)
        {
            int taskID = LittleHelp.AddTask("Load Download versionss");
            UI(false);
            StatusChanged("Loading Mod downloads", new EventArgs());
            List<LabrinthResults.DownloadManager.DownloadLink> s = null;
            try
            {
                string link = "https://api.modrinth.com/v2/project/" + id + "/version";
                var json = await Util.DownloadText(link);
                s = JSONConverter.ConvertDownloadLinksToCS(json);
                StatusChanged(Localized.Ready, new EventArgs());
                UI(true);
                LittleHelp.CompleteTask(taskID);
                return s;
            }
            catch
            {
                StatusChanged(Localized.Ready, new EventArgs());
                UI(true);
                LittleHelp.CompleteTask(taskID, false);
                return null;
            }
        }
    }

    public class LabrinthResults
    {
        public class DownloadManager
        {
            public class File
            {
                public Hashes hashes { get; set; }
                public string url { get; set; }
                public string filename { get; set; }
                public bool primary { get; set; }
                public int size { get; set; }
            }
            public class DownloadLink
            {
                public string id { get; set; }
                public string project_id { get; set; }
                public string author_id { get; set; }
                public bool featured { get; set; }
                public string name { get; set; }
                public string version_number { get; set; }
                public string changelog { get; set; }
                public object changelog_url { get; set; }
                public DateTime date_published { get; set; }
                public int downloads { get; set; }
                public string version_type { get; set; }
                public List<File> files { get; set; }
                public List<object> dependencies { get; set; }
                public List<string> game_versions { get; set; }
                public List<string> loaders { get; set; }
            }
            public class Hashes
            {
                public string sha512 { get; set; }
                public string sha1 { get; set; }
            }
        }
        public enum SearchSortOptions
        {
            Relevance,
            Downloads,
            Follows,
            Updated,
            Newest
        }

        public enum SearchCategories
        {
            Adventure,
            Cursed,
            Decoration,
            Equipment,
            Food,
            Library,
            Magic,
            Misc,
            Optimization,
            Storage,
            Technology,
            Utility,
            Worldgen

        }
        public class Version
        {
            public string id { get; set; }
            public string project_id { get; set; }
            public string author_id { get; set; }
            public bool featured { get; set; }
            public string name { get; set; }
            public string version_number { get; set; }
            public string changelog { get; set; }
            public object changelog_url { get; set; }
            public DateTime date_published { get; set; }
            public int downloads { get; set; }
            public string version_type { get; set; }
            public File[] files { get; set; }
            public object[] dependencies { get; set; }
            public string[] game_versions { get; set; }
            public string[] loaders { get; set; }
        }

        public class File
        {
            public Hashes hashes { get; set; }
            public string url { get; set; }
            public string filename { get; set; }
            public bool primary { get; set; }
            public int size { get; set; }
        }

        public class Hashes
        {
            public string sha512 { get; set; }
            public string sha1 { get; set; }
        }
        public class SearchResult
        {
            public Hit[] hits { get; set; }
            public int offset { get; set; }
            public int limit { get; set; }
            public int total_hits { get; set; }
        }

        public class Hit
        {
            public string slug { get; set; }
            public string title { get; set; }
            public string description { get; set; }
            public string[] categories { get; set; }
            public string client_side { get; set; }
            public string server_side { get; set; }
            public string project_type { get; set; }
            public int downloads { get; set; }
            public string icon_url { get; set; }
            public string project_id { get; set; }
            public string author { get; set; }
            public string[] versions { get; set; }
            public int follows { get; set; }
            public DateTime date_created { get; set; }
            public DateTime date_modified { get; set; }
            public string latest_version { get; set; }
            public string license { get; set; }
            public string[] gallery { get; set; }
        }


        ///
        ///
        ///


        public class ModrinthProject
        {
            public string id { get; set; }
            public string slug { get; set; }
            public string project_type { get; set; }
            public string team { get; set; }
            public string title { get; set; }
            public string description { get; set; }
            public string body { get; set; }
            public string body_url { get; set; }
            public DateTime published { get; set; }
            public DateTime updated { get; set; }
            public string status { get; set; }
            public object moderator_message { get; set; }
            public License license { get; set; }
            public string client_side { get; set; }
            public string server_side { get; set; }
            public int downloads { get; set; }
            public int followers { get; set; }
            public string[] categories { get; set; }
            public string[] versions { get; set; }
            public string icon_url { get; set; }
            public string issues_url { get; set; }
            public string source_url { get; set; }
            public object wiki_url { get; set; }
            public string discord_url { get; set; }
            public Donation_Urls[] donation_urls { get; set; }
            public object[] gallery { get; set; }
        }

        public class License
        {
            public string id { get; set; }
            public string name { get; set; }
            public string url { get; set; }
        }

        public class Donation_Urls
        {
            public string id { get; set; }
            public string platform { get; set; }
            public string url { get; set; }
        }


    }
}
