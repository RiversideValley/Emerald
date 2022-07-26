using CmlLib.Core;
using CmlLib.Core.Downloader;
using CmlLib.Core.Installer.FabricMC;
using CmlLib.Core.Version;
using CmlLib.Core.VersionLoader;
using CmlLib.Utils;
using SDLauncher.Core.Args;
using SDLauncher.Core.Clients;
using SDLauncher.Core.Store;
using SDLauncher.Core.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SDLauncher.Core
{
    public class SDLauncher
    {
        public event EventHandler<UIChangeRequestedEventArgs> UIChangeRequested = delegate { };
        public event EventHandler<StatusChangedEventArgs> StatusChanged = delegate { };
        public event EventHandler<ProgressChangedEventArgs> FileOrProgressChanged = delegate { };
        public event EventHandler VersionLoaderChanged = delegate { };
        public event EventHandler VersionsRefreshed = delegate { };
        public event EventHandler LogsUpdated = delegate { };
        private bool offlineloader = false;
        public bool UseOfflineLoader { get { return offlineloader; } set { offlineloader = value; VersionLoaderChanged(this, new EventArgs()); } }

        public List<string> MCVerNames
        {
            get
            {
                if (MCVersions != null)
                {
                    List<string> temp = new List<string>();
                    foreach (var item in MCVersions)
                    {
                        temp.Add(item.Name);
                    }
                    return temp;
                }
                else
                {
                    return null;
                }
            }
        }
        public List<string> FabricMCVerNames
        {
            get
            {
                if (FabricMCVersions != null)
                {
                    List<string> temp = new List<string>();
                    foreach (var item in FabricMCVersions)
                    {
                        temp.Add(item.Name);
                    }
                    return temp;
                }
                else
                {
                    return null;
                }
            }
        }
        public MVersionCollection MCVersions { get; private set; }
        public MVersionCollection FabricMCVersions { get; private set; }
        public CMLauncher Launcher { get; set; }
        public Labrinth Labrinth { get; set; }
        public GlacierClient GlacierClient { get; set; }
        public SDLauncher()
        {

            GlacierClient = new GlacierClient();
            GlacierClient.StatusChanged += GlacierClient_StatusChanged;
            GlacierClient.ProgressChanged += GlacierClient_ProgressChanged;
            GlacierClient.UIChangedReqested += GlacierClient_UIChangedReqested;
            
            Labrinth = new Labrinth();
            Labrinth.StatusChanged += Labrinth_StatusChanged;
            Labrinth.MainUIChangeRequested += Labrinth_MainUIChangeRequested;
            Labrinth.ProgressChanged += Labrinth_ProgressChanged;


            this.FileOrProgressChanged += SDLauncher_FileOrProgressChanged;
        }

        private void Labrinth_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            this.FileOrProgressChanged(sender, e);
        }

        private void Labrinth_MainUIChangeRequested(object sender, UIChangeRequestedEventArgs e)
        {
            UI(e.UI);
        }
        public string SearchFabric(string ver)
        {
            try
            {
                var item = from t in FabricMCVerNames where t.EndsWith(ver) select t;
                if (item != null)
                {
                    return item.FirstOrDefault();
                }
                else
                {
                    return "";
                }
            }
            catch
            {
                return "";
            }
        }
        public string[] GetSubVersions(string ver)
        {
            var items = from t in MCVersions where t.Name.StartsWith(ver) select t.Name;
            if (items != null)
            {
                var final = from t in items where t.Replace(ver, "").ToString().StartsWith(".") || t == ver select t;
                return final.ToArray();
            }
            else
            {
                return new List<string>().ToArray();
            }
        }
        private void SDLauncher_FileOrProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            string stats = "";
            int ProgPrecentage;
            if (e.ProgressPercentage != null && e.DownloadArgs != null)
            {
                stats = e.MaxFiles.Value + " of " + e.CurrentFile.Value;
                int per = e.MaxFiles.Value / e.CurrentFile.Value * 100;
                ProgPrecentage = e.ProgressPercentage.Value;
            }
        }

        private void Labrinth_StatusChanged(object sender, EventArgs e)
        {
            Status((string)sender);
        }

        private string changeLogsHTMLBody;
        public string ChangeLogsHTMLBody
        {
            get { return changeLogsHTMLBody; }
            private set { changeLogsHTMLBody = value; LogsUpdated(this, new EventArgs()); }
        }

        public async Task LoadChangeLogs()
        {
            var taskID = TasksHelper.AddTask("Load ChangeLogs");
            string html = "";
            try
            {
                html += await GetChangelog(Launcher.Versions.LatestSnapshotVersion.Name);
                if (Launcher.Versions.LatestReleaseVersion.Name != "1.19")
                {
                    html += await GetChangelog(Launcher.Versions.LatestReleaseVersion.Name);
                }
            }
            catch { }
            try
            {
                html += await GetChangelog("1.19");
                html += await GetChangelog("1.18.2");
                html += await GetChangelog("1.18.1");
                html += await GetChangelog("1.18");
                html += await GetChangelog("1.17.1");
                html += await GetChangelog("1.16.5");
                TasksHelper.CompleteTask(taskID, true);
            }
            catch
            {
                TasksHelper.CompleteTask(taskID, false);
            }
            ChangeLogsHTMLBody = html;
        }
        public async Task<string> GetChangelog(string version)
        {
            Status("Loading changelog v:" + version);
            Changelogs changelogs = await Changelogs.GetChangelogs(); // get changelog informations
            string[] versions = changelogs.GetAvailableVersions(); // get all available versions
            var changelogHtml = await changelogs.GetChangelogHtml(version);

            var fullbody = "<style>\np,h1,li,span,body,html {\nfont-family:\"Segoe UI\";\n}\n</style>\n" + "<h1>Version " + version + "</h1>" + changelogHtml;
            Status(Localized.Ready);
            return fullbody.Replace("h1", "h2").ToString();
        }
        private void GlacierClient_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            FileOrProgressChanged(sender, e);
        }

        private void GlacierClient_UIChangedReqested(object sender, EventArgs e)
        {
            UIChangeRequested(this, new UIChangeRequestedEventArgs((bool)sender));
        }


        private void GlacierClient_StatusChanged(object sender, EventArgs e)
        {
            StatusChanged(this, new StatusChangedEventArgs(sender.ToString()));
        }

        public static SDLauncher CreateLauncher(MinecraftPath mcpath)
        {
            var l = new SDLauncher();
            l.InitializeLauncher(mcpath);
            return l;
        }

        private void UI(bool ui)
        {
            UIChangeRequested(this, new UIChangeRequestedEventArgs(ui));
        }
        private void Status(string stats)
        {
            StatusChanged(this, new StatusChangedEventArgs(stats));
        }
        public void InitializeLauncher(MinecraftPath path)
        {
            UI(false);
            Launcher = new CMLauncher(path);
            Launcher.FileChanged += Launcher_FileChanged;
            Launcher.ProgressChanged += Launcher_ProgressChanged;
            UI(true);
        }

        public async Task<bool> RefreshVersions()
        {
            UI(false);
            int taskID = TasksHelper.AddTask("Refresh Versions");
            try
            {
                Status(Localized.GettingVers);
                MCVersions = await Launcher.GetAllVersionsAsync();
                if (!UseOfflineLoader)
                {
                    var fabricVersionLoader = new FabricVersionLoader();
                    FabricMCVersions = await fabricVersionLoader.GetVersionMetadatasAsync();
                }
                Status(Localized.Ready);
                VersionsRefreshed(this, new EventArgs());
                TasksHelper.CompleteTask(taskID, true);
                UI(true);
                return true;
            }
            catch
            {
                TasksHelper.CompleteTask(taskID, false);
                Status(Localized.Ready);
                UI(true);
                return false;
            }
        }
        public void SwitchToOffilineMode()
        {
            int offTask = TasksHelper.AddTask("Switch to offline mode");
            Launcher.VersionLoader = new LocalVersionLoader(Launcher.MinecraftPath);
            UseOfflineLoader = true;
            TasksHelper.CompleteTask(offTask, true);
        }

        private int CurrentProg;
        private void Launcher_ProgressChanged(object sender, System.ComponentModel.ProgressChangedEventArgs e)
        {
            CurrentProg = e.ProgressPercentage;
            FileOrProgressChanged(this, new ProgressChangedEventArgs(currentProg: e.ProgressPercentage));
        }

        private void Launcher_FileChanged(DownloadFileChangedEventArgs e)
        {
            Status($"{e.FileKind} : {e.FileName} ({e.ProgressedFileCount}/{e.TotalFileCount})");
            FileOrProgressChanged(this, new ProgressChangedEventArgs(maxfiles: e.ProgressedFileCount, currentfile: e.TotalFileCount, args: e, currentProg: CurrentProg));
        }

        public async Task<FabricResponsoe> CheckFabric(string mcver, string modver, string displayver)
        {
            int taskID = TasksHelper.AddTask("Get Fabric " + mcver);
            string launchVer = "";
            string displayVer = "";
            bool exists = false;
            await RefreshVersions();
            foreach (var veritem in FabricMCVersions)
            {
                if (veritem.Name == modver)
                {
                    exists = true;
                }
            }
            if (exists)
            {
                launchVer = modver;
                displayVer = displayver;
                Status("Getting Fabric");
                UI(false);
                var fabric = FabricMCVersions.GetVersionMetadata(launchVer);
                await fabric.SaveAsync(Launcher.MinecraftPath);
                UI(true);
                Status(Localized.Ready);
                await RefreshVersions();
                launchVer = modver;
                displayVer = displayver;
                Status(Localized.Ready);
                TasksHelper.CompleteTask(taskID, true);
                return new FabricResponsoe(launchVer, displayVer, FabricResponsoe.Responses.ExistsOrCreated);
            }
            else
            {
                TasksHelper.CompleteTask(taskID, false);
                return new FabricResponsoe(launchVer, displayVer, FabricResponsoe.Responses.NotExists);
            }
        }

        public class FabricResponsoe
        {
            public string LaunchVer { get; }

            public string DisplayVer { get; }

            public Responses Response { get; }

            public enum Responses
            {
                ExistsOrCreated,
                NotExists
            }
            public FabricResponsoe(string launchver, string displayver, Responses response)
            {
                this.LaunchVer = launchver;
                this.DisplayVer = displayver;
                this.Response = response;
            }
        }

    }
}
