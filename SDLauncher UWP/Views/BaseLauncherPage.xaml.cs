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
using SDLauncher.UWP.Views;
using System.Diagnostics;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Microsoft.Toolkit.Uwp.Helpers;
using SDLauncher.UWP.Helpers;
using Windows.Foundation.Metadata;
using Windows.ApplicationModel;
using SDLauncher.UWP.Converters;
using SDLauncher.UWP.Resources;
using CmlLib.Utils;
using MojangAPI;
using SDLauncher.UWP.Dialogs;
using SDLauncher.UWP.DataTemplates;
using Microsoft.Toolkit.Uwp.Notifications;
using Windows.UI.Notifications;
// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace SDLauncher.UWP
{
    
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    ///
    
    public sealed partial class BaseLauncherPage : Page
    {
        public static string launchVer { get; set; }
        public ChangeLogsPage LogsPage { get; set; }
        public MenuItemsCreator MCVerManager { get; set; }
        public StorePage StorePage { get; set; }
        public event EventHandler<Helpers.SDLauncher.UIChangeRequestedEventArgs> UIchanged = delegate { };
        public BaseLauncherPage()
        {
            this.InitializeComponent();
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
            Vars_SessionChanged(null, null);
            Vars_VerSelctorChanged(null, null);
            navitmStore.IsEnabled = false;
            vars.SessionChanged += Vars_SessionChanged;
            vars.Launcher.UIChangeRequested += Launcher_UIChangeRequested;
            vars.Launcher.VersionsRefreshed += Launcher_VersionsRefreshed;
            vars.Launcher.StatusChanged += Launcher_StatusChanged;
            vars.Launcher.FileOrProgressChanged += Launcher_FileOrProgressChanged;
            vars.Launcher.GlacierClient.DownloadCompleted += GlacierClient_DownloadCompleted;
            vars.Launcher.VersionLoaderChanged += Launcher_VersionLoaderChanged;
            vars.VerSelctorChanged += Vars_VerSelctorChanged;
            MCVerManager = new MenuItemsCreator();
            MCVerManager.ItemInvoked += MCVerManager_ItemInvoked;
            LogsPage = new ChangeLogsPage();
            navViewFrame.Content = LogsPage;
            LittleHelp.CompleteTask(taskID);
            await vars.Launcher.RefreshVersions();
            btnAdvMCVer.Flyout = MCVerManager.CreateVersions();
            UI(true);
            if (await vars.Launcher.LoadStore() == true)
            {
                navitmStore.IsEnabled = true;
            }
            await vars.Launcher.LoadChangeLogs();
        }

        private async void MCVerManager_ItemInvoked(object sender, MenuItemsCreator.ItemInvokedArgs e)
        {
            if(vars.VerSelectors == VerSelectors.Advanced)
            {
                if(e.MCType == MenuItemsCreator.MCType.Vanilla)
                {
                    launchVer = e.Ver;
                    btnAdvMCVer.Content = e.DisplayVer;
                }
                else
                {
                    int taskID = LittleHelp.AddTask("Get Fabric");
                    try
                    {
                        UI(false);
                        var fabric = vars.Launcher.FabricMCVersions.GetVersionMetadata(e.Ver);
                        await fabric.SaveAsync(vars.Launcher.Launcher.MinecraftPath);
                        UI(true);
                        btnAdvMCVer.Content = e.DisplayVer;
                        LittleHelp.CompleteTask(taskID, true);
                    }
                    catch
                    {
                        launchVer = "";
                        btnAdvMCVer.Content = "Pick a Version";
                        LittleHelp.CompleteTask(taskID, false);
                    }
                }
            }
        }

        private void Vars_VerSelctorChanged(object sender, EventArgs e)
        {
            switch (vars.VerSelectors)
            {
                case VerSelectors.Normal:
                    btnAdvMCVer.Visibility = Visibility.Collapsed;
                    cmbxVer.Visibility = Visibility.Collapsed;
                    btnMCVer.Visibility = Visibility.Visible;
                    break;
                case VerSelectors.Advanced:
                    btnAdvMCVer.Visibility = Visibility.Visible;
                    cmbxVer.Visibility = Visibility.Collapsed;
                    btnMCVer.Visibility = Visibility.Collapsed;
                    break;
                case VerSelectors.Classic:
                    btnAdvMCVer.Visibility = Visibility.Collapsed;
                    cmbxVer.Visibility = Visibility.Visible;
                    btnMCVer.Visibility = Visibility.Collapsed;
                    break;
            }
            if (vars.Launcher.UseOfflineLoader)
            {
                btnAdvMCVer.Visibility = Visibility.Collapsed;
                cmbxVer.Visibility = Visibility.Visible;
                btnMCVer.Visibility = Visibility.Collapsed;
            }
        }

        private void Vars_SessionChanged(object sender, EventArgs e)
        {
            if (vars.session != null)
            {
                txtWelcome.Text = Localized.Welcome + ", " + vars.session.Username + "!";
            }
            else
            {
                txtWelcome.Text = Localized.Welcome;
            }
        }

        private void Launcher_VersionLoaderChanged(object sender, EventArgs e)
        {
            if (vars.Launcher.UseOfflineLoader)
            {
                btnMCVer.Visibility = Visibility.Collapsed;
                btnAdvMCVer.Visibility = Visibility.Collapsed;
                vars.VerSelectors = VerSelectors.Classic;
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


        private void Launcher_FileOrProgressChanged(object sender, Helpers.SDLauncher.ProgressChangedEventArgs e)
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

        private void Launcher_StatusChanged(object sender, Helpers.SDLauncher.StatusChangedEventArgs e)
        {
            txtStatus.Text = e.Status;
        }

        private void Launcher_UIChangeRequested(object sender, Helpers.SDLauncher.UIChangeRequestedEventArgs e)
        {
            UI(e.UI);
        }

        public void ShowTips()
        {
            if(vars.VerSelectors == VerSelectors.Normal)
            {
                tipVer.Target = btnMCVer;
            }
            else if (vars.VerSelectors == VerSelectors.Classic)
            {
                tipVer.Target = cmbxVer;
            }
            else
            {
                tipVer.Target = btnAdvMCVer;
            }
            tipVer.IsOpen = true;
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
                else if(cmbxVer.Visibility == Visibility.Visible)
                {
                    cmbxVer.Focus(FocusState.Keyboard);
                }
                else
                {
                    btnAdvMCVer.Focus(FocusState.Keyboard);
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
                try
                {
                    var process = await vars.Launcher.Launcher.CreateProcessAsync(launchVer, l);
                LittleHelp.CompleteTask(taskID);
                StartProcess(process);
                }
                catch
                {
                    LittleHelp.CompleteTask(taskID, false);
                }
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
            pb_File.Value = 0;
            pb_Prog.Value = 0;
            txtStatus.Text = Localized.Ready;
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
            CreateToast("Done!", "Successfully launched minecraft version \"" + launchVer + "\"", true);
            if (vars.AutoClose)
            {
                await SettingsManager.SaveSettings();
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
            Vars_VerSelctorChanged(null, null);
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
            if (sender is MenuFlyoutItem mitem && vars.VerSelectors == VerSelectors.Normal && !vars.Launcher.UseOfflineLoader)
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
        private void FabricResponse(Helpers.SDLauncher.FabricResponsoe responsoe)
        {
            launchVer = responsoe.LaunchVer;
            btnMCVer.Content = responsoe.DisplayVer;
        }
        private void UI(bool value)
        {
            UIchanged(this, new Helpers.SDLauncher.UIChangeRequestedEventArgs(value));
            btnLaunch.IsEnabled = value;
            btnMCVer.IsEnabled = value;
            btnServer.IsEnabled = value;
            cmbxVer.IsEnabled = value;
        }
        private void cmbxVer_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (vars.VerSelectors == VerSelectors.Classic || vars.Launcher.UseOfflineLoader)
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
                    LogsPage.UpdateLogs();
                }
                else
                {
                    navViewFrame.Navigate(typeof(StorePage));
                }
            }
        }
    }
}

