using Microsoft.Toolkit.Uwp.Helpers;
using SDLauncher_UWP.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace SDLauncher_UWP.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SettingsPage : Page
    {
        public event EventHandler BackRequested = delegate { };
        public event EventHandler UpdateBGRequested = delegate { };
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
            var half = SliderRam.Maximum / 2;
            if (half > 3000 && half < 6000)
            {
                SetRam(4096);
            }
            else if (half > 1000 && half < 3000)
            {
                SetRam(2048);
            }
            else if (half > 6000 && half < 12000)
            {
                SetRam(8192);
            }
            else if (half > 12000 && half < 17000)
            {
                SetRam(16384);
            }
            else if (half > 17000)
            {
                SetRam((long)half);
            }
            else if (half < 1000)
            {
                SetRam(512);
            }
        }
        int pageCount = 0;
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
           
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
            cbAsset.IsChecked = vars.AssestsCheck;
            chkbxFullScreen.IsChecked = vars.FullScreen;
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
            tglOldVerSelector.IsOn = vars.UseOldVerSeletor;
            pageCount++;
            if (pageCount == 1)
            {
                SetRam(vars.LoadedRam);
            }
            if(vars.Launcher.Launcher != null)
            {
                txtGamePath.Text = vars.Launcher.Launcher.MinecraftPath.BasePath;
            }
        }

        private async void btnXML_Click(object sender, RoutedEventArgs e)
        {
            
            if (await MessageBox.Show("Information", "You need to close the application before editing the XML file. \nContinue ?", MessageBoxButtons.OkCancel) == MessageBoxResults.Ok)
            {
                vars.showXMLOnClose = true;
                await new SettingsDataManager().CreateSettingsFile(true);
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

        private async void btnCopyDir_Click(object sender, RoutedEventArgs e)
        {
            smbCopyDir.Glyph = "\xE8FB";
            var dataPackage = new DataPackage();
            dataPackage.SetText(txtGamePath.Text);
            Clipboard.SetContent(dataPackage);
            await Task.Delay(TimeSpan.FromSeconds(2));
            smbCopyDir.Glyph = "\xE71B";
        }

        private void btnRefreshVers_Click(object sender, RoutedEventArgs e)
        {
            _ = vars.Launcher.RefreshVersions();
            BackRequested(this, new EventArgs());
        }

        private void tglOldVerSelector_Toggled(object sender, RoutedEventArgs e)
        {
            vars.UseOldVerSeletor = tglOldVerSelector.IsOn;
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

        public async void GetAndSetBG()
        {
            var file = await StorageFile.GetFileFromPathAsync(vars.BackgroundImagePath);
            vars.BackgroundImage = await Util.LoadImage(file);
            if (vars.CustomBackground)
            {
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
        }
        private async void cmbxBG_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if ((string)cmbxBG.SelectedItem == "Browse")
            {

                FileOpenPicker fop = new FileOpenPicker();
                fop.SuggestedStartLocation = PickerLocationId.Desktop;
                fop.ViewMode = PickerViewMode.Thumbnail;
                fop.FileTypeFilter.Add(".png");
                fop.FileTypeFilter.Add(".jpg");

                var file = await fop.PickSingleFileAsync();
                if(file != null)
                {
                    vars.BackgroundImage = await Util.LoadImage(file);
                    vars.CustomBackground = true;
                    vars.BackgroundImagePath = file.Path;
                    if(cmbxBG.Items[3] == null)
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
                    cmbxBG.SelectedItem = e.RemovedItems[0];
                }
            }
            else if ((string)cmbxBG.SelectedItem == "None")
            {
                if (cmbxBG.Items[3] != null)
                {
                    cmbxBG.Items.Remove(cmbxBG.Items[3]);
                }
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
                    if(ActualTheme == ElementTheme.Dark)
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
}