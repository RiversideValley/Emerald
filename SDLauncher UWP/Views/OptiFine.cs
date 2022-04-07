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

namespace SDLauncher_UWP.Views
{
    class OptiFine
    {
        public OptFineVerReturns returns;
        string optver;
        public bool UI;
        public int DownloadProg;
        public string DownloadStats;
        private async Task<bool> IsOptiFineFilePresent(string lastFileName, string mcVer, bool isLib)
        {
            if (!isLib)
            {
                try
                {
                    var verFolder = await ApplicationData.Current.LocalFolder.GetFolderAsync("versions");
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
                    var LibsFolder = await ApplicationData.Current.LocalFolder.GetFolderAsync("libraries");
                    var LibFolder = await LibsFolder.GetFolderAsync("optifine");
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }
        public void DownloadOptiFineVer(string mcver, string modVer, MenuFlyoutItem mit)
        {
            switch (mcver)
            {
                case "1.18.2":
                    returns = new OptFineVerReturns(modVer, mit.Text.ToString(), OptFineVerReturns.Results.DownloadOptiFineVer);
                    UI = false;
                    optver = ": " + mcver;
                    OptFineDownload("https://raw.githubusercontent.com/Chaniru22/SDLauncher/main/OptiFine-1.18.2.zip", "OptiFine-" + mcver + ".zip", ModType.ver);
                    break;
                case "1.18.1":
                    returns = new OptFineVerReturns(modVer, mit.Text.ToString(), OptFineVerReturns.Results.DownloadOptiFineVer);
                    UI = false;
                    optver = ": " + mcver;
                    OptFineDownload("https://raw.githubusercontent.com/Chaniru22/SDLauncher/main/OptiFine-1.18.1.zip", "OptiFine-" + mcver + ".zip", ModType.ver);
                    break;
                case "1.17.1":
                    returns = new OptFineVerReturns(modVer, mit.Text.ToString(), OptFineVerReturns.Results.DownloadOptiFineVer);
                    UI = false;
                    optver = ": " + mcver;
                    OptFineDownload("https://raw.githubusercontent.com/Chaniru22/SDLauncher/main/OptiFine-1.17.1.zip", "OptiFine-" + mcver + ".zip", ModType.ver);
                    break;
                case "1.16.5":
                    returns = new OptFineVerReturns(modVer, mit.Text.ToString(), OptFineVerReturns.Results.DownloadOptiFineVer);
                    UI = false;
                    optver = ": " + mcver;
                    OptFineDownload("https://raw.githubusercontent.com/Chaniru22/SDLauncher/main/OptiFine-1.16.5.zip", "OptiFine-" + mcver + ".zip", ModType.ver);
                    break;

            }
        }
        public async Task CheckOptiFine(string mcver, string modVer, MenuFlyoutItem mit, CmlLib.Core.Version.MVersionCollection mcVers)
        {
            MessageBoxEx msgbx;
            bool exists = false;
            if (mcVers != null)
            {
                foreach (var veritem in mcVers)
                {
                    if (veritem.Name == modVer)
                    {
                        exists = true;
                    }
                }
                if (exists)
                {
                    returns = new OptFineVerReturns(modVer, mit.Text.ToString(), OptFineVerReturns.Results.Exists);
                }
                else
                {
                    msgbx = new MessageBoxEx("Error", "Couldn't find OptiFine installed on this minecraft. Do you want to download and install from our servers ?", MessageBoxEx.Buttons.YesNo);
                    await msgbx.ShowAsync();
                    if (msgbx.Result == MessageBoxEx.Results.Yes)
                    {
                        if (await IsOptiFineFilePresent(mcver + ".jar", mcver, false))
                        {
                            if (await IsOptiFineFilePresent(null, null, true))
                            {
                                returns = new OptFineVerReturns(null, null, OptFineVerReturns.Results.DownloadOptiFineVer);
                            }
                            else
                            {
                                msgbx = new MessageBoxEx("Error", "This will download main OptiFine library, Please click again " + mit.Text.ToString() + " (after download and extract the main OptiFine) to install optifine of that version !", MessageBoxEx.Buttons.Ok);
                                await msgbx.ShowAsync();
                                optver = " Lib";
                                returns = new OptFineVerReturns(modVer, mcver, OptFineVerReturns.Results.DownloadOptiFineLib);
                                OptFineDownload("https://raw.githubusercontent.com/Chaniru22/SDLauncher/main/optifine.zip", "OptiFine.zip", ModType.lib);
                            }
                        }
                        else
                        {
                            msgbx = new MessageBoxEx("Error", "You have to install & run minecraft version " + mcver + " one time to install OptiFine", MessageBoxEx.Buttons.Ok);
                            await msgbx.ShowAsync();
                            returns = new OptFineVerReturns(modVer, mcver, OptFineVerReturns.Results.DownloadMCVer);
                        }
                    }
                }
            }
            else
            {
                returns = new OptFineVerReturns(modVer, mcver, OptFineVerReturns.Results.Failed);
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
                    destination, CreationCollisionOption.GenerateUniqueName);

                BackgroundDownloader downloader = new BackgroundDownloader();
                DownloadOperation download = downloader.CreateDownload(source, destinationFile);
                StartDownloadWithProgress(download);
            }
            catch
            {

            }
            optDir = dir;
            dwnOptiType = m;
            UI = false;
        }
        DownloadOperation operation;
        void StartDownloadWithProgress(DownloadOperation obj)
        {
            operation = obj;
            operation.StartAsync();
            DispatcherTimer downloadprog = new DispatcherTimer();
            downloadprog.Interval = new TimeSpan(0, 0, 0, 0, 1);
            downloadprog.Tick += Downloadprog_Tick;

        }

        private void Downloadprog_Tick(object sender, object e)
        {
            double bytesIn = double.Parse(operation.Progress.BytesReceived.ToString());
            double totalBytes = double.Parse(operation.Progress.TotalBytesToReceive.ToString());
            double percentage = bytesIn / totalBytes * 100;
            DownloadStats = "Downloading: OptiFine" + optver;
            DownloadProg = int.Parse(Math.Truncate(percentage).ToString());
            if (DownloadProg > 99)
            {
                client_DownloadFileCompleted();
            }
        }

        async void client_DownloadFileCompleted()
        {
            DownloadStats = "Extracting";

            //Read the file stream
            var a = await ApplicationData.Current.TemporaryFolder.GetFileAsync(optDir);
            Stream b = await a.OpenStreamForReadAsync();
            //unzip
            ZipArchive archive = new ZipArchive(b);
            if (dwnOptiType == ModType.lib)
            {
                archive.ExtractToDirectory(Path.Combine(ApplicationData.Current.LocalFolder.Path,"libraries"), true);
            }
            else if (dwnOptiType == ModType.ver)
            {

                archive.ExtractToDirectory(Path.Combine(ApplicationData.Current.LocalFolder.Path,"versions"), true);
            }
            DownloadProg = 100;
            UI = true;
        }

    }
    class OptFineVerReturns
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
