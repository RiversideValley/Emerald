using CmlLib.Core;
using CmlLib.Core.Downloader;
using CmlLib.Core.Installer.FabricMC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CmlLib.Core.Version;
using CmlLib.Core.VersionLoader;
using SDLauncher.UWP.Resources;
using CmlLib.Utils;
using Windows.ApplicationModel.Core;
using Microsoft.Toolkit.Uwp.Notifications;
using Windows.UI.Notifications;

namespace SDLauncher.UWP.Helpers
{
    public class TasksHelper
    {
        public event EventHandler<UserControls.Task> TaskAddRequested = delegate { };
        public event EventHandler<TaskCompletedEventArgs> TaskCompleteRequested = delegate { };
        public int AllTaksCount { get; private set; } = 0;
        public int AddTask(string name)
        {
            AllTaksCount++;
            TaskAddRequested(this, new UserControls.Task(name, AllTaksCount));
            return AllTaksCount;
        }
        public void CompleteTask(int ID,bool success)
        {
            TaskCompleteRequested(this, new TaskCompletedEventArgs(ID,success));
        }
    }
    public class TaskCompletedEventArgs
    {
        public int ID;
        public bool Success;
        public TaskCompletedEventArgs(int iD, bool success)
        {
            ID = iD;
            Success = success;
        }
    }
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
        public StoreManager StoreManager { get; private set; }

        public CMLauncher Launcher { get; set; }
        public Labrinth Labrinth { get; set; }
        public TasksHelper TasksHelper { get; set; }
        public GlacierClient GlacierClient { get; set; }
        public SDLauncher()
        {

            GlacierClient = new GlacierClient();
            GlacierClient.StatusChanged += GlacierClient_StatusChanged;
            GlacierClient.ProgressChanged += GlacierClient_ProgressChanged; ;
            GlacierClient.UIChangedReqested += GlacierClient_UIChangedReqested;

            StoreManager = new StoreManager();
            Labrinth = new Labrinth();
            Labrinth.StatusChanged += Labrinth_StatusChanged;
            Labrinth.MainUIChangeRequested += Labrinth_MainUIChangeRequested;
            Labrinth.ProgressChanged += Labrinth_ProgressChanged;

            this.TasksHelper = new TasksHelper();

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
                var final = from t in items where t.Replace(ver,"").ToString().StartsWith(".") || t == ver select t;
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
            if(e.ProgressPercentage != null && e.DownloadArgs != null)
            {
                stats = e.MaxFiles.Value + " of " + e.CurrentFile.Value;
                int per = e.MaxFiles.Value / e.CurrentFile.Value * 100;
                ProgPrecentage = e.ProgressPercentage.Value;
                UpdateToast(e.DownloadArgs.FileKind.ToString(), stats, per);
            }
        }

