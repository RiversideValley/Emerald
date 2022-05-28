using CmlLib.Core;
using CmlLib.Core.Downloader;
using CmlLib.Core.Installer.FabricMC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CmlLib.Core.Version;
using SDLauncher_UWP.Resources;
using CmlLib.Utils;

namespace SDLauncher_UWP.Helpers
{
    public class SDLauncher
    {
        public event EventHandler<UIChangeRequestedEventArgs> UIChangeRequested = delegate { };
        public event EventHandler<StatusChangedEventArgs> StatusChanged = delegate { };
        public event EventHandler<ProgressChangedEventArgs> FileOrProgressChanged = delegate { };
        public event EventHandler VersionsRefreshed = delegate { };

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
        public MVersionCollection MCVersions { get; private set; }
        public MVersionCollection FabricMCVersions { get; private set; }

        public CMLauncher Launcher { get; set; }
        public OptiFine OptiFine { get; set; }
        public GlacierClient GlacierClient { get; set; }
        public SDLauncher()
        {
            OptiFine = new OptiFine();
            OptiFine.ProgressChanged += OptiFine_ProgressChanged;
            OptiFine.StatusChanged += OptiFine_StatusChanged;
            OptiFine.UIChangedReqested += OptiFine_UIChangedReqested;
            OptiFine.ErrorAppeared += OptiFine_ErrorAppeared;

            GlacierClient = new GlacierClient();
            GlacierClient.StatusChanged += GlacierClient_StatusChanged;
            GlacierClient.ProgressChanged += GlacierClient_ProgressChanged; ;
            GlacierClient.UIChangedReqested += GlacierClient_UIChangedReqested;
        }
        public async Task<string> GetChangelog(string version)
        {
            Status("Loading changelog v:" + version);
            Changelogs changelogs = await Changelogs.GetChangelogs(); // get changelog informations
            string[] versions = changelogs.GetAvailableVersions(); // get all available versions
            var changelogHtml = await changelogs.GetChangelogHtml(version);

            var fullbody = "<style>\np,h1,li,span,body,html {\nfont-family:\"Segoe UI\";\n}\n</style>\n" + "<h1>Version " + version + "</h1>" + changelogHtml;
            Status("Ready");
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
        private void OptiFine_ErrorAppeared(object sender, EventArgs e)
        {
           _ = MessageBox.Show("Error", sender.ToString(), MessageBoxButtons.Ok);
        }

        private void OptiFine_UIChangedReqested(object sender, EventArgs e)
        {
            UIChangeRequested(this, new UIChangeRequestedEventArgs((bool)sender));
        }

        private void OptiFine_StatusChanged(object sender, EventArgs e)
        {
            StatusChanged(this, new StatusChangedEventArgs(sender.ToString()));
        }

        private void OptiFine_ProgressChanged(object sender, EventArgs e)
        {
            FileOrProgressChanged(this, new ProgressChangedEventArgs(currentProg: int.Parse(sender.ToString())));
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
            Status(Localized.GettingVers);
            MCVersions = await Launcher.GetAllVersionsAsync();
            var fabricVersionLoader = new FabricVersionLoader();
            FabricMCVersions = await fabricVersionLoader.GetVersionMetadatasAsync();
            Status(Localized.Ready);
            VersionsRefreshed(this, new EventArgs());
            UI(true);
        }


        private void Launcher_ProgressChanged(object sender, System.ComponentModel.ProgressChangedEventArgs e)
        {
            FileOrProgressChanged(this, new ProgressChangedEventArgs(currentProg: e.ProgressPercentage));
        }

        private void Launcher_FileChanged(DownloadFileChangedEventArgs e)
        {
            Status($"{e.FileKind} : {e.FileName} ({e.ProgressedFileCount}/{e.TotalFileCount})");
            FileOrProgressChanged(this, new ProgressChangedEventArgs(maxfiles: e.ProgressedFileCount, currentfile: e.TotalFileCount));
        }

        public async Task<FabricResponsoe> CheckFabric(string mcver, string modver, string displayver)
        {
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
                    Status("Ready");
                    await RefreshVersions();
                    launchVer = modver;
                    displayVer = displayver;
               Status("Ready");
                return new FabricResponsoe(launchVer, displayVer, FabricResponsoe.Responses.ExistsOrCreated);
            }
            else
            {
                if (await MessageBox.Show("Error", "To run " + displayver + " you need to have installed version " + mcver + ". Vanilla,Do you want to install now ?", MessageBoxButtons.YesNo) == MessageBoxResults.Yes)
                {
                    displayVer = mcver;
                    launchVer = mcver;
                    return new FabricResponsoe(launchVer, displayVer, FabricResponsoe.Responses.NeedMojangVer);
                }
                else
                {
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
            public ProgressChangedEventArgs(int? currentfile = null, int? maxfiles = null, int? currentProg = null)
            {
                MaxFiles = maxfiles;
                CurrentFile = currentfile;
                ProgressPercentage = currentProg;
            }
        }
    }
}