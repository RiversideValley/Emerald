using CmlLib.Core;
using Emerald.Core.Tasks;
using Newtonsoft.Json;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Net.Http.Headers;
using System.Text.Json.Serialization;

namespace Emerald.Core.Store
{
    public class Labrinth
    {
        private HttpClient Client;
        public MinecraftPath MCPath { get; set; }

        public Labrinth(MinecraftPath path)
        {
            Client = new()
            {
                BaseAddress = new Uri("https://api.modrinth.com/v2/")
            };
            MCPath = path;
            Client.DefaultRequestHeaders.Accept.Clear();
            Client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }
        public Labrinth() : this(new MinecraftPath(MinecraftPath.GetOSDefaultPath())) { }


        private async Task<string> Get(string code)
        {
            HttpResponseMessage response = await Client.GetAsync(code);

            if (response.IsSuccessStatusCode)
                return await response.Content.ReadAsStringAsync();

            throw new Exception("Failed to GET: \"" + code + "\", StatusCode: " + response.StatusCode.ToString());

        }

        private int DownloadTaskID;
        private bool IsDownloading;

        public void DownloadMod(Results.File file, MinecraftPath mcPath = null)
        {
            if (IsDownloading)
            {
                var id = TasksHelper.AddProgressTask("DownloadMod", 0, 1, 0, "");
                TasksHelper.CompleteTask(id, false, "DownloaderBusy");
                return;
            }
            IsDownloading = true;
            DownloadTaskID = Tasks.TasksHelper.AddProgressTask($"{Localized.Download} {file.Filename}");
            var mods = System.IO.Directory.CreateDirectory((mcPath == null ? MCPath.BasePath : mcPath.BasePath) + "\\mods").FullName;
            ModrinthDownload(file.Url, mods, file.Filename);
        }

        private async void ModrinthDownload(string link, string folderdir, string fileName)
        {
            using var client = new FileDownloader(link, folderdir + "\\" + fileName);
            client.ProgressChanged += (totalFileSize, totalBytesDownloaded, progressPercentage) =>
            {
                TasksHelper.EditProgressTask(DownloadTaskID, Convert.ToInt32(progressPercentage));
                if (progressPercentage == 100)
                {
                    IsDownloading = false;
                    client.Dispose();
                    TasksHelper.CompleteTask(DownloadTaskID, true);
                }
            };

            await client.StartDownload();
        }

        public async Task<Results.SearchResult> Search(string name, int limit = 15, Enums.SearchSortOptions sortOptions = Enums.SearchSortOptions.Relevance, Enums.SearchCategories[] categories = null)
        {
            int taskID = name == "" ? Tasks.TasksHelper.AddTask(Localized.GettingMods) : TasksHelper.AddTask(Localized.SearchStore, name);

            string categouriesString = (categories != null && categories.Any() && categories.Length != 15) ? $"[\"categories:{string.Join("\"],[\"categories:", categories)}\"],".ToLower() : "";

            Results.SearchResult s;

            try
            {
                var fn = string.IsNullOrEmpty(name) ? "" : $"query={name}";
                var url = $"search?{fn}&index={sortOptions.ToString().ToLower()}&facets=[{categouriesString}[\"project_type:mod\"]]&limit={limit}";
                var json = await Get(url);
                s = JsonConvert.DeserializeObject<Results.SearchResult>(json);
                Tasks.TasksHelper.CompleteTask(taskID, true, name);

                return s;
            }
            catch (Exception ex)
            {
                Tasks.TasksHelper.CompleteTask(taskID, false, ex.Message);
                return null;
            }
        }

        public async Task<Results.ModrinthProject> GetProject(string id, string name)
        {
            int taskID = Tasks.TasksHelper.AddTask(Localized.LoadMod, name);
            Results.ModrinthProject s = null;

            try
            {
                var json = await Get("project/" + id);
                s = JsonConvert.DeserializeObject<Results.ModrinthProject>(json);
                Tasks.TasksHelper.CompleteTask(taskID, true, name);
                return s;
            }
            catch (Exception ex)
            {
                Tasks.TasksHelper.CompleteTask(taskID, false, ex.Message);
                return null;
            }
        }

        public async Task<List<Results.Version>> GetVersions(string id, string name)
        {
            int taskID = Tasks.TasksHelper.AddTask(Localized.LoadDownloadVers, name);
            List<Results.Version> s;

            try
            {
                var json = await Get("project/" + id + "/version");
                s = JsonConvert.DeserializeObject<List<Results.Version>>(json);
                Tasks.TasksHelper.CompleteTask(taskID, true);
                return s;
            }
            catch (Exception ex)
            {
                Tasks.TasksHelper.CompleteTask(taskID, false, ex.Message);
                return new();
            }
        }
    }
}
namespace Emerald.Core.Store.Enums
{
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
        Fabric,
        Forge,
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
}
namespace Emerald.Core.Store.Results
{

    public class File
    {

        [JsonPropertyName("hashes")]
        public Hashes Hashes { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("filename")]
        public string Filename { get; set; }

        [JsonPropertyName("primary")]
        public bool Primary { get; set; }

        [JsonPropertyName("size")]
        public int size { get; set; }
    }

