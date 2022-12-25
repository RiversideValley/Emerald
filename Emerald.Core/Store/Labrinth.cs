using Emerald.Core.Args;
using System.Net.Http.Headers;
using System.Web;
namespace Emerald.Core.Store
{
    public class Labrinth
    {
        public event EventHandler StatusChanged = delegate { };
        public event EventHandler<UIChangeRequestedEventArgs> UIChangeRequested = delegate { };
        public event EventHandler<UIChangeRequestedEventArgs> MainUIChangeRequested = delegate { };
        public event EventHandler<ProgressChangedEventArgs> ProgressChanged = delegate { };
        public HttpClient Client;
        //ModrinthClient c = new ModrinthClient();
        public Labrinth()
        {
            Client = new HttpClient();
            Client.BaseAddress = new Uri("https://api.modrinth.com/");
            Client.DefaultRequestHeaders.Accept.Clear();
            Client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }
        public async Task<string> Get(string code)
        {
            try
            {
                HttpResponseMessage response = await Client.GetAsync(code);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync();
                }
                else
                {
                    throw new Exception("Failed to get: " + code);
                }
            }
            catch
            {
                throw new Exception("Failed to get: " + code);
            }
        }
        private void UI(bool UI)
        {
            UIChangeRequested(this, new UIChangeRequestedEventArgs(UI));
        }
        int DownloadTaskID;
        public void DownloadMod(LabrinthResults.DownloadManager.File file, CmlLib.Core.MinecraftPath mcPath)
        {
            this.MainUIChangeRequested(this, new UIChangeRequestedEventArgs(false));
            DownloadTaskID = Tasks.TasksHelper.AddTask($"{Localized.Download} {file.filename}");
            MainUIChangeRequested(this, new UIChangeRequestedEventArgs(false));
            var mods = System.IO.Directory.CreateDirectory(mcPath.BasePath + "\\mods").FullName;
            ModrinthDownload(file.url, mods, file.filename);
        }
        private async void ModrinthDownload(string link, string folderdir, string fileName)
        {
            using (var client = new FileDownloader(link, folderdir + "\\" + fileName))
            {
                client.ProgressChanged += (totalFileSize, totalBytesDownloaded, progressPercentage) =>
                {
                    this.MainUIChangeRequested(this, new UIChangeRequestedEventArgs(false));
                    StatusChanged($"{Localized.Downloading} : {fileName}", new EventArgs());
                    this.ProgressChanged(this, new ProgressChangedEventArgs(currentProg: Convert.ToInt32(progressPercentage), maxfiles: 100, currentfile: Convert.ToInt32(progressPercentage)));
                    if (progressPercentage == 100)
                    {
                        this.DownloadFileCompleted();
                        client.Dispose();
                        this.MainUIChangeRequested(this, new UIChangeRequestedEventArgs(true));
                        Tasks.TasksHelper.CompleteTask(DownloadTaskID, true);
                    }
                };

                await client.StartDownload();
            }
        }
        private void DownloadFileCompleted()
        {
            StatusChanged(Localized.Ready, new EventArgs());
            ProgressChanged(this, new ProgressChangedEventArgs(currentProg: 0, currentfile: 0));
            MainUIChangeRequested(this, new UIChangeRequestedEventArgs(true));
        }
        public async Task<LabrinthResults.SearchResult> Search(string name, int? limit = null, LabrinthResults.SearchSortOptions sortOptions = LabrinthResults.SearchSortOptions.Relevance, LabrinthResults.SearchCategories[] categories = null)
        {
            int taskID = 0;
            if (name == "")
            {
                StatusChanged(Localized.GettingMods, new EventArgs());
                taskID = Tasks.TasksHelper.AddTask(Localized.GettingMods);
            }
            else
            {
                taskID = Tasks.TasksHelper.AddTask(Localized.SearchStore);
                StatusChanged(Localized.SearchingStore, new EventArgs());
            }
            string categouriesString = "";
            if (categories != null)
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
                var json = await Get("v2/search?query=" + q + "&index=" + sortOptions.ToString().ToLower() + "&facets=[[\"categories:fabric\"]" + categouriesString + ",[\"project_type:mod\"]]&" + l);
                s = JSONConverter.ConvertToLabrinthSearchResult(json);
                StatusChanged(Localized.Ready, new EventArgs());
                Tasks.TasksHelper.CompleteTask(taskID, true);
                return s;
            }
            catch
            {
                StatusChanged(Localized.Ready, new EventArgs());
                Tasks.TasksHelper.CompleteTask(taskID, false);
                return null;
            }
        }
        public async Task<LabrinthResults.ModrinthProject> GetProject(string id, bool UIChange = true)
        {
            int taskID = Tasks.TasksHelper.AddTask(Localized.LoadMod);
            if (UIChange)
            {
                UI(false);
            }
            StatusChanged(Localized.LoadingMod, new EventArgs());
            LabrinthResults.ModrinthProject s = null;
            try
            {
                var json = await Get("v2/project/" + id);
                s = JSONConverter.ConvertToLabrinthProject(json);
                StatusChanged(Localized.Ready, new EventArgs());
                if (UIChange)
                {
                    UI(true);
                }
                Tasks.TasksHelper.CompleteTask(taskID, true);
                return s;
            }
            catch
            {
                StatusChanged(Localized.Ready, new EventArgs());
                if (UIChange)
                {
                    UI(true);
                }
                Tasks.TasksHelper.CompleteTask(taskID, false);
                return null;
            }
        }
        public async Task<List<LabrinthResults.DownloadManager.DownloadLink>> GetVersions(string id)
        {
            int taskID = Tasks.TasksHelper.AddTask(Localized.LoadDownloadVers);
            UI(false);
            StatusChanged(Localized.LoadingDownloadVers, new EventArgs());
            List<LabrinthResults.DownloadManager.DownloadLink> s = null;
            try
            {
                var json = await Get("v2/project/" + id + "/version");
                s = JSONConverter.ConvertDownloadLinksToCS(json);
                StatusChanged(Localized.Ready, new EventArgs());
                UI(true);
                Tasks.TasksHelper.CompleteTask(taskID, true);
                return s;
            }
            catch
            {
                StatusChanged(Localized.Ready, new EventArgs());
                UI(true);
                Tasks.TasksHelper.CompleteTask(taskID, false);
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
