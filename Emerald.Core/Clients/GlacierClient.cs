using Emerald.Core.Args;
using Emerald.Core.Tasks;
using System.IO.Compression;

namespace Emerald.Core.Clients
{
    public class GlacierClient
    {
        public event EventHandler<ProgressChangedEventArgs> ProgressChanged = delegate { };

        public event EventHandler StatusChanged = delegate { };

        public event EventHandler DownloadCompleted = delegate { };

        public event EventHandler UIChangedReqested = delegate { };

        public bool ClientExists()
        {
            return Util.FolderExists(MainCore.Launcher.Launcher.MinecraftPath.Versions + "/Glacier Client");
        }

        public async void DownloadClient()
        {
            var ver = await Util.DownloadText("https://www.slashonline.net/glacier/c.txt");
            if (ver != MainCore.GlacierClientVersion)
            {
                int taskID = TasksHelper.AddTask("Download Glacier Client");
                UIChangedReqested(false, new EventArgs());

                try
                {
                    using var client = new FileDownloader("https://slashonline.net/glacier/get/release/Glacier.zip", MainCore.Launcher.Launcher.MinecraftPath.Versions + "/Glacier.zip");
                    client.ProgressChanged += (totalFileSize, totalBytesDownloaded, progressPercentage) =>
                    {
                        StatusChanged("Downloading Glacier Client", new EventArgs());
                        try
                        {
                            ProgressChanged(this, new ProgressChangedEventArgs(currentProg: Convert.ToInt32(progressPercentage), maxfiles: 100, currentfile: Convert.ToInt32(progressPercentage)));
                        }
                        catch
                        {
                        }

                        if (progressPercentage == 100)
                        {
                            StatusChanged("Ready", new EventArgs());
                            Extract();
                            client.Dispose();
                            MainCore.GlacierClientVersion = ver;
                            ProgressChanged(this, new ProgressChangedEventArgs(currentProg: 0, maxfiles: 100, currentfile: 00));
                            TasksHelper.CompleteTask(taskID, true);
                        }
                    };

                    await client.StartDownload();
                }
                catch
                {
                    TasksHelper.CompleteTask(taskID, false);
                }
            }
        }

        private async void Extract()
        {
            int TaskID = TasksHelper.AddTask("Extract Glacier Client");
            UIChangedReqested(false, new EventArgs());
            StatusChanged("Extracting Glacier Client", new EventArgs());

            // Read the file stream
            Stream b = File.OpenRead(MainCore.Launcher.Launcher.MinecraftPath.Versions + "/Glacier.zip");

            // Unzip
            ZipArchive archive = new ZipArchive(b);
            archive.ExtractToDirectory(Directory.CreateDirectory(MainCore.Launcher.Launcher.MinecraftPath.Versions + "/Glacier Client").FullName);

            ProgressChanged(this, new ProgressChangedEventArgs(currentfile: 0));
            StatusChanged("Ready", new EventArgs());
            UIChangedReqested(true, new EventArgs());
            DownloadCompleted(true, new EventArgs());

            TasksHelper.CompleteTask(TaskID, true);

            MainCore.GlacierClientVersion = await Util.DownloadText("https://www.slashonline.net/glacier/c.txt");
        }
    }
}
