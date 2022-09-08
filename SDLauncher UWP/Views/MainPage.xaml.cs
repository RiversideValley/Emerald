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
using SDLauncher.UWP.Views;
using Windows.UI.Xaml.Media.Imaging;
using Windows.Storage;
using CmlLib.Core;
using SDLauncher.Core;
using SDLauncher.Core.Tasks;
using SDLauncher.UWP.Resources;
using SDLauncher.Core.Args;
using SDLauncher.UWP.Helpers;
#pragma warning disable CS8305 // Type is for evaluation purposes only and is subject to change or removal in future updates.
#pragma warning disable CS8305 // Type is for evaluation purposes only and is subject to change or removal in future updates.
#pragma warning disable CS8305 // Type is for evaluation purposes only and is subject to change or removal in future updates.
#pragma warning disable CS8305 // Type is for evaluation purposes only and is subject to change or removal in future updates.


namespace SDLauncher.UWP
{
    public sealed partial class MainPage : Page
    {
        public BaseLauncherPage LauncherPage;
        public SettingsPage settingsPage;
        public MainPage()
        {
            this.InitializeComponent();
            MainCore.Intialize();
            MainCore.UIChanged += Core_UIChanged;
            LauncherPage = new BaseLauncherPage();
            settingsPage = new SettingsPage();
            MainCore.Launcher.InitializeLauncher(new MinecraftPath(ApplicationData.Current.LocalFolder.Path));
            TasksHelper.TaskAddRequested += (s, e) => tasks.AddTask(Localizer.GetLocalizedString( e.Name), e.TaskID);
            TasksHelper.TaskCompleteRequested += (s, e) => tasks.CompleteTask(e.ID,e.Success);

            settingsPage.UpdateBGRequested += SettingsPage_UpdateBGRequested;
            settingsPage.BackRequested += SettingsPage_BackRequested;
            vars.BackgroundUpdatd += Vars_BackgroundUpdatd;
            vars.SessionChanged += Vars_SessionChanged;
            Vars_SessionChanged(null, null);
            Vars_BackgroundUpdatd(null, null);
            if (!string.IsNullOrEmpty(vars.BackgroundImagePath))
            {
                settingsPage.GetAndSetBG();
            }
        }

        private void Core_UIChanged(object sender, Core.Args.UIChangeRequestedEventArgs e)
        {
            settingsPage.IsEnabled = e.UI;
            btnAccount.IsEnabled = e.UI;
        }

        private void Vars_SessionChanged(object sender, EventArgs e)
        {
            if (vars.session != null)
            {
                txtUsername.Text = vars.session.Username;
                txtLogin.Text = vars.session.Username;
                prpFly.DisplayName = vars.session.Username;
                prpLogin.DisplayName = vars.session.Username;
                prpLogin.DisplayName = vars.session.Username;
                if(vars.session.UUID == "user_uuid")
                {
                    prpFly.ProfilePicture = new BitmapImage(new Uri("https://minotar.net/helm/MHF_Steve"));
                    prpLogin.ProfilePicture = new BitmapImage(new Uri("https://minotar.net/helm/MHF_Steve"));
                }
                else
                {
                    prpFly.ProfilePicture = new BitmapImage(new Uri("https://minotar.net/helm/" + vars.session.UUID));
                    prpLogin.ProfilePicture = new BitmapImage(new Uri("https://minotar.net/helm/" + vars.session.UUID));
                }
                btnLogin.Tag = "Change";
            }
            else
            {
                txtUsername.Text = Helpers.Localizer.GetLocalizedString("MainPage_Login");
                txtLogin.Text = Helpers.Localizer.GetLocalizedString("MainPage_Login");
                prpFly.DisplayName = "";
                prpLogin.DisplayName = "";
                btnLogin.Tag = "Login";
            }
        }

        private void Vars_BackgroundUpdatd(object sender, EventArgs e)
        {
            if (vars.CustomBackground)
            {
                imgBack.ImageSource = vars.BackgroundImage;
            }
            else
            {
                Page_ActualThemeChanged(this, new EventArgs());
            }
        }

        private void TasksHelper_TaskCompleteRequested(object sender, TaskCompletedEventArgs e)
        {
            tasks.CompleteTask(e.ID,e.Success);
        }

        private void TasksHelper_TaskAddRequested(object sender, Core.Tasks.TaskAddRequestedEventArgs e)
        {
            tasks.AddTask(e.Name, e.TaskID);
        }

        private void SettingsPage_UpdateBGRequested(object sender, EventArgs e)
        {
            Page_ActualThemeChanged(null, null);
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            Page_ActualThemeChanged(null, null);
            MainFrame.Content = LauncherPage;
            if (vars.ShowTips)
            {
                tipacc.IsOpen = true;
            }
            if (vars.IsFixedDiscord)
            {
                btnPinDiscord_Click(null, null);
            }
            var computerMemory = Helpers.Util.GetMemoryMb() * 1024;
            if (computerMemory != null)
            {
                int max = (int)computerMemory;
                int min = max / 10;
                double slidermin = (long)(max / 7);
                vars.MinRam = min;
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
                vars.SliderRamMax = max;
                vars.SliderRamValue = max / 2;
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
               await MessageBox.Show(Localized.Error, Localized.RamFailed, MessageBoxButtons.Ok);
            }
            LauncherPage.InitializeLauncher();
            this.Loaded -= Page_Loaded;
        }

        private void SettingsPage_BackRequested(object sender, EventArgs e)
        {
            btnBack_Click(null, null);
        }


