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
            var ver = await Util.DownloadText("https://www.slashonline.net/glacier/c.txt");
            if (ver != vars.GlacierClientVersion)
            {
                int taskID = LittleHelp.AddTask("Download Glacier Client");
                UIChangedReqested(false, new EventArgs());
                try
                {
                    StorageFile destinationFile = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(
                        "Glacier.zip",
                        CreationCollisionOption.ReplaceExisting);
                    using (var client = new HttpClientDownloadWithProgress("https://slashonline.net/glacier/get/release/Glacier.zip", destinationFile.Path))
                    {
                        client.ProgressChanged += (totalFileSize, totalBytesDownloaded, progressPercentage) =>
                        {
                            StatusChanged("Downloading Glacier Client", new EventArgs());
                            try
                            {
                                this.ProgressChanged(this, new SDLauncher.ProgressChangedEventArgs(currentProg: Convert.ToInt32(progressPercentage), maxfiles: 100, currentfile: Convert.ToInt32(progressPercentage)));
                            }
                            catch { }
                            if (progressPercentage == 100)
                            {
                                StatusChanged("Ready", new EventArgs());
                                this.Extract();
                                client.Dispose();
                                vars.GlacierClientVersion = ver;
                                this.ProgressChanged(this, new SDLauncher.ProgressChangedEventArgs(currentProg: 0, maxfiles: 100, currentfile: 00));
                                LittleHelp.CompleteTask(taskID, true);
                            }
                        };

                        await client.StartDownload();
                    }
                }
                catch
                {
                    LittleHelp.CompleteTask(taskID, false);
                }
            }
        }


            private async void Extract()
        {
            int TaskID = LittleHelp.AddTask("Extract Glacier Client");
            UIChangedReqested(false, new EventArgs());
            StatusChanged("Extracting Glacier Client", new EventArgs());

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
            LittleHelp.CompleteTask(TaskID, true);
            vars.GlacierClientVersion = await Util.DownloadText("https://www.slashonline.net/glacier/c.txt");
        }

    }
}
