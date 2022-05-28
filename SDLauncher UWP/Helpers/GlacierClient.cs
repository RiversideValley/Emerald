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

namespace SDLauncher_UWP.Helpers
{
    public class GlacierClient
    {
        public event EventHandler<SDLauncher.ProgressChangedEventArgs> ProgressChanged = delegate { };
        public event EventHandler StatusChanged = delegate { };
        public event EventHandler DownloadCompleted = delegate { };
        public event EventHandler UIChangedReqested = delegate { };
        public async Task<bool> ClientExists()
        {
            try
            {
                var verFolder = await StorageFolder.GetFolderFromPathAsync(vars.Launcher.Launcher.MinecraftPath.Versions);
                var mcVerFolder = await verFolder.GetFolderAsync("Glacier Client");
                return true;
            }
            catch
            {
                return false;
            }
        }
        public async void DownloadClient()
        {
            UIChangedReqested(false, new EventArgs());
            try
            {
                Uri source = new Uri("https://slashonline.net/glacier/get/release/Glacier.zip".Trim());
                string destination = "Glacier.zip";

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
        }
        DownloadOperation operation;
        DispatcherTimer downloadprog = new DispatcherTimer();
        private async void StartDownloadWithProgress(DownloadOperation operation)
        {
            StatusChanged("Downloading Glacier Client", new EventArgs());
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
                ProgressChanged(0, new SDLauncher.ProgressChangedEventArgs(currentfile:0));
            }
            else if (operation.Progress.Status == BackgroundTransferStatus.Running)
            {
                try
                {
                    double bytesIn = operation.Progress.BytesReceived;
                    double totalBytes = operation.Progress.TotalBytesToReceive;
                    ProgressChanged(this, new SDLauncher.ProgressChangedEventArgs(currentfile: unchecked((int)bytesIn), maxfiles: unchecked((int)totalBytes)));
                }
                catch { }
                StatusChanged("Downloading Glacier Client", new EventArgs());
            }
            else if (operation.Progress.Status == BackgroundTransferStatus.Error)
            {
                DownloadCompleted(false, new EventArgs());
                downloadprog.Stop();
            }
        }

        private async void DownloadFileCompleted()
        {
            UIChangedReqested(false, new EventArgs());
            StatusChanged("Extracting", new EventArgs());

            //Read the file stream
            var wf = await StorageFolder.GetFolderFromPathAsync(vars.Launcher.Launcher.MinecraftPath.Versions);
            var f =  await wf.CreateFolderAsync("Glacier Client", CreationCollisionOption.ReplaceExisting);
            var a = await ApplicationData.Current.TemporaryFolder.GetFileAsync("Glacier.zip");
            Stream b = await a.OpenStreamForReadAsync();
            //unzip
            ZipArchive archive = new ZipArchive(b);
                archive.ExtractToDirectory(f.Path, true);
            
            ProgressChanged(this, new SDLauncher.ProgressChangedEventArgs(currentfile:0));
            StatusChanged(Localized.Ready, new EventArgs());
            UIChangedReqested(true, new EventArgs());
            DownloadCompleted(true, new EventArgs());
        }

    }
}
