using Microsoft.Toolkit.Uwp.Helpers;
using Emerald.UWP.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Emerald.UWP.Views
{
    public sealed partial class SettingsPage : Page
    {
        public event EventHandler BackRequested = delegate { };
        public event EventHandler UpdateBGRequested = delegate { };
        public new bool Loaded { get; set; }
        public SettingsPage()
        {
            this.InitializeComponent();
        }

        public void SetRam(long val)
        {

            txtRam.Text = val.ToString() + "MB";
            SliderRam.Value = val;
            vars.CurrentRam = (int)SliderRam.Value;
            if (vars.SliderRamMax != 0 && vars.SliderRamMax != 0)
                SliderRam.Maximum = vars.SliderRamMax;
            SliderRam.Minimum = vars.SliderRamMin;
        }

        public void ScrollteToTop()
        {
            scrlView.ChangeView(0,0,1);
        }
        private void cmbxTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Window.Current.Content is FrameworkElement fe)
            {
                if (cmbxTheme.SelectedItem.ToString() == "Light")
                {
                    vars.Theme = ElementTheme.Light;
                    fe.RequestedTheme = ElementTheme.Light;
                }
                if (cmbxTheme.SelectedItem.ToString() == "Dark")
                {
                    vars.Theme = ElementTheme.Dark;
                    fe.RequestedTheme = ElementTheme.Dark;
                }
                if (cmbxTheme.SelectedItem.ToString() == "System")
                {
                    vars.Theme = ElementTheme.Default;
                    fe.RequestedTheme = ElementTheme.Default;
                }
            }
        }

        private void SliderRam_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            SetRam((long)SliderRam.Value);
        }

        private void rptbtnRamMinus_Click(object sender, RoutedEventArgs e)
        {
            SetRam((long)(SliderRam.Value - 50));
        }


        private void rptbtnRamPlus_Click(object sender, RoutedEventArgs e)
        {
            SetRam((long)(SliderRam.Value + 50));

        }

        public void btnAutoRAM_Click(object sender, RoutedEventArgs e)
        {
            var half = vars.SliderRamMax / 2;
            SetRam(half);
            //if (half > 3000 && half < 6000)
            //{
            //    SetRam(4096);
            //}
            //else if (half > 1000 && half < 3000)
            //{
            //    SetRam(2048);
            //}
            //else if (half > 6000 && half < 12000)
            //{
            //    SetRam(8192);
            //}
            //else if (half > 12000 && half < 17000)
            //{
            //    SetRam(16384);
            //}
            //else if (half > 17000)
            //{
            //    SetRam((long)half);
            //}
            //else if (half < 1000)
            //{
            //    SetRam(512);
            //}
        }
        int pageCount = 0;
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded = false;
            if (Core.MainCore.Launcher.UseOfflineLoader)
            {
                cmbxVerSelector.IsEnabled = false;
            }
           
            if (vars.Theme == ElementTheme.Dark)
            {
                cmbxTheme.SelectedIndex = 1;
            }
            else if (vars.Theme == ElementTheme.Light)
            {
                cmbxTheme.SelectedIndex = 0;
            }
            else if (vars.Theme == ElementTheme.Default)
            {
                cmbxTheme.SelectedIndex = 2;
            }
            tglEncryptSettings.IsOn = (bool)ApplicationData.Current.RoamingSettings.Values["IsInAppSettings"];
            pnlSettingsData.IsEnabled = !tglEncryptSettings.IsOn;
            cbAsset.IsChecked = vars.AssestsCheck;
            chkbxFullScreen.IsChecked = vars.FullScreen;
            tglAutoClose.IsOn = vars.AutoClose;
            cmbxUIStyle.SelectedItem = UIResourceHelper.CurrentStyle.ToString();
            if(vars.JVMScreenWidth != 0 && vars.JVMScreenHeight != 0)
            {
                nbrbxHeight.Value = vars.JVMScreenHeight;
                nbrbxWidth.Value = vars.JVMScreenWidth;
            }
            args.UpdateSource();
            ver.Text = $"Version {SystemInformation.Instance.ApplicationVersion.Major}.{SystemInformation.Instance.ApplicationVersion.Minor}.{SystemInformation.Instance.ApplicationVersion.Build}.{SystemInformation.Instance.ApplicationVersion.Revision}";
            RefreshScreenData();
            tglLogs.IsOn = vars.GameLogs;
            cbHash.IsChecked = vars.HashCheck;
            switchAutolog.IsOn = vars.autoLog;
            pageCount++;
            if (pageCount == 1)
            {
                SetRam(vars.LoadedRam);
            }
            if(Core.MainCore.Launcher.Launcher != null)
            {
                txtGamePath.Text = Core.MainCore.Launcher.Launcher.MinecraftPath.BasePath;
            }
            Loaded = true;
        }

        private async void btnXML_Click(object sender, RoutedEventArgs e)
        {
            
            if (await MessageBox.Show("Information", "You need to close the application before editing the JSON file. \nContinue ?", MessageBoxButtons.OkCancel) == MessageBoxResults.Ok)
            {
                await SettingsManager.SaveSettings();
                await Windows.System.Launcher.LaunchFileAsync(await ApplicationData.Current.RoamingFolder.GetFileAsync("settings.json"));
                Application.Current.Exit();
            }
        }

        private void cbAsset_Checked(object sender, RoutedEventArgs e)
        {
            vars.AssestsCheck = true;
        }

        private void cbAsset_Unchecked(object sender, RoutedEventArgs e)
        {
            vars.AssestsCheck = false;
        }

        private void cbHash_Checked(object sender, RoutedEventArgs e)
        {
            vars.HashCheck = true;
        }

        private void cbHash_Unchecked(object sender, RoutedEventArgs e)
        {
            vars.HashCheck = false;
        }

        private void switchAutolog_Toggled(object sender, RoutedEventArgs e)
        {
            if (switchAutolog.IsOn)
            {
                vars.autoLog = true;
            }
            else
            {
                vars.autoLog = false;
            }
        }

        private void btnCopyDir_Click(object sender, RoutedEventArgs e)
        {
            //var dataPackage = new DataPackage();
            // dataPackage.SetText(txtGamePath.Text);
            // Clipboard.SetContent(dataPackage);
            _ = Launcher.LaunchFolderPathAsync(Core.MainCore.Launcher.Launcher.MinecraftPath.BasePath);
        }

        private void btnRefreshVers_Click(object sender, RoutedEventArgs e)
        {
            _ = Core.MainCore.Launcher.RefreshVersions();
            BackRequested(this, new EventArgs());
        }



        private void nbrbxWidth_ValueChanged(Microsoft.UI.Xaml.Controls.NumberBox sender, Microsoft.UI.Xaml.Controls.NumberBoxValueChangedEventArgs args)
        {
            int? r = 0;
            if (double.IsNaN(args.NewValue))
            {
                r = -1;
            }
            else
            {
                var s = Math.Floor(args.NewValue);
                r = int.Parse(s.ToString());
            }
            SetScreenSize(width: r);
            RefreshScreenData();
        }

        private void SetScreenSize(int? width = null,int? height = null)
        {
            if (width != null)
            {
                if (width == 0)
                {
                    txtWidth.Visibility = Visibility.Visible;
                    vars.JVMScreenWidth = 0;
                    return;
                }
                if (width == -1)
                {
                    if (!double.IsNaN(nbrbxHeight.Value))
                    {
                        txtWidth.Visibility = Visibility.Visible;
                        vars.JVMScreenWidth = 0;
                        return;
                    }
                    else
                    {
                        txtWidth.Visibility = Visibility.Collapsed;
                        vars.JVMScreenWidth = 0;
                        return;
                    }
                }
                if (double.IsNaN(nbrbxHeight.Value))
                {
                    txtHeight.Visibility = Visibility.Visible;
                }
                txtWidth.Visibility = Visibility.Collapsed;
                vars.JVMScreenWidth = (int)width;
                return;
            }
            if (height != null)
            {
                if (height == 0)
                {
                    txtHeight.Visibility = Visibility.Visible;
                    vars.JVMScreenHeight = (int)height;
                    return;
                }
                if(height == -1)
                {
                    if (!double.IsNaN(nbrbxWidth.Value))
                    {
                        txtHeight.Visibility = Visibility.Visible;
                        vars.JVMScreenHeight = 0;
                        return;
                    }
                    else
                    {
                        txtHeight.Visibility = Visibility.Collapsed;
                        vars.JVMScreenHeight = 0;
                        return;
                    }
                }
                if (double.IsNaN(nbrbxWidth.Value))
                {
                    txtWidth.Visibility = Visibility.Visible;
                }
                txtHeight.Visibility = Visibility.Collapsed;
                vars.JVMScreenHeight = (int)height;
                return;
            }
        }
        private void nbrbxHeight_ValueChanged(Microsoft.UI.Xaml.Controls.NumberBox sender, Microsoft.UI.Xaml.Controls.NumberBoxValueChangedEventArgs args)
        {
            int? r = 0;
            if (double.IsNaN(args.NewValue))
            {
                r = -1;
            }
            else
            {
                var s = Math.Floor(args.NewValue);
                r = int.Parse(s.ToString());
            }
            SetScreenSize(height: r);
            RefreshScreenData();
        }

        private void RefreshScreenData()
        {
            if (vars.FullScreen)
            {
                nbrbxWidth.IsEnabled = false;
                nbrbxHeight.IsEnabled = false;
                txtScreenStatus.Text = "Full Screen";
            }
            else
            {
                nbrbxWidth.IsEnabled = true;
                nbrbxHeight.IsEnabled = true;
                if (vars.JVMScreenWidth != 0 && vars.JVMScreenHeight != 0)
                {
                    txtScreenStatus.Text = vars.JVMScreenWidth + " × " + vars.JVMScreenHeight;
                }
                else
                {
                    txtWidth.Visibility = Visibility.Collapsed;
                    txtHeight.Visibility = Visibility.Collapsed;
                    txtScreenStatus.Text = "Default";
                }
            }
        }
        private void chkbxFullScreen_Click(object sender, RoutedEventArgs e)
        {
            vars.FullScreen = (bool)chkbxFullScreen.IsChecked;
            RefreshScreenData();
        }

        private void tglRPC_Toggled(object sender, RoutedEventArgs e)
        {
            vars.SDRPC = new RPCHelper();
           _ = vars.SDRPC.Authenticate();
        }

        private void tglLogs_Toggled(object sender, RoutedEventArgs e)
        {
            vars.GameLogs = tglLogs.IsOn;
        }
        private bool BGEdit = true;
        public async void GetAndSetBG()
        {
            BGEdit = false;
            StorageFile file;
            string name;
            if(vars.BackgroundImagePath == "null")
            {
                name = "None";
                vars.BackgroundImage = new BitmapImage(new Uri("ms-appx:///Assets/BackDrops/Transparent.png"));
            }
            else
            {
                file = await StorageFile.GetFileFromPathAsync(vars.BackgroundImagePath);
                name = file.DisplayName;
                vars.BackgroundImage = await Util.LoadImage(file);
            }
            if (vars.CustomBackground)
            {
                if (cmbxBG.Items[3] == null)
                {
                    cmbxBG.Items.Add(name);
                    cmbxBG.SelectedIndex = 3;
                }
                else
                {
                    cmbxBG.Items[3] = name;
                    cmbxBG.SelectedIndex = 3;
                }
            }
            BGEdit = true;
        }
        private async void cmbxBG_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (BGEdit)
            {
                if ((string)cmbxBG.SelectedItem == "Browse")
                {
                    FileOpenPicker fop = new FileOpenPicker();
                    fop.SuggestedStartLocation = PickerLocationId.Desktop;
                    fop.ViewMode = PickerViewMode.Thumbnail;
                    fop.FileTypeFilter.Add(".png");
                    fop.FileTypeFilter.Add(".jpg");

                    var file = await fop.PickSingleFileAsync();
                    if (file != null)
                    {
                        vars.BackgroundImage = await Util.LoadImage(file);
                        vars.CustomBackground = true;
                        vars.BackgroundImagePath = file.Path;
                        if (cmbxBG.Items[3] == null)
                        {
                            cmbxBG.Items.Add(file.DisplayName);
                            cmbxBG.SelectedIndex = 3;
                        }
                        else
                        {
                            cmbxBG.Items[3] = file.DisplayName;
                            cmbxBG.SelectedIndex = 3;
                        }
                    }
                    else
                    {
                        if (e.RemovedItems.Count > 0)
                        {
                            cmbxBG.SelectedItem = e.RemovedItems[0];
                        }
                        else
                        {
                            cmbxBG.SelectedIndex = 0;
                        }
                    }
                }
                else if ((string)cmbxBG.SelectedItem == "None")
                {
                    if (cmbxBG.Items[3] != null)
                    {
                        cmbxBG.Items.Remove(cmbxBG.Items[3]);
                    }
                    vars.BackgroundImagePath = "null";
                    vars.BackgroundImage = new BitmapImage(new Uri("ms-appx:///Assets/BackDrops/Transparent.png"));
                    vars.CustomBackground = true;
                }
                else if ((string)cmbxBG.SelectedItem == "Default")
                {
                    if (cmbxBG.Items[3] != null)
                    {
                        cmbxBG.Items.Remove(cmbxBG.Items[3]);
                    }
                    vars.CustomBackground = false;

                    if (Window.Current.Content is FrameworkElement fe)
                    {
                        if (ActualTheme == ElementTheme.Dark)
                        {
                            fe.RequestedTheme = ElementTheme.Light;
                            fe.RequestedTheme = ElementTheme.Dark;
                        }
                        else
                        {
                            fe.RequestedTheme = ElementTheme.Dark;
                            fe.RequestedTheme = ElementTheme.Light;
                        }
                    }
                }
            }
        }
        private void tglAutoClose_Toggled(object sender, RoutedEventArgs e)
        {
            vars.AutoClose = tglAutoClose.IsOn;
        }

        private async void tglChangeAdmin_Toggled(object sender, RoutedEventArgs e)
        {
            if (tglChangeAdmin.IsOn)
            {
                vars.AdminLaunch = true;
            }
            else
            {
                if(await MessageBox.Show("Warning", "You are trying to do a thing that we never recommended.Are you sure to continue ?\n(This setting will revert after next time launch of the app.)", MessageBoxButtons.YesNo) == MessageBoxResults.Yes)
                {
                    vars.AdminLaunch = false;
                }
                else
                {
                    tglChangeAdmin.IsOn = true;
                    vars.AdminLaunch = true;
                }
            }
        }

        private void cmbxVerSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            vars.VerSelectors = (VerSelectors)Enum.Parse(typeof(VerSelectors), cmbxVerSelector.SelectedItem.ToString());
        }

        private async void btnExportXML_Click(object sender, RoutedEventArgs e)
        {
            FileSavePicker savePicker = new FileSavePicker();
            savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            savePicker.FileTypeChoices.Add("SDL Settings", new List<string>() { ".json" });
            savePicker.SuggestedFileName = "New Document";

            StorageFile sfile = await savePicker.PickSaveFileAsync();
            if (sfile != null)
            {
                await SettingsManager.SaveSettings(sfile);
            }
        }


        private async void btnImportXML_Click(object sender, RoutedEventArgs e)
        {
            if (await MessageBox.Show("Information", "A restart is required to load the settings correctly.\nContinue ?", MessageBoxButtons.YesNo) == MessageBoxResults.Yes)
            {
                var picker = new FileOpenPicker();
                picker.ViewMode = PickerViewMode.Thumbnail;
                picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                picker.FileTypeFilter.Add(".json");

                StorageFile file = await picker.PickSingleFileAsync();
                if (file != null)
                {
                    if (tglEncryptSettings.IsOn)
                    {
                        var text = await FileIO.ReadTextAsync(file);
                        ApplicationData.Current.RoamingSettings.Values["InAppSettings"] = text;
                    }
                    else
                    {
                        await file.CopyAsync(ApplicationData.Current.RoamingFolder, "settings.json", NameCollisionOption.ReplaceExisting);
                    }
                    await CoreApplication.RequestRestartAsync("");
                }
            }
        }
        private async void tglEncryptSettings_Toggled(object sender, RoutedEventArgs e)
        {
            if (tglEncryptSettings.IsOn)
            {
                try
                {
                    var file = await ApplicationData.Current.RoamingFolder.GetFileAsync("settings.json");
                    await file.DeleteAsync();
                }
                catch { }
                ApplicationData.Current.RoamingSettings.Values["IsInAppSettings"] = true;
                pnlSettingsData.IsEnabled = false;
            }
            else
            {
                pnlSettingsData.IsEnabled = true;
                ApplicationData.Current.RoamingSettings.Values["IsInAppSettings"] = false;
                await SettingsManager.SaveSettings();
            }
        }

        private async void cmbxUIStyle_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Loaded)
            {
                UIResourceHelper.CurrentStyle = (ResourceStyle)Enum.Parse(typeof(ResourceStyle), cmbxUIStyle.SelectedItem.ToString());
                var msg = await MessageBox.Show("Information", "Take a restart to see the change. Restart now ?", MessageBoxButtons.Custom,"Yes","Not now");
                if (msg == MessageBoxResults.Yes)
                {
                    if ((bool)ApplicationData.Current.RoamingSettings.Values["IsInAppSettings"] == false)
                    {
                        await SettingsManager.SaveSettings();
                    }
                    else
                    {
                        ApplicationData.Current.RoamingSettings.Values["InAppSettings"] = await SettingsManager.SerializeSettings();
                    }
                    await CoreApplication.RequestRestartAsync("");
                }
                else
                {

                }
            }
        }
    }

    public enum VerSelectors
    {
        Classic,
        Normal,
        Advanced
    }
}