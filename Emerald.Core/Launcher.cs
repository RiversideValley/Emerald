using CmlLib.Core;
using CmlLib.Core.Downloader;
using CmlLib.Core.Files;
using CmlLib.Core.Installer.FabricMC;
using CmlLib.Core.Version;
using CmlLib.Core.VersionLoader;
using CmlLib.Utils;
using Emerald.Core.Args;
using Emerald.Core.Clients;
using Emerald.Core.News;
using Emerald.Core.Store;
using Emerald.Core.Tasks;
using ProjBobcat.Class.Model.Optifine;
using System.ComponentModel;
using Windows.System;
using ProgressChangedEventArgs = Emerald.Core.Args.ProgressChangedEventArgs;

namespace Emerald.Core
{
    public class Emerald : INotifyPropertyChanged
    {
        public event EventHandler<UIChangeRequestedEventArgs> UIChangeRequested = delegate { };

        public event EventHandler<StatusChangedEventArgs> StatusChanged = delegate { };

        public event EventHandler<ProgressChangedEventArgs> FileOrProgressChanged = delegate { };

        public event EventHandler VersionLoaderChanged = delegate { };

        public event EventHandler<VersionsRefreshedEventArgs> VersionsRefreshed = delegate { };

        public event EventHandler LogsUpdated = delegate { };

        public event PropertyChangedEventHandler? PropertyChanged;

        internal void Set<T>(ref T obj, T value, string name = null)
        {
            obj = value;
            InvokePropertyChanged(name);
        }

