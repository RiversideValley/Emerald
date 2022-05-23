using SDLauncher_UWP.Resources;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking.BackgroundTransfer;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace SDLauncher_UWP.Helpers
{
    public class OptiFine
    {
        public event EventHandler StatusChanged = delegate { };
        public event EventHandler ProgressChanged = delegate { };
        public event EventHandler ErrorAppeared = delegate { };
        public event EventHandler UIChangedReqested = delegate { };
        public event EventHandler DownloadCompleted = delegate { };

        public OptFineVerReturns returns;
        string optver;
        public bool UI;
        public int DownloadProg;
        public string DownloadStats;
        public async Task<bool> IsOptiFineFilePresent(string lastFileName, string mcVer, bool isLib)
        {
            if (!isLib)
            {
                try
                {
                    var verFolder = await StorageFolder.GetFolderFromPathAsync(vars.Launcher.Launcher.MinecraftPath.Versions);
                    var mcVerFolder = await verFolder.GetFolderAsync(mcVer);
                    var file = await mcVerFolder.GetFileAsync(lastFileName);
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }
            else
            {
                try
                {
                    var LibsFolder = await StorageFolder.GetFolderFromPathAsync(vars.Launcher.Launcher.MinecraftPath.Library);
                    var LibFolder = await LibsFolder.GetFolderAsync("optifine");
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        public void DownloadOptiFineVer(string mcver, string modVer, string DisplayVer)
        {
            MCver = mcver;
            Modver = modVer;
            Displayver = DisplayVer;
            UIChangedReqested(false, new EventArgs());
            switch (mcver)
            {
                case "1.18.2":
                    returns = new OptFineVerReturns(modVer, DisplayVer, OptFineVerReturns.Results.DownloadOptiFineVer);
                    optver = ": " + mcver;
                    OptFineDownload("https://raw.githubusercontent.com/Chaniru22/SDLauncher/main/OptiFine-1.18.2.zip", "OptiFine-" + mcver + ".zip", ModType.ver);
                    break;
                case "1.18.1":
                    returns = new OptFineVerReturns(modVer, DisplayVer, OptFineVerReturns.Results.DownloadOptiFineVer);
                    optver = ": " + mcver;
                    OptFineDownload("https://raw.githubusercontent.com/Chaniru22/SDLauncher/main/OptiFine-1.18.1.zip", "OptiFine-" + mcver + ".zip", ModType.ver);
                    break;
                case "1.17.1":
                    returns = new OptFineVerReturns(modVer, DisplayVer, OptFineVerReturns.Results.DownloadOptiFineVer);
                    optver = ": " + mcver;
                    OptFineDownload("https://raw.githubusercontent.com/Chaniru22/SDLauncher/main/OptiFine-1.17.1.zip", "OptiFine-" + mcver + ".zip", ModType.ver);
                    break;
                case "1.16.5":
                    returns = new OptFineVerReturns(modVer, DisplayVer, OptFineVerReturns.Results.DownloadOptiFineVer);
                    optver = ": " + mcver;
                    OptFineDownload("https://raw.githubusercontent.com/Chaniru22/SDLauncher/main/OptiFine-1.16.5.zip", "OptiFine-" + mcver + ".zip", ModType.ver);
                    break;
            }
        }
        public string MCver { get; set; }
        public string Modver { get; set; }
        public string Displayver { get; set; }
        public async Task<OptFineVerReturns> CheckOptiFine(string mcver, string modVer, string DisplayVer)
        {
            await vars.Launcher.RefreshVersions();
            UIChangedReqested(false, new EventArgs());
            bool exists = false;
            if (vars.Launcher.MCVersions != null)
            {
                foreach (var veritem in vars.Launcher.MCVersions)
                {
                    if (veritem.Name == modVer)
                    {
                        exists = true;
                    }
                }
                if (exists)
                {
                    returns = new OptFineVerReturns(modVer, DisplayVer, OptFineVerReturns.Results.Exists);
                    UIChangedReqested(true, new EventArgs());
                    return returns;
                }
                else
                {
                    var r = await MessageBox.Show("Error", "Couldn't find OptiFine installed on this minecraft. Do you want to download and install from our servers ?", MessageBoxButtons.YesNo);
                  
                    if (r == MessageBoxResults.Yes)
                    {
                        if (await IsOptiFineFilePresent(mcver + ".jar", mcver, false))
                        {
                            if (await IsOptiFineFilePresent(null, null, true))
                            {
                                returns = new OptFineVerReturns(null, null, OptFineVerReturns.Results.DownloadOptiFineVer);
                                UIChangedReqested(true, new EventArgs());
                                return returns;
                            }
                            else
                            {
                                await MessageBox.Show("Information", "This will download main OptiFine library, Please click again " + DisplayVer + " (after download and extract the main OptiFine) to install optifine of that version !", MessageBoxButtons.Ok);
                                optver = " Lib";
                                returns = new OptFineVerReturns(modVer, mcver, OptFineVerReturns.Results.DownloadOptiFineLib);
                                OptFineDownload("https://raw.githubusercontent.com/Chaniru22/SDLauncher/main/optifine.zip", "OptiFine.zip", ModType.lib);
                                UIChangedReqested(true, new EventArgs());
                                return returns;
                            }
                        }
                        else
                        {
                            await MessageBox.Show("Error", "You have to install & run minecraft version " + mcver + " one time to install OptiFine", MessageBoxButtons.Ok);
                            returns = new OptFineVerReturns(mcver, mcver, OptFineVerReturns.Results.DownloadMCVer);
                            UIChangedReqested(true, new EventArgs());
                            return returns;
                        }
                    }
                }
                UIChangedReqested(true, new EventArgs());
                return returns;
            }
            else
            {
                returns = new OptFineVerReturns(null, null, OptFineVerReturns.Results.Failed);
                UIChangedReqested(true, new EventArgs());
                return returns;
            }
        }
        //

        private enum ModType
        {
            lib,
            ver
        }
        string optDir;
        ModType dwnOptiType;
        private async void OptFineDownload(string link, string dir, ModType m)
        {
            try
            {
                Uri source = new Uri(link.Trim());
                string destination = dir.Trim();

                StorageFile destinationFile = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(
                    destination,
                    CreationCollisionOption.ReplaceExisting);

                BackgroundDownloader downloader = new BackgroundDownloader();
                DownloadOperation download = downloader.CreateDownload(source, destinationFile);
                StartDownloadWithProgress(download);
            }
            catch
            {

            }
            optDir = dir;
            dwnOptiType = m;
            UIChangedReqested(false, new EventArgs());
        }
        DownloadOperation operation;
        DispatcherTimer downloadprog = new DispatcherTimer();
        private async void StartDownloadWithProgress(DownloadOperation operation)
        {
            StatusChanged("Downloading: OptiFine" + optver, new EventArgs());
            this.operation = operation;
            await this.operation.StartAsync();
            downloadprog.Interval = new TimeSpan(0, 0, 0, 0, 1);
            downloadprog.Tick += Downloadprog_Tick;
            downloadprog.Start();
        }


        private void Downloadprog_Tick(object sender, object e)
        {
            if (operation.Progress.Status == BackgroundTransferStatus.Completed)
            {
                DownloadFileCompleted();
                downloadprog.Stop();
                ProgressChanged(0, new EventArgs());
            }
            else if (operation.Progress.Status == BackgroundTransferStatus.Running)
            {
                try
                {
                    double bytesIn = operation.Progress.BytesReceived;
                    double totalBytes = operation.Progress.TotalBytesToReceive;
                    double percentage = bytesIn / totalBytes * 100;
                    ProgressChanged(int.Parse(Math.Floor(percentage).ToString()), new EventArgs());
                }
                catch { }
                StatusChanged("Downloading: OptiFine" + optver, new EventArgs());
            }
            else if (operation.Progress.Status == BackgroundTransferStatus.Error)
            {
                ErrorAppeared("Failed to download the file",new EventArgs());
                DownloadCompleted(false, new EventArgs());
                downloadprog.Stop();
            }
        }

        private async void DownloadFileCompleted()
        {
            UIChangedReqested(false, new EventArgs());
            StatusChanged("Extracting", new EventArgs());

            //Read the file stream
            var a = await ApplicationData.Current.TemporaryFolder.GetFileAsync(optDir);
            Stream b = await a.OpenStreamForReadAsync();
            //unzip
            ZipArchive archive = new ZipArchive(b);
            if (dwnOptiType == ModType.lib)
            {
                archive.ExtractToDirectory(vars.Launcher.Launcher.MinecraftPath.Library, true);
            }
            else if (dwnOptiType == ModType.ver)
            {

                archive.ExtractToDirectory(vars.Launcher.Launcher.MinecraftPath.Versions, true);
            }
            ProgressChanged(100, new EventArgs());
            StatusChanged(Localized.Ready, new EventArgs());
            UIChangedReqested(true, new EventArgs());
            DownloadCompleted(true, new EventArgs());
        }
        

    }
    public class OptFineVerReturns
    {
        public enum Results
        {
            Failed,
            DownloadOptiFineLib,
            DownloadOptiFineVer,
            DownloadMCVer,
            Exists
        }
        public string LaunchVer { get; set; }
        public string btnVer { get; set; }
        public Results Result { get; set; }
        public OptFineVerReturns(string launchver, string btnver, Results result)
        {
            LaunchVer = launchver;
            btnVer = btnver;
            Result = result;
        }
    }
}
