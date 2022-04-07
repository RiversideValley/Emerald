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
using System.Threading.Tasks;
using CmlLib.Core.Installer.FabricMC;
using CmlLib.Core.Downloader;
using System.ComponentModel;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.ViewManagement;
using Windows.UI;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using SDLauncher_UWP.Views;
using Windows.UI.Xaml.Media.Imaging;
using Windows.Storage;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace SDLauncher_UWP
{
    public sealed partial class MainPage : Page
    {
        public BaseLauncherPage launcher = new BaseLauncherPage();
        public SettingsPage settingsPage = new SettingsPage();
        public SettingsData settings = new SettingsData();
        public MainPage()
        {

            this.InitializeComponent();
        }


        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            Page_ActualThemeChanged(null, null);
            MainFrame.Content = launcher;

            foreach (var account in vars.Accounts)
            {
                
            }
            if (vars.ShowTips)
            {
                tipacc.IsOpen = true;
            }
            if (vars.IsFixedDiscord)
            {
                btnPinDiscord_Click(null, null);
            }
            vars.ProgressStatus = "Intializing RAM";
            var computerMemory = new Util().GetMemoryMb();
            if (computerMemory != null)
            {
                double max = (double)computerMemory;
                if (max < 1024)
                {
                    max = 1024;
                }
                else if (max > 4096 && max < 6000)
                {
                    max = 4096;
                }
                else if (max > 6000 && max < 12000)
                {
                    max = 8192;
                }
                else if (max > 12000 && max < 17000)
                {
                    max = 16384;
                }
                else if (max > 17000 && max < 21000)
                {
                    max = 20480;
                }
                else if (max > 21000 && max < 25000)
                {
                    max = 24576;
                }
                else if (max > 25000 && max < 29000)
                {
                    max = 28672;
                }
                else if (max > 29000 && max < 33000)
                {
                    max = 32768;
                }
                double min = max / 10;
                double slidermin = (long)(max / 7);
                vars.MinRam = (int)Math.Floor(min);
                if (slidermin < 1024 && slidermin > 0)
                {
                    if (slidermin >= 512)
                        slidermin = 1024;
                    else
                        slidermin = 512;
                }
                else if (slidermin > 1024)
                {
                    slidermin = 1024;
                }
                vars.SliderRamMin = (long)slidermin;
                vars.SliderRamMax = (long)max;
                vars.SliderRamValue = (long)(max / 2);
                settingsPage = new SettingsPage();
                if (vars.LoadedRam != 0)
                {
                    settingsPage.SetRam(vars.LoadedRam);
                    vars.CurrentRam = vars.LoadedRam;
                }
                else
                {
                    settingsPage.btnAutoRAM_Click(null, null);
                }
            }
            else
            {
               await new MessageBoxEx("Error", "Failed to get ram", MessageBoxEx.Buttons.Ok).ShowAsync();
            }
        }

        private void Page_Loading(FrameworkElement sender, object args)
        {
            var titleBar = ApplicationView.GetForCurrentView().TitleBar;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

            var coreTitleBar = CoreApplication.GetCurrentView().TitleBar;
            coreTitleBar.ExtendViewIntoTitleBar = true;
            UpdateTitleBarLayout(coreTitleBar);
            Window.Current.SetTitleBar(AppTitleBar);
            coreTitleBar.LayoutMetricsChanged += CoreTitleBar_LayoutMetricsChanged;
            coreTitleBar.IsVisibleChanged += CoreTitleBar_IsVisibleChanged;
            Window.Current.Activated += Current_Activated;
            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = new TimeSpan(0, 0, 0, 0, 1);
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        private async void Timer_Tick(object sender, object e)
        {
            if (!string.IsNullOrEmpty(vars.UserName))
            {
                txtUsername.Text = vars.UserName;
                txtLogin.Text = vars.UserName;
                prpFly.DisplayName = vars.UserName;
                prpLogin.DisplayName = vars.UserName;
                btnLogin.Tag = "Change";
            }
            else
            {
                txtUsername.Text = "Login";
                txtLogin.Text = "Login";
                prpFly.DisplayName = "";
                prpLogin.DisplayName = "";
                btnLogin.Tag = "Login";
            }
            if (vars.closing)
            {
                vars.closing = false;
                await settings.CreateSettingsFile(true);
            }
        }

        private void CoreTitleBar_LayoutMetricsChanged(CoreApplicationViewTitleBar sender, object args)
        {
            UpdateTitleBarLayout(sender);
        }

        private void CoreTitleBar_IsVisibleChanged(CoreApplicationViewTitleBar sender, object args)
        {
            if (sender.IsVisible)
            {

                AppTitleBar.Visibility = Visibility.Visible;
            }
            else
            {

                AppTitleBar.Visibility = Visibility.Collapsed;
            }
        }

        private void Current_Activated(object sender, WindowActivatedEventArgs e)
        {
            //if (e.WindowActivationState == Windows.UI.Core.CoreWindowActivationState.Deactivated)
            //{

            //    AppTitle.Foreground = disabletxt.Foreground;
            //}
            //else
            //{

            //    AppTitle.Foreground = enabletxt.Foreground;
            //}
        }

        private void UpdateTitleBarLayout(CoreApplicationViewTitleBar coreTitleBar)
        {
            AppTitleBar.Height = coreTitleBar.Height;

            Thickness currMargin = AppTitleBar.Margin;
        }

        [Obsolete]
        private void btnSettings_Click(object sender, RoutedEventArgs e)
        {
            loginFly.Hide();
            btnBack.Visibility = Visibility.Visible;
            pnlTitle.Margin = new Thickness(50, pnlTitle.Margin.Top, pnlTitle.Margin.Right, pnlTitle.Margin.Bottom);
            MainFrame.Content = settingsPage;
            settingsPage.ScrollteToTop();
        }

        Login login = new Login();
        private void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            loginFly.Hide();
            if (btnLogin.Tag.ToString() != "Change")
            {
                login.Hide();
                login = new Login();
                login.ShowAsync();
            }
            else {
                login.Hide();
                login = new Login() { Title = "Change Account" };
                login.ShowAsync();
            }
        }

        private void btnChat_Click(object sender, RoutedEventArgs e)
        {
            loginFly.Hide();
            if (discordFixed.Visibility != Visibility.Visible)
                Canvas.SetZIndex(discordView, 9);
            discordView.IsPaneOpen = true;
        }

        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            btnBack.Visibility = Visibility.Collapsed;
            pnlTitle.Margin = new Thickness(0, pnlTitle.Margin.Top, pnlTitle.Margin.Right, pnlTitle.Margin.Bottom);
            MainFrame.Content = launcher;            
        }

        private void MainFrame_Navigated(object sender, NavigationEventArgs e)
        {
            if (MainFrame.CanGoBack)
            {
            }
            else
            {
            }
        }

        private void tipacc_CloseButtonClick(Microsoft.UI.Xaml.Controls.TeachingTip sender, object args)
        {
            vars.ShowLaunchTips = true;
        }

        private void Page_ActualThemeChanged(FrameworkElement sender, object args)
        {
            if (this.ActualTheme == ElementTheme.Light)
            {
                imgDiscord.Source = new BitmapImage(new Uri("ms-appx:///Assets/Discord/discord.jpg"));
                imgBack.ImageSource = new BitmapImage(new Uri("ms-appx:///Assets/BackDrops/bg-light.png"));
            }
            if (this.ActualTheme == ElementTheme.Dark)
            {
                imgDiscord.Source = new BitmapImage(new Uri("ms-appx:///Assets/Discord/discord-dark.png"));
                imgBack.ImageSource = new BitmapImage(new Uri("ms-appx:///Assets/BackDrops/bg.jpg"));
            }
        }

        private async void Page_Unloaded(object sender, RoutedEventArgs e)
        {
           await new MessageBoxEx("Meow", "e", MessageBoxEx.Buttons.Ok).ShowAsync();
        }

        private void btnPinDiscord_Click(object sender, RoutedEventArgs e)
        {
            Canvas.SetZIndex(discordView, 1);
            vars.IsFixedDiscord = true;
            discordView.IsPaneOpen = false;
            btnUnPinDiscord.IsChecked = true;
            discordFixed.Visibility = Visibility.Visible;
            try
            {
                wv2DiscordFixed.CoreWebView2.Navigate(wv2Discord.Source.ToString());
            }
            catch
            {

            }
        }

        private void btnUnPinDiscord_Click(object sender, RoutedEventArgs e)
        {
            Canvas.SetZIndex(discordView, 9);
            wv2DiscordFixed.CoreWebView2.Navigate(wv2DiscordFixed.Source.ToString());
            vars.IsFixedDiscord = false;
            discordView.IsPaneOpen = true;
            btnPinDiscord.IsChecked = false;
            discordFixed.Visibility = Visibility.Collapsed;
        }

        private void discordView_PaneClosed(SplitView sender, object args)
        {
            Canvas.SetZIndex(discordView, 1);
        }
    }
}
