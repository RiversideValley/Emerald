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
using CmlLib.Utils;
using MojangAPI;
using SDLauncher_UWP.Dialogs;
using SDLauncher_UWP.DataTemplates;
using Microsoft.Toolkit.Uwp.Notifications;
using Windows.UI.Notifications;
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
        public ChangeLogsPage LogsPage { get; set; }
        public StorePage StorePage { get; set; }
        public event EventHandler<SDLauncher.UIChangeRequestedEventArgs> UIchanged = delegate { };
        public BaseLauncherPage()
        {
            this.InitializeComponent();
            var timer = new DispatcherTimer();
            timer.Interval = new TimeSpan(0, 0, 0, 0, 1);
            timer.Tick += Timer_Tick;
            timer.Start();
            vars.ThemeUpdated += Vars_ThemeUpdated;
            var s = new ServerTemplate("mc.hypixel.net", 25565);
        }

        private void Vars_ThemeUpdated(object sender, EventArgs e)
        {
            if (vars.Theme != null)
            {
                if (Window.Current.Content is FrameworkElement fe)
                {
                    fe.RequestedTheme = (ElementTheme)vars.Theme;
                }
            }
            this.RequestedTheme = (ElementTheme)vars.Theme;
            if (vars.Theme != ElementTheme.Default)
            {
                while (this.ActualTheme != vars.Theme)
                {

                }
            }
        }

        public async void InitializeLauncher()
        {
            UI(false);
            int taskID = LittleHelp.AddTask("Initialize Launcher Core");
            navitmStore.IsEnabled = false;
            vars.Launcher.UIChangeRequested += Launcher_UIChangeRequested;
            vars.Launcher.VersionsRefreshed += Launcher_VersionsRefreshed;
            vars.Launcher.StatusChanged += Launcher_StatusChanged;
            vars.Launcher.FileOrProgressChanged += Launcher_FileOrProgressChanged;
            vars.Launcher.OptiFine.DownloadCompleted += OptiFine_DownloadCompleted;
            vars.Launcher.GlacierClient.DownloadCompleted += GlacierClient_DownloadCompleted;
            vars.Launcher.VersionLoaderChanged += Launcher_VersionLoaderChanged;
            LogsPage = new ChangeLogsPage();
            navViewFrame.Content = LogsPage;
            LittleHelp.CompleteTask(taskID);
            await vars.Launcher.RefreshVersions();
            UI(true);
            if (await vars.Launcher.LoadStore() == true)
            {
                navitmStore.IsEnabled = true;
            }
            await vars.Launcher.LoadChangeLogs();
        }

        private void Launcher_VersionLoaderChanged(object sender, EventArgs e)
        {
            if (vars.Launcher.UseOfflineLoader)
            {
                btnMCVer.Visibility = Visibility.Collapsed;
                cmbxVer.Visibility = Visibility.Visible;
            }
        }

        private async void GlacierClient_DownloadCompleted(object sender, EventArgs e)
        {
            await vars.Launcher.RefreshVersions();
            bool exists = false;
            foreach(var item in vars.Launcher.MCVersions)
            {
                if(item.Name == "Glacier Client")
                {
                    exists = true;
                }
            }
            if (exists)
            {
                btnMCVer.Content = "Glacier Client";
                launchVer = "Glacier Client";
            }
            else
            {
                btnMCVer.Content = "Version";
                launchVer = "";
            }
        }

        private void Launcher_VersionsRefreshed(object sender, EventArgs e)
        {
            cmbxVer.ItemsSource = null;
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
                OptiFineFinish(await vars.Launcher.OptiFine.CheckOptiFine(vars.Launcher.OptiFine.MCver, vars.Launcher.OptiFine.Modver, vars.Launcher.OptiFine.Displayver));
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
                UI(true);
                return;
            }
            if (launchVer == null)
            {
                UI(true);
                _ = await MessageBox.Show(Localized.Error, Localized.BegVer, MessageBoxButtons.Ok);
                if(btnMCVer.Visibility == Visibility.Visible)
                {
                    btnMCVer.Focus(FocusState.Keyboard);
                }
                else
                {
                    cmbxVer.Focus(FocusState.Keyboard);
                }
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
                vars.Launcher.CreateToast();
                int taskID = LittleHelp.AddTask("Launch Minecraft");
                var process = await vars.Launcher.Launcher.CreateProcessAsync(launchVer, l);
                LittleHelp.CompleteTask(taskID);
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
            if (ApiInformation.IsApiContractPresent("Windows.ApplicationModel.FullTrustAppContract", 1, 0))
            {
                if (vars.AdminLaunch)
                {
                    await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync("Admin");
                }
                else
                {
                    await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync("User");
                }
            }
            CreateToast("Done!", "Successfully launcher minecraft version \"" + launchVer + "\"", true);
            if (vars.AutoClose)
            {
                Application.Current.Exit();
            }
        }
        private void CreateToast(string Title,string description,bool clearBefore)
        {
            if (clearBefore)
            {
                ToastNotificationManagerCompat.History.Clear();
            }
            var toastContent = new ToastContent()
            {
                Visual = new ToastVisual()
                {
                    BindingGeneric = new ToastBindingGeneric()
                    {
                        Children =
            {
                new AdaptiveText()
                {
                    Text = Title
                },
                new AdaptiveText()
                {
                    Text = description
                }
            }
                    }
                },
                Launch = "action=ToastClicked"
            };

            // Create the toast notification
            var toastNotif = new ToastNotification(toastContent.GetXml());

            // And send the notification
            ToastNotificationManager.CreateToastNotifier().Show(toastNotif);
        }
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LogsPage.UpdateLogs();
            if (vars.Launcher.UseOfflineLoader)
                return;

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
            if (sender is MenuFlyoutItem mitem && !vars.UseOldVerSeletor && !vars.Launcher.UseOfflineLoader)
            {
                VersionCheck(mitem);
            }
        }
        private async void VersionCheck(MenuFlyoutItem item)
        {
            string displayName = item.Text.ToString();
            switch (displayName)
            {
                case "Glacier Client":
                    if (await vars.Launcher.GlacierClient.ClientExists())
                    {
                        btnMCVer.Content = item.Text;
                        launchVer = btnMCVer.Content.ToString();
                    }
                    else
                    {
                        if(await MessageBox.Show("Information","Glacier client needs to be downloaded from their servers.Do you want to download it now",MessageBoxButtons.YesNo) == MessageBoxResults.Yes)
                        {
                            vars.Launcher.GlacierClient.DownloadClient();
                        }
                        else
                        {
                            btnMCVer.Content = "Version";
                            launchVer = "";
                        }
                    }
                    break;
                case "Latest":
                    launchVer = vars.Launcher.Launcher.Versions.LatestReleaseVersion.Name;
                    btnMCVer.Content = launchVer;
                    break;
                case "Latest Fabric":
                    FabricResponse(await vars.Launcher.CheckFabric(vars.Launcher.Launcher.Versions.LatestReleaseVersion.Name, SearchFabric(vars.Launcher.Launcher.Versions.LatestReleaseVersion.Name), item.Text));
                    btnMCVer.Content = "Fabric " + vars.Launcher.Launcher.Versions.LatestReleaseVersion.Name;
                    break;
                case "Latest Snapshot":
                    launchVer = vars.Launcher.Launcher.Versions?.LatestSnapshotVersion?.Name;
                    btnMCVer.Content = launchVer;
                    break;
                case "OptiFine 1.18.2":
                    OptiFineFinish(await vars.Launcher.OptiFine.CheckOptiFine("1.18.2", "1.18.2-OptiFine_HD_U_H6_pre1", displayName));
                    break;
                case "OptiFine 1.18.1":
                    OptiFineFinish(await vars.Launcher.OptiFine.CheckOptiFine("1.18.1", "1.18.1-OptiFine_HD_U_H4", displayName));
                    break;
                case "OptiFine 1.17.1":
                    OptiFineFinish(await vars.Launcher.OptiFine.CheckOptiFine("1.17.1", "1.17.1-OptiFine_HD_U_H1", displayName));
                    break;
                case "OptiFine 1.16.5":
                    OptiFineFinish(await vars.Launcher.OptiFine.CheckOptiFine("1.16.5", "OptiFine 1.16.5", displayName));
                    break;

                case "Fabric 1.19":
                    FabricResponse(await vars.Launcher.CheckFabric("1.19", SearchFabric("1.19"), item.Text));
                    break;
                case "Fabric 1.18.2":
                    FabricResponse(await vars.Launcher.CheckFabric("1.18.2", SearchFabric("1.18.2"), item.Text));
                    break;
                case "Fabric 1.18.1":
                    FabricResponse(await vars.Launcher.CheckFabric("1.18.1", SearchFabric("1.18.1"), item.Text));
                    break;
                case "Fabric 1.17.1":
                    FabricResponse(await vars.Launcher.CheckFabric("1.17.1", SearchFabric("1.17.1"), item.Text));
                    break;
                case "Fabric 1.16.5":
                    FabricResponse(await vars.Launcher.CheckFabric("1.16.5", SearchFabric("1.16.5"), item.Text));
                    break;
                default:
                    btnMCVer.Content = item.Text;
                    launchVer = btnMCVer.Content.ToString();
                    break;
            }
        }
        //
        public string SearchFabric(string ver)
        {
            var item = from t in vars.Launcher.FabricMCVersions where t.Name.EndsWith(ver) select t;
            if (item != null)
            {
                return item.FirstOrDefault().Name;
            }
            else
            {
                return "";
            }
        }
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
            btnServer.IsEnabled = value;
            cmbxVer.IsEnabled = value;
        }
        private void OptiFineFinish(OptFineVerReturns returned)
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
                    vars.Launcher.OptiFine.DownloadOptiFineVer(returned.MCVer, returned.ModVer, returned.DisplayVer);
                    break;
                case OptFineVerReturns.Results.Failed:
                    btnMCVer.Content = "Version";
                    launchVer = "";
                    break;
                case OptFineVerReturns.Results.Exists:
                    btnMCVer.Content = returned.DisplayVer;
                    launchVer = returned.ModVer;
                    break;
                case OptFineVerReturns.Results.DownloadMCVer:
                    btnMCVer.Content = returned.DisplayVer;
                    launchVer = returned.MCVer;
                    break;
            }
        }

        

        private void cmbxVer_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (vars.UseOldVerSeletor || vars.Launcher.UseOfflineLoader)
            {
                launchVer = cmbxVer.SelectedItem.ToString();
            }
        }

        private void Page_ActualThemeChanged(FrameworkElement sender, object args)
        {
        }

        private void btnServer_Click(object sender, RoutedEventArgs e)
        {
           _ = new ServerChooserDialog().ShowAsync();
        }

        private void navView_ItemInvoked(Microsoft.UI.Xaml.Controls.NavigationView sender, Microsoft.UI.Xaml.Controls.NavigationViewItemInvokedEventArgs args)
        {
            if(navView.SelectedItem is Microsoft.UI.Xaml.Controls.NavigationViewItem itm)
            {
                if(itm.Content.ToString() == "ChangeLogs")
                {
                    navViewFrame.Content = LogsPage;
                }
                else
                {
                    navViewFrame.Navigate(typeof(StorePage));
                }
            }
        }
    }
}