    public class Version : INotifyPropertyChanged
    {
        public bool IsDetailsVisible = false;
        public string? FileName => Files.FirstOrDefault(x => x.Primary)?.Filename;

        [JsonPropertyName("id")]
        public string ID { get; set; }

        [JsonPropertyName("project_id")]
        public string Project_id { get; set; }

        [JsonPropertyName("author_id")]
        public string Author_id { get; set; }

        [JsonPropertyName("featured")]
        public bool Featured { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("version_number")]
        public string Version_number { get; set; }

        [JsonPropertyName("changelog")]
        public string Changelog { get; set; }

        [JsonPropertyName("changelog_url")]
        public object Changelog_url { get; set; }

        [JsonPropertyName("date_published")]
        public DateTime Date_published { get; set; }

        [JsonPropertyName("downloads")]
        public int Downloads { get; set; }

        [JsonPropertyName("version_type")]
        public string Version_type { get; set; }

        [JsonPropertyName("files")]
        public File[] Files { get; set; }

        [JsonPropertyName("dependencies")]
        public object[] Dependencies { get; set; }

        [JsonPropertyName("game_versions")]
        public string[] Game_versions { get; set; }

        [JsonPropertyName("loaders")]
        public string[] Loaders { get; set; }

       public event PropertyChangedEventHandler? PropertyChanged;
        public void InvokePropertyChanged(string propertyName = null)=> PropertyChanged?.Invoke(this, new(propertyName));
    }

    public class Hashes
    {

        [JsonPropertyName("sha512")]
        public string Sha512 { get; set; }

        [JsonPropertyName("sha1")]
        public string Sha1 { get; set; }
    }

    public class SearchResult
    {

        [JsonPropertyName("hits")]
        public Hit[] Hits { get; set; }

        [JsonPropertyName("offset")]
        public int Offset { get; set; }

        [JsonPropertyName("limit")]
        public int Limit { get; set; }

        [JsonPropertyName("total_hits")]
        public int Total_hits { get; set; }
    }

    public class Hit
    {

        [JsonPropertyName("slug")]
        public string Slug { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("categories")]
        public string[] Categories { get; set; }

        [JsonPropertyName("client_side")]
        public string Client_side { get; set; }

        [JsonPropertyName("server_side")]
        public string Server_side { get; set; }

        [JsonPropertyName("project_type")]
        public string Project_type { get; set; }

        [JsonPropertyName("downloads")]
        public int Downloads { get; set; }

        [JsonPropertyName("icon_url")]
        public string Icon_url { get; set; }

        [JsonPropertyName("project_id")]
        public string Project_ID { get; set; }

        [JsonPropertyName("author")]
        public string Author { get; set; }

        [JsonPropertyName("versions")]
        public string[] Versions { get; set; }

        [JsonPropertyName("follows")]
        public int Follows { get; set; }

        [JsonPropertyName("date_created")]
        public DateTime Date_created { get; set; }

        [JsonPropertyName("date_modified")]
        public DateTime Date_modified { get; set; }

        [JsonPropertyName("latest_version")]
        public string Latest_version { get; set; }

        [JsonPropertyName("license")]
        public string License { get; set; }

        [JsonPropertyName("gallery")]
        public string[] Gallery { get; set; }
    }

    public class ModrinthProject
    {
        [JsonPropertyName("id")]
        public string ID { get; set; }

        [JsonPropertyName("slug")]
        public string Slug { get; set; }

        [JsonPropertyName("project_type")]
        public string Project_Type { get; set; }

        [JsonPropertyName("team")]
        public string Team { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("body")]
        public string Body { get; set; }

        [JsonPropertyName("body_url")]
        public string Body_Url { get; set; }

        [JsonPropertyName("published")]
        public DateTime PublishedDate { get; set; }

        [JsonPropertyName("updated")]
        public DateTime UpdatedDate { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("moderator_message")]
        public object ModeratorMessage { get; set; }

        [JsonPropertyName("license")]
        public License License { get; set; }

        [JsonPropertyName("client_side")]
        public string Client_Side { get; set; }

        [JsonPropertyName("server_side")]
        public string Server_Side { get; set; }

        [JsonPropertyName("downloads")]
        public int Downloads { get; set; }

        [JsonPropertyName("followers")]
        public int Followers { get; set; }

        [JsonPropertyName("categories")]
        public string[] Categories { get; set; }

        [JsonPropertyName("versions")]
        public string[] Versions { get; set; }

        [JsonPropertyName("icon_url")]
        public string Icon_Url { get; set; }

        [JsonPropertyName("issues_url")]
        public string Issues_Url { get; set; }

        [JsonPropertyName("source_url")]
        public string Source_Url { get; set; }

        [JsonPropertyName("wiki_url")]
        public object Wiki_Url { get; set; }

        [JsonPropertyName("discord_url")]
        public string Discord_Url { get; set; }

        [JsonPropertyName("donation_urls")]
        public Donation_Urls[] Donation_Urls { get; set; }

        [JsonPropertyName("gallery")]
        public object[] Gallery { get; set; }
    }

    public class License
    {
        [JsonPropertyName("id")]
        public string ID { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }
    }

    public class Donation_Urls
    {

        [JsonPropertyName("id")]
        public string ID { get; set; }

        [JsonPropertyName("platform")]
        public string Platform { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }
    }
}