        private void Labrinth_StatusChanged(object sender, EventArgs e)
        {
            Status((string)sender);
        }
        public void CreateToast()
        {
            // Define a tag (and optionally a group) to uniquely identify the notification, in order update the notification data later;
            string tag = "Minecraft";
            string group = "downloads";

            // Construct the toast content with data bound fields
            var content = new ToastContentBuilder()
                .AddText("Launching Minecraft...")
                .AddVisualChild(new AdaptiveProgressBar()
                {
                    Title = new BindableString("Type"),
                    Status = "",
                    Value = new BindableProgressBarValue("Pogress"),
                    ValueStringOverride = new BindableString("progressValueString"),
                })
                .GetToastContent();

            // Generate the toast notification
            var toast = new ToastNotification(content.GetXml());

            // Assign the tag and group
            toast.Tag = tag;
            toast.Group = group;

            // Assign initial NotificationData values
            // Values must be of type string
            toast.Data = new NotificationData();
            toast.Data.Values["Type"] = "Minecraft";
            toast.Data.Values["Pogress"] = "0";
            toast.Data.Values["progressValueString"] = "15/26 songs";

            // Provide sequence number to prevent out-of-order updates, or assign 0 to indicate "always update"
            toast.Data.SequenceNumber = 1;

            // Show the toast notification to the user
            ToastNotificationManager.CreateToastNotifier().Show(toast);
        }
        public void UpdateToast(string type, string valueString,int progress)
        {
            string tag = "Minecraft";
            string group = "downloads";

            // Create NotificationData and make sure the sequence number is incremented
            // since last update, or assign 0 for updating regardless of order
            var data = new NotificationData
            {
                SequenceNumber = 2
            };

            // Assign new values
            // Note that you only need to assign values that changed. In this example
            // we don't assign progressStatus since we don't need to change it
            data.Values["Pogress"] = progress.ToString();
            data.Values["Type"] = type;
            data.Values["progressValueString"] = valueString;

            // Update the existing notification's data by using tag/group
            ToastNotificationManager.CreateToastNotifier().Update(data, tag, group);
        }
        public async Task<bool> LoadStore()
        {
            Status("Loading Store");
            int taskID = TasksHelper.AddTask("Load Store");
            this.StoreManager.store = await StoreManager.GetStore();

            if(this.StoreManager.store == null)
            {
                var result = await MessageBox.Show("Error", "Failed to load the data of the store, Retry", MessageBoxButtons.OkCancel);
                if(result == MessageBoxResults.Ok)
                {
                    TasksHelper.CompleteTask(taskID, false);
                    Status(Localized.Ready);
                    var s = await LoadStore();
                    return s;
                }
                else
                {
                    Status(Localized.Ready);
                    TasksHelper.CompleteTask(taskID,false);
                    return false;
                }
            }
            else
            {
                Status(Localized.Ready);
                TasksHelper.CompleteTask(taskID,true);
                return true;
            }
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
                html += await vars.Launcher.GetChangelog(vars.Launcher.Launcher.Versions.LatestSnapshotVersion.Name);
                if (vars.Launcher.Launcher.Versions.LatestReleaseVersion.Name != "1.19")
                {
                    html += await vars.Launcher.GetChangelog(vars.Launcher.Launcher.Versions.LatestReleaseVersion.Name);
                }
            }
            catch { }
            try
            {
                html += await vars.Launcher.GetChangelog("1.19");
                html += await vars.Launcher.GetChangelog("1.18.2");
                html += await vars.Launcher.GetChangelog("1.18.1");
                html += await vars.Launcher.GetChangelog("1.18");
                html += await vars.Launcher.GetChangelog("1.17.1");
                html += await vars.Launcher.GetChangelog("1.16.5");
                TasksHelper.CompleteTask(taskID,true);
            }
            catch
            {
                TasksHelper.CompleteTask(taskID,false);
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

        public async Task RefreshVersions()
        {
            UI(false);
            int taskID = TasksHelper.AddTask("Refresh Versions");
            try { 
            Status(Localized.GettingVers);
            MCVersions = await Launcher.GetAllVersionsAsync();
                if (!UseOfflineLoader)
                {
                    var fabricVersionLoader = new FabricVersionLoader();
                    FabricMCVersions = await fabricVersionLoader.GetVersionMetadatasAsync();
                }
            Status(Localized.Ready);
            VersionsRefreshed(this, new EventArgs());
                TasksHelper.CompleteTask(taskID,true);
            }
            catch
            {
                var result = await MessageBox.Show("Error", "Couldn't detect a valid internet connecton.Do you want to retry or switch to offfline mode ? (you can switch to online mode again by restarting the app.)", MessageBoxButtons.CustomWithCancel, "Retry", "Switch to offline mode");
                if (result == MessageBoxResults.CustomResult1)
                {
                    TasksHelper.CompleteTask(taskID, false);
                    await RefreshVersions();
                }
                else if(result == MessageBoxResults.CustomResult2)
                {
                    vars.VerSelectors = Views.VerSelectors.Classic;
                    TasksHelper.CompleteTask(taskID, false);
                    int offTask = TasksHelper.AddTask("Switch to offline mode");
                    Launcher.VersionLoader = new LocalVersionLoader(Launcher.MinecraftPath);
                    UseOfflineLoader = true;
                    TasksHelper.CompleteTask(offTask,true);
                    await RefreshVersions();
                }
                else
                {
                    CoreApplication.Exit();
                }
            }
            UI(true);
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
            FileOrProgressChanged(this, new ProgressChangedEventArgs(maxfiles: e.ProgressedFileCount, currentfile: e.TotalFileCount, args: e,currentProg: CurrentProg)) ;
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
                if (await MessageBox.Show("Error", "To run " + displayver + " you need to have installed version " + mcver + ". Vanilla,Do you want to install now ?", MessageBoxButtons.YesNo) == MessageBoxResults.Yes)
                {
                    displayVer = mcver;
                    launchVer = mcver;
                    TasksHelper.CompleteTask(taskID, true);
                    return new FabricResponsoe(launchVer, displayVer, FabricResponsoe.Responses.NeedMojangVer);
                }
                else
                {
                    TasksHelper.CompleteTask(taskID,false);
                    return new FabricResponsoe("", "Version", FabricResponsoe.Responses.NeedMojangVer);
                }
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
                NeedMojangVer
            }
            public FabricResponsoe(string launchver, string displayver, Responses response)
            {
                this.LaunchVer = launchver;
                this.DisplayVer = displayver;
                this.Response = response;
            }
        }
        public class UIChangeRequestedEventArgs : EventArgs
        {
            public bool UI { get; set; }
            public UIChangeRequestedEventArgs(bool ui)
            {
                this.UI = ui;
            }
        }
        public class StatusChangedEventArgs : EventArgs
        {
            public string Status { get; set; }
            public StatusChangedEventArgs(string status)
            {
                this.Status = status;
            }
        }
        public class ProgressChangedEventArgs : EventArgs
        {
            public int? MaxFiles { get; set; }
            //
            public int? CurrentFile { get; set; }
            //
            public int? ProgressPercentage { get; set; }
            public DownloadFileChangedEventArgs DownloadArgs { get; set; }
            public ProgressChangedEventArgs(int? currentfile = null, int? maxfiles = null, int? currentProg = null, DownloadFileChangedEventArgs args = null)
            {
                MaxFiles = maxfiles;
                CurrentFile = currentfile;
                ProgressPercentage = currentProg;
                DownloadArgs = args;
            }
        }
    }
}