using CmlLib.Core;
using CmlLib.Core.Downloader;
using CmlLib.Core.Installer.FabricMC;
using CmlLib.Core.Version;
using CmlLib.Core.VersionLoader;
using CmlLib.Utils;
using Emerald.Core.Args;
using Emerald.Core.Clients;
using Emerald.Core.Store;
using Emerald.Core.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Emerald.Core
{
    public class Emerald
    {
        public event EventHandler<UIChangeRequestedEventArgs> UIChangeRequested = delegate { };
        public event EventHandler<StatusChangedEventArgs> StatusChanged = delegate { };
        public event EventHandler<ProgressChangedEventArgs> FileOrProgressChanged = delegate { };
        public event EventHandler VersionLoaderChanged = delegate { };
        public event EventHandler<VersionsRefreshedEventArgs> VersionsRefreshed = delegate { };
        public event EventHandler LogsUpdated = delegate { };
        private bool offlineloader = false;

        /// <summary>
        /// Checks whether the launcher is in Offline mode
        /// </summary>
        public bool UseOfflineLoader { get { return offlineloader; } set { offlineloader = value; VersionLoaderChanged(this, new EventArgs()); } }

        /// <summary>
        /// The stirng list of Available Minecraft versions
        /// </summary>
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
                    return new List<string>();
                }
            }
        }


        /// <summary>
        /// The stirng list of Available FabricMC versions
        /// </summary>
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
                    return new List<string>();
                }
            }
        }
        public MVersionCollection MCVersions { get; private set; }
        public MVersionCollection FabricMCVersions { get; private set; }
        public CMLauncher Launcher { get; set; }
        public GlacierClient GlacierClient { get; set; }
        public Emerald()
        {

            GlacierClient = new GlacierClient();
            GlacierClient.StatusChanged += GlacierClient_StatusChanged;
            GlacierClient.ProgressChanged += GlacierClient_ProgressChanged;
            GlacierClient.UIChangedReqested += GlacierClient_UIChangedReqested;
            
        }

        /// <summary>
        /// Creates a Minecraft <see cref="System.Diagnostics.Process"/> using the given <paramref name="ver"/> and <paramref name="launchOption"/>
        /// </summary>
        public async Task<System.Diagnostics.Process> CreateProcessAsync(string ver, MLaunchOption launchOption)
        {
            var id = TasksHelper.AddTask(Localized.LaunchMC);
            try
            {
                var p = await Launcher.CreateProcessAsync(ver, launchOption);
                TasksHelper.CompleteTask(id, true);
                return p;
            }
            catch (Exception ex)
            {
                TasksHelper.CompleteTask(id, false, ex.Message);
                return null;
            }
            
        }
        private void Labrinth_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            this.FileOrProgressChanged(sender, e);
        }

        private void Labrinth_MainUIChangeRequested(object sender, UIChangeRequestedEventArgs e)
        {
            UI(e.UI);
        }


        /// <summary>
        /// Gets the FabricMC version name using the given Minecraft <paramref name="ver"/>
        /// </summary>
        public string SearchFabric(string ver)
        {
            try
            {
                var item = FabricMCVerNames.Where(x => x.EndsWith(ver));
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


        /// <summary>
        /// Gets the subversions of the given <paramref name="ver"/> as an <see cref="string"/>[]
        /// </summary>
        public string[] GetSubVersions(string ver)
        {
            var items = MCVersions.Where(x => x.Name.StartsWith(ver)).Select(x => x.Name);
            if (items != null)
            {
                var final = from t in items where t.Replace(ver, "").StartsWith(".") || t == ver select t;
                return final.ToArray();
            }
            else
            {
                return Array.Empty<string>();
            }
        }


        private string changeLogsHTMLBody = "";
        /// <summary>
        /// Changelogs HTML
        /// </summary>
        public string ChangeLogsHTMLBody
        {
            get { return string.IsNullOrEmpty(changeLogsHTMLBody) ? "" : changeLogsHTMLBody; }
            set { changeLogsHTMLBody = value; LogsUpdated(this, new EventArgs()); }
        }

        /// <summary>
        /// Gets the changelogs HTML
        /// </summary>
        public async Task LoadChangeLogs()
        {
            var taskID = TasksHelper.AddTask(Localized.LoadChangeLogs);
            string html = "";
            try
            {
                if (Launcher.Versions.LatestSnapshotVersion.Name != Launcher.Versions.LatestReleaseVersion.Name)
                {
                    html += await GetChangelog(Launcher.Versions.LatestSnapshotVersion.Name);
                    UpdateLogs(html);
                }
                if (Launcher.Versions.LatestReleaseVersion.Name != "1.19.1")
                {
                    html += await GetChangelog(Launcher.Versions.LatestReleaseVersion.Name);
                    UpdateLogs(html);
                }
            }
            catch { }
            try
            {
                html += await GetChangelog("1.19");
                UpdateLogs(html);
                html += await GetChangelog("1.18.2");
                UpdateLogs(html);
                html += await GetChangelog("1.18.1");
                UpdateLogs(html);
                html += await GetChangelog("1.18");
                UpdateLogs(html);
                html += await GetChangelog("1.17.1");
                UpdateLogs(html);
                html += await GetChangelog("1.16.5");
                UpdateLogs(html);
                TasksHelper.CompleteTask(taskID, true);
            }
            catch (Exception ex)
            {
                TasksHelper.CompleteTask(taskID, false,ex.Message);
            }
        }
        private void UpdateLogs(string html)
        {
            if (!string.IsNullOrEmpty(html.Replace(" ", "")))
            {
                ChangeLogsHTMLBody = html;
            }
        }


        /// <summary>
        /// Get the changelog HTML of the given <paramref name="version"/>
        /// </summary>
        public async Task<string> GetChangelog(string version)
        {
            Status($"{Localized.LoadingChangeLogs} v:" + version);
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

        public static Emerald CreateLauncher(MinecraftPath mcpath)
        {
            var l = new Emerald();
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
        private void Status(Localized stats)
        {
            StatusChanged(this, new StatusChangedEventArgs(stats.ToString()));
        }
        public void InitializeLauncher(MinecraftPath path)
        {
            UI(false);
            Launcher = new CMLauncher(path);
            Launcher.FileChanged += Launcher_FileChanged;
            Launcher.ProgressChanged += Launcher_ProgressChanged;
            UI(true);
        }


        /// <summary>
        /// Refreshes the available Minecraft/Fabric Versions
        /// </summary>
        /// <returns>
        /// <see cref="true"/> if succeed,
        /// <see cref="false"/> if failed
        /// </returns>
        public async Task<bool> RefreshVersions()
        {
            UI(false);
            int taskID = TasksHelper.AddTask(Localized.RefreshVers);
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
                VersionsRefreshed(this, new VersionsRefreshedEventArgs(true));
                TasksHelper.CompleteTask(taskID, true);
                UI(true);
                return true;
            }
            catch (Exception ex)
            {
                TasksHelper.CompleteTask(taskID, false, ex.Message);
                Status(Localized.Ready);
                UI(true);
                VersionsRefreshed(this, new VersionsRefreshedEventArgs(false));
                return false;
            }
        }
        /// <summary>
        /// Switches the launcher to offline mode, can't get back online until restart
        /// </summary>
        public void SwitchToOffilineMode()
        {
            int offTask = TasksHelper.AddTask(Localized.SwitchOffline);
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
            FileOrProgressChanged(this, new ProgressChangedEventArgs(maxfiles: e.TotalFileCount, currentfile: e.ProgressedFileCount, args: e, currentProg: CurrentProg));
        }


        /// <summary>
        /// Checks fabric whether it exists if not install it
        /// </summary>
        public async Task<FabricResponsoe> CheckFabric(string mcver, string modver, string displayver)
        {
            int taskID = TasksHelper.AddTask($"{Localized.GetFabric} " + mcver);
            string launchVer = "";
            string displayVer = Localized.PickVer.ToString();
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
                Status(Localized.GettingFabric);
                UI(false);
                var fabric = FabricMCVersions.GetVersionMetadata(launchVer);
                await fabric.SaveAsync(Launcher.MinecraftPath);
                await RefreshVersions();
                UI(true);
                Status(Localized.Ready);
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
