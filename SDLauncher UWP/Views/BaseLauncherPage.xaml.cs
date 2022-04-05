using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Storage;
using CmlLib.Core;
using CmlLib.Core.Auth;
using System.Threading.Tasks;
using System.ComponentModel;
using CmlLib.Core.Downloader;
using CmlLib.Core.Installer.FabricMC;
using Microsoft.UI.Xaml.Controls;
using System.Net.NetworkInformation;
using System.Net;
using System.IO.Compression;
using Windows.Networking.BackgroundTransfer;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace SDLauncher_UWP
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class BaseLauncherPage : Page
    {
        CMLauncher launcher;
        MinecraftPath gamepath;
        MessageBoxEx p;
        string launchVer;
        bool isOptiFineRuns;
        public BaseLauncherPage()
        {
            this.InitializeComponent();
            var timer = new DispatcherTimer();
            timer.Interval = new TimeSpan(0, 0, 0, 0, 1);
            timer.Tick += Timer_Tick;
            timer.Start();
            gamepath = new MinecraftPath(ApplicationData.Current.LocalFolder.Path);
            initializeLauncher(gamepath);
            if (new Util().CheckInternet() != true)
            {
                new MessageBoxEx("me", "e", MessageBoxEx.Buttons.Ok).ShowAsync();
            }
        }

        private void Timer_Tick(object sender, object e)
        {
            if (vars.ShowLaunchTips)
            {
                vars.ShowLaunchTips = false;
                tipVer.IsOpen = true;
            }
            if (vars.theme != null)
            {
                if (Window.Current.Content is FrameworkElement fe)
                {
                    fe.RequestedTheme = (ElementTheme)vars.theme;
                }
            }
            if (vars.UserName != null)
            {
                txtWelcome.Text = "Welcome " + vars.UserName + "!";
            }
        }

        private async Task initializeLauncher(MinecraftPath path)
        {
            gamepath = path;

            launcher = new CMLauncher(path);
            vars.LauncherSynced = launcher;
            launcher.FileChanged += Launcher_FileChanged;
            launcher.ProgressChanged += Launcher_ProgressChanged;
            await refreshVersions(null);
        }

        private void Launcher_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            pb_Prog.Maximum = 100;
            pb_Prog.Value = e.ProgressPercentage;
        }

        private void Launcher_FileChanged(DownloadFileChangedEventArgs e)
        {
            txtStatus.Text = $"{e.FileKind} : {e.FileName} ({e.ProgressedFileCount}/{e.TotalFileCount})";
            pb_File.Maximum = e.TotalFileCount;
            pb_File.Value = e.ProgressedFileCount;
        }

        public static CmlLib.Core.Version.MVersionCollection mcVers;
        public static CmlLib.Core.Version.MVersionCollection mcFabricVers;
        private async Task refreshVersions(string showVersion)
        {
            mcVers = await launcher.GetAllVersionsAsync();

            //mcFabricVers = await new FabricVersionLoader().GetVersionMetadatasAsync();

            //foreach (var item in mcFabricVers)
            //{
            //cmbxVer.Items.Add(item.Name);
            //}
            foreach (var item in mcVers)
            {
                cmbxVer.Items.Add(item.Name);
            }

        }
        private async void BtnLaunch_Click(object sender, RoutedEventArgs e)
        {
            if (vars.session == null)
            {
                p = new MessageBoxEx("Error", "Please Login", MessageBoxEx.Buttons.OkCancel);
                await p.ShowAsync();
                if (p.Result == MessageBoxEx.Results.Ok)
                {
                    await new Login().ShowAsync();
                    BtnLaunch_Click(null, null);
                }
                return;
            }
            if (cmbxVer.SelectedItem == null)
            {
                p = new MessageBoxEx("Error", "Please enter a version", MessageBoxEx.Buttons.Ok);
                await p.ShowAsync();
                cmbxVer.Focus(FocusState.Keyboard);
                return;
            }
            if (vars.MinRam == null) { p = new MessageBoxEx("Error", "Invalid RAM", MessageBoxEx.Buttons.Ok); await p.ShowAsync(); return; }
            if (vars.CurrentRam == null) { p = new MessageBoxEx("Error", "Invalid RAM", MessageBoxEx.Buttons.Ok); await p.ShowAsync(); return; }
            ToolTipService.SetToolTip(btnLaunch, gamepath.BasePath);
            System.Net.ServicePointManager.DefaultConnectionLimit = 256;
            launcher.FileDownloader = new AsyncParallelDownloader();
            try
            {
                var process = await launcher.CreateProcessAsync(cmbxVer.SelectedItem.ToString(), new MLaunchOption
                {
                    MinimumRamMb = vars.MinRam,
                    MaximumRamMb = vars.CurrentRam,
                    Session = vars.session,
                }); ;
                process.Start();
            }
            catch (System.Net.WebException)
            {
                p = new MessageBoxEx("Error", "Seems like you don't have good internet", MessageBoxEx.Buttons.Ok);
                p.ShowAsync();
            }
            catch (MDownloadFileException mex) // download exception
            {
                p = new MessageBoxEx("Error",
                    $"FileName : {mex.ExceptionFile.Name}\n" +
                    $"FilePath : {mex.ExceptionFile.Path}\n" +
                    $"FileUrl : {mex.ExceptionFile.Url}\n" +
                    $"FileType : {mex.ExceptionFile.Type}\n\n" +
                    mex.ToString(), MessageBoxEx.Buttons.Ok);
                p.ShowAsync();
            }
            catch (Win32Exception wex) // java exception
            {
                p = new MessageBoxEx("Error", wex + "\n\nIt seems your java setting has problem", MessageBoxEx.Buttons.Ok);
                p.ShowAsync();
            }
            catch (Exception ex) // all exception
            {
                p = new MessageBoxEx("Error", ex.ToString(), MessageBoxEx.Buttons.Ok);
                p.ShowAsync();
            }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
        }

        private void tip_CloseButtonClick(TeachingTip sender, object args)
        {
            if (sender.Title == "Choose A version")
            {
                tipLaunch.IsOpen = true;
            }
        }


        private void tipLaunch_ActionButtonClick(TeachingTip sender, object args)
        {
            tipLaunch.IsOpen = false;
            vars.ShowTips = false;
        }

        private void MenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem mitem)
            {
                VersionCheck(mitem);
            }
        }
        private void VersionCheck(MenuFlyoutItem item)
        {
            if (item.Text.ToString() == "Latest")
            {
                btnMCVer.Content = launcher.Versions?.LatestReleaseVersion?.Name;
                launchVer = btnMCVer.Content.ToString();
                return;
            }
            else if (item.Text.ToString() == "Latest Snapshot")
            {
                btnMCVer.Content = launcher.Versions?.LatestSnapshotVersion?.Name;
                launchVer = btnMCVer.Content.ToString();
                return;
            }
            else if (item.Text.ToString() == "OptiFine 1.18.2")
            {
                CheckOptiFine("1.18.2", "1.18.2-OptiFine_HD_U_H6_pre1", item);
            }
            else if (item.Text.ToString() == "OptiFine 1.18.1")
            {
                CheckOptiFine("1.18.1", "1.18.1-OptiFine_HD_U_H4", item);
            }
            else if (item.Text.ToString() == "OptiFine 1.17.1")
            {
                CheckOptiFine("1.17.1", "1.17.1-OptiFine_HD_U_H1", item);
            }
            else if (item.Text.ToString() == "OptiFine 1.16.5")
            {
                CheckOptiFine("1.16.5", "OptiFine 1.16.5", item);
            }
            else if (item.Text.ToString() == "Fabric 1.18.1")
            {
                CheckFabric("1.18.1", "fabric-loader-0.13.3-1.18.1", item);
            }
            else if (item.Text.ToString() == "Fabric 1.17.1")
            {
                CheckFabric("1.17.1", "fabric-loader-0.13.3-1.17.1", item);
            }
            else if (item.Text.ToString() == "Fabric 1.16.5")
            {
                CheckFabric("1.16.5", "fabric-loader-0.13.3-1.16.5", item);
            }
            else
            {
                btnMCVer.Content = item.Text;
                launchVer = btnMCVer.Content.ToString();
            }
        }
        //
        private void UI(bool value)
        {
            btnLaunch.IsEnabled = value;
            btnMCVer.IsEnabled = value;
            cmbxVer.IsEnabled = value;
        }
        //
        private async void CheckFabric(string mcver, string modver, MenuFlyoutItem mit)
        {
            bool exists = false;
            foreach (var veritem in mcFabricVers)
            {
                if (veritem.Name == modver)
                {
                    exists = true;
                }
            }
            if (exists)
            {
                launchVer = modver;
                btnMCVer.Content = mit.Text.ToString();
                txtStatus.Text = "Getting Fabric";
                UI(false);
                System.Threading.Thread thread = new System.Threading.Thread(async () =>
                {
                    var fabric = mcFabricVers.GetVersionMetadata(launchVer);
                    await fabric.SaveAsync(gamepath);
                    UI(true);
                    txtStatus.Text = "Ready";
                    await refreshVersions(null);
                    launchVer = modver;
                    btnMCVer.Content = mit.Text.ToString();

                });
                thread.Start();
                txtStatus.Text = "Ready";
            }
            else
            {
                var msg = new MessageBoxEx("Error", "To run " + mit.Text.ToString() + " you need to have installed version " + mcver + ". Vanilla,Do you want to install now ?", MessageBoxEx.Buttons.YesNo);
                await msg.ShowAsync();
                if (msg.Result == MessageBoxEx.Results.Yes)
                {
                    btnMCVer.Content = mcver;
                    launchVer = mcver;
                }
            }
        }
        //
        string optver;
        public async Task<bool> IsOptiFineFilePresent(string lastFileName,string mcVer,bool isLib)
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
        private async void CheckOptiFine(string mcver, string modVer, MenuFlyoutItem mit)
        {
            MessageBoxEx msgbx;
            bool exists = false;
            foreach (var veritem in mcVers)
            {
                if (veritem.Name == modVer)
                {
                    exists = true;
                }
            }
            if (exists)
            {
                btnMCVer.Content = mit.Text.ToString();
                launchVer = modVer;
            }
            else
            {
                msgbx = new MessageBoxEx("Error","Couldn't find OptiFine installed on this minecraft. Do you want to download and install from our servers ?", MessageBoxEx.Buttons.YesNo);
                await msgbx.ShowAsync();
                if (msgbx.Result == MessageBoxEx.Results.Yes)
                {
                    if (await IsOptiFineFilePresent(mcver + ".jar",mcver,false))
                    {
                        if (await IsOptiFineFilePresent(null,null,true))
                        {
                            if (mcver == "1.18.2")
                            {
                                btnMCVer.Content = mit.Text.ToString();
                                launchVer = modVer;
                                isOptiFineRuns = true;
                                UI(false);
                                optver = ": " + mcver;
                                OptFineDownload("https://raw.githubusercontent.com/Chaniru22/SDLauncher/main/OptiFine-1.18.2.zip", "OptiFine-" + mcver + ".zip", ModType.ver);
                            }
                            if (mcver == "1.18.1")
                            {
                                btnMCVer.Content = mit.Text.ToString();
                                launchVer = modVer;
                                isOptiFineRuns = true;
                                UI(false);
                                optver = ": " + mcver;
                                OptFineDownload("https://raw.githubusercontent.com/Chaniru22/SDLauncher/main/OptiFine-1.18.1.zip", "OptiFine-" + mcver + ".zip", ModType.ver);
                            }
                            else if (mcver == "1.17.1")
                            {
                                btnMCVer.Content = mit.Text.ToString();
                                launchVer = modVer;
                                isOptiFineRuns = true;
                                UI(false);
                                optver = ": " + mcver;
                                OptFineDownload("https://raw.githubusercontent.com/Chaniru22/SDLauncher/main/OptiFine-1.17.1.zip", "OptiFine-" + mcver + ".zip", ModType.ver);
                            }
                            else if (mcver == "1.16.5")
                            {
                                btnMCVer.Content = mit.Text.ToString();
                                launchVer = modVer;
                                isOptiFineRuns = true;
                                UI(false);
                                optver = ": " + mcver;
                                OptFineDownload("https://raw.githubusercontent.com/Chaniru22/SDLauncher/main/OptiFine-1.16.5.zip","OptiFine-" + mcver + ".zip", ModType.ver);
                            }
                        }
                        else
                        {
                            isOptiFineRuns = true;
                            msgbx = new MessageBoxEx("Error", "This will download main OptiFine library, Please click again " + mit.Text.ToString() + " (after download and extract the main OptiFine) to install optifine of that version !", MessageBoxEx.Buttons.Ok);
                            await msgbx.ShowAsync();
                            optver = " Lib";
                            OptFineDownload("https://raw.githubusercontent.com/Chaniru22/SDLauncher/main/optifine.zip", Directory.GetCurrentDirectory() + "\\OptiFine.zip", ModType.lib);
                        }
                    }
                    else
                    {
                        msgbx = new MessageBoxEx("Error", "You have to install & run minecraft version " + mcver + " one time to install OptiFine", MessageBoxEx.Buttons.Ok);
                        await msgbx.ShowAsync();
                        btnMCVer.Content = mcver;
                        launchVer = mcver;
                    }
                }
            }
        }
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
            UI(false);
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
            txtStatus.Text = "Downloading: OptiFine" + optver;
            pb_Prog.Maximum = 100;
            pb_Prog.Value = int.Parse(Math.Truncate(percentage).ToString());
            if(pb_Prog.Value == 100)
            {
                client_DownloadFileCompleted(null, null);
            }
        }

        async void client_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            txtStatus.Text = "Extracting";

            //Read the file stream
            var a = await ApplicationData.Current.TemporaryFolder.GetFileAsync(optDir);
            Stream b = await a.OpenStreamForReadAsync();
            //unzip
            ZipArchive archive = new ZipArchive(b);
            if (dwnOptiType == ModType.lib)
            {
                archive.ExtractToDirectory(gamepath.BasePath + @"\libraries", true);
            }
            else if (dwnOptiType == ModType.ver)
            {

                archive.ExtractToDirectory(gamepath.BasePath + @"\versions", true);
            }
            var oldDisver = btnMCVer.Content.ToString();
            var oldVer = launchVer;
            await refreshVersions(null);
            launchVer = oldVer;
            btnMCVer.Content = oldDisver;
            txtStatus.Text = "Ready";
            isOptiFineRuns = false;
            UI(true);
        }
    }
}
