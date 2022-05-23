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
using System.Diagnostics;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Microsoft.Toolkit.Uwp.Helpers;
using SDLauncher_UWP.Helpers;
using Windows.Foundation.Metadata;
using Windows.ApplicationModel;
using SDLauncher_UWP.Converters;
using SDLauncher_UWP.Resources;
// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace SDLauncher_UWP
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    ///
    
    public sealed partial class BaseLauncherPage : Page
    {
        public static string launchVer { get; set; }
        public event EventHandler<SDLauncher.UIChangeRequestedEventArgs> UIchanged = delegate { };
        public BaseLauncherPage()
        {
            this.InitializeComponent();
            var timer = new DispatcherTimer();
            timer.Interval = new TimeSpan(0, 0, 0, 0, 1);
            timer.Tick += Timer_Tick;
            timer.Start();
            InitializeLauncher();
        }

        private void InitializeLauncher()
        {
            UI(false);
            vars.Launcher = SDLauncher.CreateLauncher(new MinecraftPath(ApplicationData.Current.LocalFolder.Path));
            vars.Launcher.UIChangeRequested += Launcher_UIChangeRequested;
            vars.Launcher.VersionsRefreshed += Launcher_VersionsRefreshed;
            vars.Launcher.StatusChanged += Launcher_StatusChanged;
            vars.Launcher.FileOrProgressChanged += Launcher_FileOrProgressChanged;
            vars.Launcher.OptiFine.DownloadCompleted += OptiFine_DownloadCompleted;
            UI(true);
        }

        private void Launcher_VersionsRefreshed(object sender, EventArgs e)
        {
            cmbxVer.ItemsSource = vars.Launcher.MCVerNames;
        }

        private async void OptiFine_DownloadCompleted(object sender, EventArgs e)
        {
            if (!(bool)sender)
            {
                launchVer = "";
                btnMCVer.Content = "Version";
            }
            else
            {
                await vars.Launcher.RefreshVersions();
                var result = await vars.Launcher.OptiFine.CheckOptiFine(vars.Launcher.OptiFine.MCver, vars.Launcher.OptiFine.Modver, vars.Launcher.OptiFine.Displayver);
                OptiFineFinish(vars.Launcher.OptiFine.MCver, vars.Launcher.OptiFine.Modver, vars.Launcher.OptiFine.Displayver, result);
            }
        }

        private void Launcher_FileOrProgressChanged(object sender, SDLauncher.ProgressChangedEventArgs e)
        {
            if(e.CurrentFile != null && e.MaxFiles != null)
            {
                pb_File.Value = (int)e.CurrentFile;
                pb_File.Value = (int)e.MaxFiles;
            }
            if(e.ProgressPercentage != null)
            {
                pb_Prog.Maximum = 100;
                pb_Prog.Value = (int)e.ProgressPercentage;
            }
        }

        private void Launcher_StatusChanged(object sender, SDLauncher.StatusChangedEventArgs e)
        {
            txtStatus.Text = e.Status;
        }

        private void Launcher_UIChangeRequested(object sender, SDLauncher.UIChangeRequestedEventArgs e)
        {
            UI(e.UI);
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
                txtWelcome.Text = Localized.Welcome + ", " + vars.UserName + "!";
            }
        }

        private async void BtnLaunch_Click(object sender, RoutedEventArgs e)
        {
            UI(false);
            if (vars.session == null)
            {
                if (await MessageBox.Show(Localized.Error, Localized.BegLogIn, MessageBoxButtons.OkCancel) == MessageBoxResults.Ok)
                {
                    _ = await new Login().ShowAsync();
                    UI(true);
                    BtnLaunch_Click(null, null);
                }
                return;
            }
            if (launchVer == null)
            {
                UI(true);
                _ = await MessageBox.Show(Localized.Error, Localized.BegVer, MessageBoxButtons.Ok);
                _ = cmbxVer.Focus(FocusState.Keyboard);
                return;
            }
            if (vars.MinRam == 0) { _ = await MessageBox.Show(Localized.Error, Localized.WrongRAM, MessageBoxButtons.Ok); return; }
            if (vars.CurrentRam == 0) { _ = await MessageBox.Show(Localized.Error, Localized.WrongRAM, MessageBoxButtons.Ok); return; }
            System.Net.ServicePointManager.DefaultConnectionLimit = 256;
            vars.Launcher.Launcher.FileDownloader = new AsyncParallelDownloader();
            try
            {
                var l = new MLaunchOption
                {
                    MinimumRamMb = vars.MinRam,
                    MaximumRamMb = vars.CurrentRam,
                    Session = vars.session,
                };

                if (vars.JVMScreenWidth != 0 && vars.JVMScreenHeight != 0)
                {
                    l.ScreenWidth = vars.JVMScreenWidth;
                    l.ScreenHeight = vars.JVMScreenHeight;
                }
                l.FullScreen = vars.FullScreen;
                l.JVMArguments = vars.JVMArgs.ToArray();
                var process = await vars.Launcher.Launcher.CreateProcessAsync(launchVer, l);
                StartProcess(process);
            }
            catch (WebException)
            {
                _ = await MessageBox.Show(Localized.Error, Localized.NoNetwork, MessageBoxButtons.Ok);
            }
            catch (MDownloadFileException mex) // download exception
            {
                _ = await MessageBox.Show(Localized.Error,
                    $"FileName : {mex.ExceptionFile.Name}\n" +
                    $"FilePath : {mex.ExceptionFile.Path}\n" +
                    $"FileUrl : {mex.ExceptionFile.Url}\n" +
                    $"FileType : {mex.ExceptionFile.Type}\n\n" +
                    mex.ToString(), MessageBoxButtons.Ok);
            }
            catch (Win32Exception wex) // java exception
            {
                _ = await MessageBox.Show(Localized.Error, wex + "\n\n" + Localized.Win32Error, MessageBoxButtons.Ok);
            }
            catch (Exception ex) // all exception
            {
                _ = await MessageBox.Show(Localized.Error, ex.ToString(), MessageBoxButtons.Ok);
            }
            UI(true);
        }
        private async void StartProcess(Process process)
        {
            await ProcessToXmlConverter.Convert(process, ApplicationData.Current.LocalFolder, "StartInfo.xml");
            if (ApiInformation.IsApiContractPresent(
                 "Windows.ApplicationModel.FullTrustAppContract", 1, 0))
            {
                await
                  FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync("Admin");
            }
        }
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (vars.UseOldVerSeletor)
            {
                btnMCVer.Visibility = Visibility.Collapsed;
                cmbxVer.Visibility = Visibility.Visible;
            }
            else
            {
                cmbxVer.Visibility = Visibility.Collapsed;
                btnMCVer.Visibility = Visibility.Visible;
            }
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
            if (flyVer.IsOpen)
            {
                flyVer.Hide();
            }
            if (sender is MenuFlyoutItem mitem && !vars.UseOldVerSeletor)
            {
                VersionCheck(mitem);
            }
        }
        private async void VersionCheck(MenuFlyoutItem item)
        {
            string displayName = item.Text.ToString();
            OptFineVerReturns result;
            switch (displayName)
            {
                case "Latest":
                    launchVer = vars.Launcher.Launcher.Versions.LatestReleaseVersion.Name;
                    btnMCVer.Content = launchVer;
                    break;
                case "Latest Snapshot":
                    launchVer = vars.Launcher.Launcher.Versions?.LatestSnapshotVersion?.Name;
                    btnMCVer.Content = launchVer;
                    break;
                case "OptiFine 1.18.2":
                    result = await vars.Launcher.OptiFine.CheckOptiFine("1.18.2", "1.18.2-OptiFine_HD_U_H6_pre1", displayName);
                    OptiFineFinish("1.16.5", "OptiFine 1.16.5", displayName, result);
                    break;
                case "OptiFine 1.18.1":
                    result = await vars.Launcher.OptiFine.CheckOptiFine("1.18.1", "1.18.1-OptiFine_HD_U_H4", displayName);
                    OptiFineFinish("1.16.5", "OptiFine 1.16.5", displayName, result);
                    break;
                case "OptiFine 1.17.1":
                    result =  await vars.Launcher.OptiFine.CheckOptiFine("1.17.1", "1.17.1-OptiFine_HD_U_H1", displayName);
                    OptiFineFinish("1.16.5", "OptiFine 1.16.5", displayName, result);
                    break;
                case "OptiFine 1.16.5":
                    result = await vars.Launcher.OptiFine.CheckOptiFine("1.16.5", "OptiFine 1.16.5", displayName);
                    OptiFineFinish("1.16.5", "OptiFine 1.16.5", displayName, result);
                    break;
            }
            if (item.Text.ToString() == "Fabric 1.18.1")
            {
                FabricResponse(await vars.Launcher.CheckFabric("1.18.1", "fabric-loader-0.13.3-1.18.1", item.Text));
            }
            else if (item.Text.ToString() == "Fabric 1.17.1")
            {
                FabricResponse(await vars.Launcher.CheckFabric("1.17.1", "fabric-loader-0.13.3-1.17.1", item.Text));
            }
            else if (item.Text.ToString() == "Fabric 1.16.5")
            {
                FabricResponse(await vars.Launcher.CheckFabric("1.16.5", "fabric-loader-0.13.3-1.16.5", item.Text));
            }
            else
            {
                btnMCVer.Content = item.Text;
                launchVer = btnMCVer.Content.ToString();
            }
        }
        //
        private void FabricResponse(SDLauncher.FabricResponsoe responsoe)
        {
            launchVer = responsoe.LaunchVer;
            btnMCVer.Content = responsoe.DisplayVer;
        }
        private void UI(bool value)
        {
            UIchanged(this, new SDLauncher.UIChangeRequestedEventArgs(value));
            btnLaunch.IsEnabled = value;
            btnMCVer.IsEnabled = value;
            cmbxVer.IsEnabled = value;
        }
        private void OptiFineFinish(string mcver, string modver, string displayVer, OptFineVerReturns returned)
        {
            txtStatus.Text = "Ready";
            UI(true);
            switch (returned.Result)
            {
                case OptFineVerReturns.Results.DownloadOptiFineLib:
                    btnMCVer.Content = "Version";
                    launchVer = "";
                    break;
                case OptFineVerReturns.Results.DownloadOptiFineVer:
                    pb_File.Value = 0;
                    pb_Prog.Maximum = 100;
                    vars.Launcher.OptiFine.DownloadOptiFineVer(mcver, modver, displayVer);
                    break;
                case OptFineVerReturns.Results.Failed:
                    _ = MessageBox.Show(Localized.Error, Localized.GetVerFailed, MessageBoxButtons.Ok);
                    btnMCVer.Content = "Version";
                    launchVer = "";
                    break;
                case OptFineVerReturns.Results.Exists:
                    btnMCVer.Content = returned.btnVer;
                    launchVer = returned.LaunchVer;
                    break;
                case OptFineVerReturns.Results.DownloadMCVer:
                    btnMCVer.Content = mcver;
                    launchVer = mcver;
                    break;
            }
        }

        

        private void cmbxVer_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (vars.UseOldVerSeletor)
            {
                launchVer = cmbxVer.SelectedItem.ToString();
            }
        }
    }
}