        public void InvokePropertyChanged(string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private bool offlineloader = false;

        private bool _UIState;
        public bool UIState
        {
            get => !GameRuns && _UIState;
            set => Set(ref _UIState, value,nameof(UIState));
        }

        private bool _GameRuns;
        public bool GameRuns
        {
            get => _GameRuns;
            set => Set(ref _GameRuns, value, nameof(UIState));
        }

        /// <summary>
        /// Checks whether the launcher is in Offline mode
        /// </summary>
        public bool UseOfflineLoader { get { return offlineloader; } private set { offlineloader = value; VersionLoaderChanged(this, new EventArgs()); } }

        /// <summary>
        /// The stirng list of Available Minecraft versions
        /// </summary>
        public List<string> MCVerNames
        {
            get => MCVersions != null ? MCVersions.Select(x => x.Name).ToList() : new();
        }

        /// <summary>
        /// The stirng list of Available FabricMC versions
        /// </summary>
        public List<string> FabricMCVerNames
        {
            get => FabricMCVersions != null ? FabricMCVersions.Select(x => x.Name).ToList() : new();
        }

        /// <summary>
        /// The stirng list of Available Optifine versions
        /// </summary>
        public List<string> OptiFineMCVerNames
        {
            get => OptifineMCVersions != null ? OptifineMCVersions.Select(x => x.ToFullVersion()).ToList() : new();
        }

        public MVersionCollection MCVersions { get; private set; }

        public MVersionCollection FabricMCVersions { get; private set; }

        public List<OptifineDownloadVersionModel> OptifineMCVersions { get; private set; }

        public CMLauncher Launcher { get; private set; }

        public GlacierClient GlacierClient { get; set; }

        public Optifine Optifine { get; private set; }

        public Labrinth Labrinth { get; private set; }

        public NewsHelper News { get; private set; } = new();

        public Emerald()
        {
            GlacierClient = new GlacierClient();
            GlacierClient.StatusChanged += GlacierClient_StatusChanged;
            GlacierClient.ProgressChanged += GlacierClient_ProgressChanged;
            GlacierClient.UIChangedReqested += GlacierClient_UIChangedReqested;
        }

        /// <summary>
        /// Creates a Minecraft <see cref="System.Diagnostics.Process"/> using the given <paramref name="ver"/> and <paramref name="launchOption"/>(s)
        /// </summary>
        public async Task<System.Diagnostics.Process?> CreateProcessAsync(string ver, MLaunchOption launchOption, bool createTask = true, bool SkipAssetsCheck = false, bool SkipHashCheck = false)
        {
            if (UseOfflineLoader)
                SkipAssetsCheck = SkipHashCheck = true;

            Launcher.GameFileCheckers.AssetFileChecker = SkipAssetsCheck ? null : new();

            if (Launcher.GameFileCheckers.AssetFileChecker != null)
                Launcher.GameFileCheckers.AssetFileChecker.CheckHash = !SkipHashCheck;

            if (Launcher.GameFileCheckers.ClientFileChecker != null)
                Launcher.GameFileCheckers.ClientFileChecker.CheckHash = !SkipHashCheck;

            if (Launcher.GameFileCheckers.LibraryFileChecker != null)
                Launcher.GameFileCheckers.LibraryFileChecker.CheckHash = !SkipHashCheck;

            var id = createTask ? TasksHelper.AddProgressTask(Localized.LaunchMC) : int.MaxValue;
            int prog = 0;
            string message = "";

            void ProgChange(object sender, System.ComponentModel.ProgressChangedEventArgs e)
            {
                prog = e.ProgressPercentage;
                if (createTask)
                    TasksHelper.EditProgressTask(id, prog, message: message);
            };

            void FileChange(DownloadFileChangedEventArgs e)
            {
                message = $"{e.FileKind} : {e.FileName} ({e.ProgressedFileCount}/{e.TotalFileCount})";

                if (createTask)
                    TasksHelper.EditProgressTask(id, prog, message: message);
            };

            try
            {
                Launcher.ProgressChanged += ProgChange;
                Launcher.FileChanged += FileChange;

                var p = await Launcher.CreateProcessAsync(ver, launchOption);

                Launcher.ProgressChanged -= ProgChange;
                Launcher.FileChanged -= FileChange;

                if (createTask)
                    TasksHelper.CompleteTask(id, true);

                return p;
            }
            catch (Exception ex)
            {
                Launcher.ProgressChanged -= ProgChange;
                Launcher.FileChanged -= FileChange;

                if (createTask)
                    TasksHelper.CompleteTask(id, false, ex.Message);

                return null;
            }
        }

        private void Labrinth_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            FileOrProgressChanged(sender, e);
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
                    return item.FirstOrDefault() ?? "";
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

        public async Task<bool> ConfigureOptifine(OptifineDownloadVersionModel model)
        {
            double makePrcent(double value, double CurrentMax, double NextMax) => value * NextMax / CurrentMax;

            if (UseOfflineLoader)
                return MCVerNames.Contains(model.ToFullVersion());

            var taskID = TasksHelper.AddProgressTask(Localized.ConfigureOptifine, message: Localized.GettingInheritedVersion.ToString());
            string msg = "";
            int prog = 0;

            if (!MCVerNames.Contains(model.ToFullVersion()))
            {
                void ProgChange(object? sender, System.ComponentModel.ProgressChangedEventArgs e)
                {
                    msg = Localized.GettingInheritedVersion.ToString();
                    prog = (int)Math.Round(makePrcent(e.ProgressPercentage, 100, 35));
                    TasksHelper.EditProgressTask(taskID, prog, message: msg);
                }

                Launcher.ProgressChanged += ProgChange;
                var VMCp = await CreateProcessAsync(model.McVersion, new(), false);
                TasksHelper.EditProgressTask(taskID, 35, message: msg);

                if (VMCp != null)
                {
                    void InstallerProgChange(object? sender, int e)
                    {
                        msg = Localized.GettingOptifine.ToString();
                        prog = 35 + (int)Math.Round(makePrcent(e, 100, 65));
                        TasksHelper.EditProgressTask(taskID, prog, message: msg);
                    }

                    Optifine.ProgressChanged += InstallerProgChange;

                    var r = await Optifine.Save(model);
                    if (!r.Item1)
                    {
                        TasksHelper.CompleteTask(taskID, false, r.Item2);
                        return false;
                    }

                    TasksHelper.CompleteTask(taskID, true, r.Item2);

                    await RefreshVersions(false);

                    return true;
                }
                else
                {
                    TasksHelper.CompleteTask(taskID, false, "");
                    return false;
                }
            }
            else
            {
                TasksHelper.CompleteTask(taskID, true, "");
                return true;
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
            get => string.IsNullOrEmpty(changeLogsHTMLBody) ? "" : changeLogsHTMLBody;
            set
            {
                changeLogsHTMLBody = value;
                LogsUpdated(this, new EventArgs());
            }
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
                if (Launcher.Versions?.LatestSnapshotVersion?.Name != Launcher.Versions?.LatestReleaseVersion?.Name)
                {
                    html += await GetChangelog(Launcher.Versions?.LatestSnapshotVersion?.Name);
                    UpdateLogs(html);
                }
                if (Launcher.Versions?.LatestReleaseVersion?.Name != "1.19.1")
                {
                    html += await GetChangelog(Launcher.Versions?.LatestReleaseVersion?.Name);
                    UpdateLogs(html);
                }
            }
            catch
            {
            }

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
                TasksHelper.CompleteTask(taskID, false, ex.Message);
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

            // Get changelog informations
            Changelogs changelogs = await Changelogs.GetChangelogs();

            // Get all available versions
            string[] versions = changelogs.GetAvailableVersions();

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
            UIState = ui;
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
            var taskID = TasksHelper.AddTask(Localized.InitializeCore);
            UI(false);
            UseOfflineLoader = false;
            Launcher = new CMLauncher(path);
            Optifine = new(path);
            Labrinth = new(path);
            UI(true);
            TasksHelper.CompleteTask(taskID);
        }

        /// <summary>
        /// Refreshes the available Minecraft/Fabric Versions
        /// </summary>
        /// <returns>
        /// <see cref="true"/> if succeed,
        /// <see cref="false"/> if failed
        /// </returns>
        public async Task<bool> RefreshVersions(bool UIchange = true)
        {
            if (UIchange)
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
                    OptifineMCVersions = await Optifine.GetOptifineVersions();
                }

                Status(Localized.Ready);
                VersionsRefreshed(this, new VersionsRefreshedEventArgs(true));
                TasksHelper.CompleteTask(taskID, true);

                if (UIchange)
                    UI(true);

                return true;
            }
            catch (Exception ex)
            {
                TasksHelper.CompleteTask(taskID, false, ex.Message);
                Status(Localized.Ready);
                if (UIchange)
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

        /// <summary>
        /// Checks fabric whether it exists if not install it
        /// </summary>
        public async Task<bool> InitializeFabric(string mcver, string fullVer)
        {
            int taskID = TasksHelper.AddTask($"{Localized.GetFabric} " + mcver);
            bool exists = false;

            foreach (var veritem in FabricMCVersions)
            {
                if (veritem.Name == fullVer)
                {
                    exists = true;
                }
            }

            if (exists)
            {
                UI(false);

                var fabric = FabricMCVersions.GetVersionMetadata(fullVer);
                await fabric.SaveAsync(Launcher.MinecraftPath);

                await RefreshVersions();

                UI(true);

                TasksHelper.CompleteTask(taskID, true);

                return true;
            }
            else
            {
                TasksHelper.CompleteTask(taskID, false);
                return false;
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
                LaunchVer = launchver;
                DisplayVer = displayver;
                Response = response;
            }
        }
    }
}