        public string localize(string key)
        {
            return Helpers.Localizer.GetLocalizedString(key);
        }
        private void Page_Loading(FrameworkElement sender, object args)
        { 
            var coreTitleBar = CoreApplication.GetCurrentView().TitleBar;
            coreTitleBar.ExtendViewIntoTitleBar = true;
            UpdateTitleBarLayout(coreTitleBar);
            Window.Current.SetTitleBar(AppTitleBar);
            coreTitleBar.LayoutMetricsChanged += CoreTitleBar_LayoutMetricsChanged;
            coreTitleBar.IsVisibleChanged += CoreTitleBar_IsVisibleChanged;
            Window.Current.Activated += Current_Activated;
        }

        private void CoreTitleBar_LayoutMetricsChanged(CoreApplicationViewTitleBar sender, object args)
        {
            UpdateTitleBarLayout(sender);
        }

        private void CoreTitleBar_IsVisibleChanged(CoreApplicationViewTitleBar sender, object args)
        {
            AppTitleBar.Visibility = sender.IsVisible ? Visibility.Visible : Visibility.Collapsed;
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

        private void btnSettings_Click(object sender, RoutedEventArgs e)
        {
            loginFly.Hide();
            btnBack.Visibility = Visibility.Visible;
            pnlTitle.Margin = new Thickness(40, pnlTitle.Margin.Top, pnlTitle.Margin.Right, pnlTitle.Margin.Bottom);
            MainFrame.Content = settingsPage;
            settingsPage.ScrollteToTop();
        }

        Login login = new Login();
        private void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            loginFly.Hide();
            login = new Login();
            _ = login.ShowAsync();
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
            MainFrame.Content = LauncherPage;        
        }

        private void MainFrame_Navigated(object sender, NavigationEventArgs e)
        {
        }

        private void tipacc_CloseButtonClick(Microsoft.UI.Xaml.Controls.TeachingTip sender, object args)
        {
            LauncherPage.ShowTips();
        }

        private void Page_ActualThemeChanged(FrameworkElement sender, object args)
        {
            var titleBar = ApplicationView.GetForCurrentView().TitleBar;
            if (this.ActualTheme == ElementTheme.Light)
            {
                titleBar.ButtonBackgroundColor = Colors.Transparent;
                titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                titleBar.ButtonPressedBackgroundColor = Colors.Transparent;
                titleBar.ButtonForegroundColor = Colors.Black;
                titleBar.ButtonHoverBackgroundColor = ((SolidColorBrush)Application.Current.Resources["LayerFillColorDefaultBrush"]).Color;
                    imgDiscord.Source = new BitmapImage(new Uri("ms-appx:///Assets/Discord/discord.jpg"));
                if (!vars.CustomBackground)
                {
                    imgBack.ImageSource = new BitmapImage(new Uri("ms-appx:///Assets/BackDrops/bg-light.png"));
                }
            }
            if (this.ActualTheme == ElementTheme.Dark)
            {
                titleBar.ButtonBackgroundColor = Colors.Transparent;
                titleBar.ButtonPressedBackgroundColor = Colors.Transparent;
                titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                titleBar.ButtonForegroundColor = Colors.White;
                titleBar.ButtonHoverBackgroundColor = ((SolidColorBrush)Application.Current.Resources["LayerFillColorDefaultBrush"]).Color;
                imgDiscord.Source = new BitmapImage(new Uri("ms-appx:///Assets/Discord/discord-dark.png"));
                if (!vars.CustomBackground)
                {
                    imgBack.ImageSource = new BitmapImage(new Uri("ms-appx:///Assets/BackDrops/bg.jpg"));
                }
            }
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
        }

        private void btnPinDiscord_Click(object sender, RoutedEventArgs e)
        {
            // await ApplicationView.GetForCurrentView().TryEnterViewModeAsync(ApplicationViewMode.CompactOverlay);
            Canvas.SetZIndex(discordView, 0);
            Canvas.SetZIndex(discordFixed, 10);
            vars.IsFixedDiscord = true;
            discordView.IsPaneOpen = false;
            btnUnPinDiscord.IsChecked = true;
            discordFixed.Visibility = Visibility.Visible;
            try
            {
                if (wv2Discord.Source.ToString() != wv2DiscordSticked.Source.ToString())
                {
                    wv2DiscordSticked.CoreWebView2.Navigate(wv2Discord.Source.ToString());
                }
            }
            catch
            {

            }
        }

        private void btnUnPinDiscord_Click(object sender, RoutedEventArgs e)
        {
            Canvas.SetZIndex(discordView, 10);
            if (wv2Discord.Source.ToString() != wv2DiscordSticked.Source.ToString())
            {
                wv2DiscordSticked.CoreWebView2.Navigate(wv2DiscordSticked.Source.ToString());
            }
            vars.IsFixedDiscord = false;
            discordView.IsPaneOpen = true;
            btnPinDiscord.IsChecked = false;
            discordFixed.Visibility = Visibility.Collapsed;
        }

        private void discordView_PaneClosed(SplitView sender, object args)
        {
            Canvas.SetZIndex(discordView, 0);
        }

        private void tasks_ErrorTaskRecieved(object sender, EventArgs e)
        {
            infbdgErrorTasks.Value = tasks.UnwatchedErrorTasksCount;
            if(tasks.UnwatchedErrorTasksCount > 0)
            {
                infbdgErrorTasks.Visibility = Visibility.Visible;
            }
            else
            {
                infbdgErrorTasks.Visibility = Visibility.Collapsed;
            }
        }

        private void btnTasks_Click(object sender, RoutedEventArgs e)
        {
            tasks.ClearUnwatchedErrorTasks();
            tasks_ErrorTaskRecieved(null, null);
            taskView.IsPaneOpen = true;
            Canvas.SetZIndex(taskView, 10);
        }

        private void taskView_PaneClosed(SplitView sender, object args)
        {
            Canvas.SetZIndex(taskView, 0);
        }
    }
}
