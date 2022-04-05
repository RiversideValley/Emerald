using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace SDLauncher_UWP.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SettingsPage : Page
    {
        public SettingsPage()
        {
            this.InitializeComponent();

        }

        public void SetRam(long val)
        {

            txtRam.Text = val.ToString() + "MB";
            SliderRam.Value = val;
            vars.CurrentRam = (int)SliderRam.Value;
            if (vars.SliderRamMax != null && vars.SliderRamMax != null)
                SliderRam.Maximum = vars.SliderRamMax;
            SliderRam.Minimum = vars.SliderRamMin;
        }
        [Obsolete]

        public void ScrollteToTop()
        {
            scrlView.ScrollToVerticalOffset(-1000);
        }
        private void cmbxTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Window.Current.Content is FrameworkElement fe)
            {
                if (cmbxTheme.SelectedItem.ToString() == "Light")
                {
                    vars.theme = ElementTheme.Light;
                    fe.RequestedTheme = ElementTheme.Light;
                }
                if (cmbxTheme.SelectedItem.ToString() == "Dark")
                {
                    vars.theme = ElementTheme.Dark;
                    fe.RequestedTheme = ElementTheme.Dark;
                }
                if (cmbxTheme.SelectedItem.ToString() == "System")
                {
                    vars.theme = ElementTheme.Default;
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
           
            if (vars.theme == ElementTheme.Dark)
            {
                cmbxTheme.SelectedIndex = 1;
            }
            else if (vars.theme == ElementTheme.Light)
            {
                cmbxTheme.SelectedIndex = 0;
            }
            else if (vars.theme == ElementTheme.Default)
            {
                cmbxTheme.SelectedIndex = 2;
            }
            cbAsset.IsChecked = vars.AssestsCheck;
            cbHash.IsChecked = vars.HashCheck;
            switchAutolog.IsOn = vars.autoLog;
            pageCount++;
            if (pageCount == 1)
            {
                SetRam(vars.LoadedRam);
            }
            if(vars.LauncherSynced != null)
            {
                txtGamePath.Text = vars.LauncherSynced.MinecraftPath.BasePath;
            }
        }

        private async void btnXML_Click(object sender, RoutedEventArgs e)
        {
            var p = new MessageBoxEx("Information", "You need to close the application before editing the XML file. \nContinue ?", MessageBoxEx.Buttons.OkCancel);
            await p.ShowAsync();
            if (p.Result == MessageBoxEx.Results.Ok)
            {
                vars.showXMLOnClose = true;
                new SettingsData().CreateSettingsFile(true);
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
    }
}
