using CmlLib.Core;
using CommunityToolkit.WinUI.Helpers;
using Emerald.Core;
using Emerald.Core.Tasks;
using Emerald.WinUI.Models;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Shapes;
using Newtonsoft.Json;
using Octokit;
using ProjBobcat.Class.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using Windows.Management.Deployment;
using Windows.Storage;
using static PInvoke.Kernel32;
using SS = Emerald.WinUI.Helpers.Settings.SettingsSystem;

namespace Emerald.WinUI.Helpers.Updater
{
    public class Updater
    {
        public Architecture Architecture => RuntimeInformation.ProcessArchitecture;

        private GitHubClient Client;
        public bool IsPrereleaseEnabled => SS.Settings.App.Updates.IncludePreReleases;
        public Updater()
        {
        }
        bool IsInitialized = false;
        public async System.Threading.Tasks.Task Initialize()
        {
            var cId = await FileIO.ReadTextAsync(await StorageFile.GetFileFromPathAsync($"{Windows.ApplicationModel.Package.Current.InstalledPath}\\GithubClientID.txt"));

            Client = new GitHubClient(new Octokit.ProductHeaderValue(cId));
            IsInitialized = true;
        }
        private bool isRunning = false;
        public async void CheckForUpdates(bool OnlyInformifHigherAvailable = false)
        {
            if (isRunning || !IsInitialized) return;

            isRunning = true;
            var id = TasksHelper.AddTask("CheckForUpdates", "ConnectingGithub");

            List<Octokit.Release> Releases;

            try
            {
                Releases = (await Client.Repository.Release.GetAll("RiversideValley", "Emerald")).ToList();
            }
            catch (Exception ex)
            {
                TasksHelper.CompleteTask(id, false, ex.Message);
               goto Return;
            }
            Octokit.Release rel = new();
            if (IsPrereleaseEnabled)
            {
                if (Releases.First().CreatedAt < Releases.First(x => x.Prerelease).CreatedAt)
                    rel = Releases.First(x => x.Prerelease);
                else
                    rel = Releases.First();
            }
            else
                rel = Releases.First(x => !x.Prerelease);

            var ver = new Version(rel.TagName.Split('@')[0].Replace("v", ""));
            var currentver = new Version(DirectResoucres.AppVersion);


            if (!rel.Assets.Any(x => x.Name.EndsWith("msixbundle") && x.Name.ToLower().Contains(this.Architecture.ToString().ToLower())))
            {
                TasksHelper.CompleteTask(id, false, "NoMsixUpdate");

                if (!OnlyInformifHigherAvailable)
                    MessageBox.Show("Error".Localize(), "NoMsixUpdate".Localize(), Enums.MessageBoxButtons.Ok);

                goto Return;
            }
            var asset = rel.Assets.First(x => x.Name.EndsWith("msixbundle") && x.Name.ToLower().Contains(this.Architecture.ToString().ToLower()));
            if (ver > currentver)
            {
                TasksHelper.CompleteTask(id, true, "UpdateAvailable");

                var msg = await MessageBox.Show("UpdateAvailable".Localize(), "## Version: " + ver.ToString() + "\n\n###ReleaseNotes".Localize() + "\n\n " + rel.Body,Enums.MessageBoxButtons.CustomWithCancel, "UpdateNow".Localize());
                if(msg == Enums.MessageBoxResults.Cancel)
                   goto Return;

            }
            else if(ver < currentver)
            {
                TasksHelper.CompleteTask(id, true, "DowngradeAvailable");

                if(OnlyInformifHigherAvailable)
                    goto Return;

                var msg = await MessageBox.Show("DowngradeAvailable".Localize(), "DowngradeDescription".Localize(),Enums.MessageBoxButtons.Ok);
                   goto Return;


            }
            else if(ver == currentver)
            {
                TasksHelper.CompleteTask(id, true, "NoUpdates");
                if (OnlyInformifHigherAvailable)
                    goto Return;

               _= MessageBox.Show("NoUpdatesAvailable", "NoUpdates",Enums.MessageBoxButtons.Ok);

                goto Return;
            }
            var a = rel.Assets.First(x => x.Name.EndsWith("msixbundle") && x.Name.ToLower().Contains(this.Architecture.ToString().ToLower()));
            if(a == null)
                goto Return ;
            DownloadQAndInstallUpdate(a.BrowserDownloadUrl,a.Name);

        Return:
            isRunning = false;
            return;
        }

        private async void DownloadQAndInstallUpdate(string url, string filename)
        {
            var path = ApplicationData.Current.TemporaryFolder.Path + "\\" + filename;
            using var client = new FileDownloader(url, path);
            var t = TasksHelper.AddProgressTask("DownloadingUpdate");
            client.ProgressChanged += (totalFileSize, totalBytesDownloaded, progressPercentage) =>
            {
                TasksHelper.EditProgressTask(t, Convert.ToInt32(progressPercentage));
                if (progressPercentage == 100)
                {
                    client.Dispose();
                    TasksHelper.CompleteTask(t, true);
                    Install(path);
                }
            };

            await client.StartDownload();
        }
        private async void Install(string path)
        {
            var f = await ApplicationData.Current.TemporaryFolder.CreateFileAsync("script.ps1", CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(f, $"taskkill.exe /f /im Emerald.App.exe\nAdd-AppxPackage -Path \"{path}\" -ForceUpdateFromAnyVersion -ForceTargetApplicationShutdown\r\nstart emerald.exe\r\nexit");

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{f.Path}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            Process.Start(startInfo);            
        }
    }
}