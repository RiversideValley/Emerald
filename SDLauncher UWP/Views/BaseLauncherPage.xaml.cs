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
using SDLauncher_UWP.Views;

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
        OptiFine OptiFine;
        string launchVer;
        bool isOptiFineRuns;
        public BaseLauncherPage()
        {
            this.InitializeComponent();
            OptiFine = new OptiFine();
            var timer = new DispatcherTimer();
            timer.Interval = new TimeSpan(0, 0, 0, 0, 1);
            timer.Tick += Timer_Tick;
            timer.Start();
            gamepath = new MinecraftPath(ApplicationData.Current.LocalFolder.Path);
            initializeLauncher(gamepath);
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
            UI(false);
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
            UI(false);
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
            UI(true);

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
        private async void VersionCheck(MenuFlyoutItem item)
        {
            switch (item.Text.ToString())
            {
                case "Latest":
                    btnMCVer.Content = launcher.Versions?.LatestReleaseVersion?.Name;
                    launchVer = btnMCVer.Content.ToString();
                    break;
                case "Latest Snapshot":
                    btnMCVer.Content = launcher.Versions?.LatestSnapshotVersion?.Name;
                    launchVer = btnMCVer.Content.ToString();
                    break;
                case "OptiFine 1.18.2":
                    await OptiFine.CheckOptiFine("1.18.2", "1.18.2-OptiFine_HD_U_H6_pre1", item, mcVers);
                    OptiFineFinish("1.16.5", "OptiFine 1.16.5", item);
                    break;
                case "OptiFine 1.18.1":
                    await OptiFine.CheckOptiFine("1.18.1", "1.18.1-OptiFine_HD_U_H4", item, mcVers);
                    OptiFineFinish("1.16.5", "OptiFine 1.16.5", item);
                    break;
                case "OptiFine 1.17.1":
                    await OptiFine.CheckOptiFine("1.17.1", "1.17.1-OptiFine_HD_U_H1", item, mcVers);
                    OptiFineFinish("1.16.5", "OptiFine 1.16.5", item);
                    break;
                case "OptiFine 1.16.5":
                    await OptiFine.CheckOptiFine("1.16.5", "OptiFine 1.16.5", item, mcVers);
                    OptiFineFinish("1.16.5", "OptiFine 1.16.5", item);
                    break;
            }
            if (item.Text.ToString() == "Fabric 1.18.1")
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
        private void OptiFineFinish(string mcver,string modver,MenuFlyoutItem itm)
        {
            switch (OptiFine.returns.Result)
            {
                case OptFineVerReturns.Results.DownloadOptiFineVer:
                    pb_File.Value = 0;
                    pb_Prog.Maximum = 100;
                    OptiFine.DownloadOptiFineVer(mcver, modver, itm);
                    DispatcherTimer optFine = new DispatcherTimer();
                    optFine.Interval = new TimeSpan(0, 0, 0, 0, 1);
                    optFine.Tick += OptFine_Tick;
                    optFine.Start();
                    break;
                case OptFineVerReturns.Results.Failed:
                    new MessageBoxEx("Error", "Failed to get versions", MessageBoxEx.Buttons.Ok).ShowAsync();
                    break;
            }
        }

        private void OptFine_Tick(object sender, object e)
        {
            txtStatus.Text = OptiFine.DownloadStats;
            pb_Prog.Value = OptiFine.DownloadProg;
            UI(OptiFine.UI);
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
    }
}
