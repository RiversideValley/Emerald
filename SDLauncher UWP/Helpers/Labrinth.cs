using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace SDLauncher_UWP.Helpers
{
    public static class LittleHelp
    {
        public static int AddTask(string name)
        {
            return vars.Launcher.TasksHelper.AddTask(name);
        }
        public static void CompleteTask(int ID)
        {
            vars.Launcher.TasksHelper.CompleteTask(ID);
        }
    }
    public class Labrinth
    {
        public event EventHandler StatusChanged = delegate { };
        public event EventHandler<SDLauncher.UIChangeRequestedEventArgs> UIChangeRequested = delegate { };
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
        public async Task<LabrinthResults.SearchResult> Search(string name, int? limit = null)
        {
            int taskID = 0;
            UI(false);
            if (name == "")
            {
                StatusChanged("Getting Mods",new EventArgs());
               taskID = LittleHelp.AddTask("Getting Mods");
            }
            else
            {
               taskID = LittleHelp.AddTask("Searcging Store");
               StatusChanged("Searcging Store", new EventArgs());
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
                var json = await Util.DownloadText("https://api.modrinth.com/v2/search?query=" + q + "&facets=[[%22categories:fabric%22],[%22project_type:mod%22]]&" + l);
                s = JSONConverter.ConvertToLabrinthSearchResult(json);
                StatusChanged("Ready", new EventArgs());
                UI(true);
                LittleHelp.CompleteTask(taskID);
                return s;
            }
            catch
            {
                StatusChanged("Ready", new EventArgs());
                UI(true);
                LittleHelp.CompleteTask(taskID);
                return null;
            }
        }
        public async Task<LabrinthResults.ModrinthProject> GetProject(string id)
        {
            int taskID = LittleHelp.AddTask("Loading Mod");
            UI(false);
            StatusChanged("Loading Mod",new EventArgs());
            LabrinthResults.ModrinthProject s = null;
            try
            {
                var json = await Util.DownloadText("https://api.modrinth.com/v2/project/" + id);
                s = JSONConverter.ConvertToLabrinthProject(json);
                StatusChanged("Ready", new EventArgs());
                UI(true);
                LittleHelp.CompleteTask(taskID);
                return s;
            }
            catch
            {
                StatusChanged("Ready", new EventArgs());
                UI(true);
                LittleHelp.CompleteTask(taskID);
                return null;
            }
        }
    }

    public class LabrinthResults
    {
        public enum SearchSortOptions
        {
            Relevance,
            Downloads,
            Follows,
            Updated
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
