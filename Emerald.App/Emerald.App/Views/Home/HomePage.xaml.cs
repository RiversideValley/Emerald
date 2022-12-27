using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Downloader;
using CommunityToolkit.WinUI.Helpers;
using Emerald.Core;
using Emerald.WinUI.Enums;
using Emerald.WinUI.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using ProjBobcat.Class.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net;
using Windows.ApplicationModel.Core;
using Windows.System;
using SS = Emerald.WinUI.Helpers.Settings.SettingsSystem;
namespace Emerald.WinUI.Views.Home
{
    public sealed partial class HomePage : Page
    {
        public static MSession Session { get; set; }
        public AccountsPage AccountsPage { get; private set; }
        public HomePage()
        {
            this.InitializeComponent();
            this.Loaded += InitializeWhenLoad;
        }

        private void InitializeWhenLoad(object sender, RoutedEventArgs e) => Initialize();

        public void Initialize()
        {
            this.Loaded -= InitializeWhenLoad;
            MainCore.Intialize();
            MainCore.Launcher.InitializeLauncher(new MinecraftPath(SS.Settings.Minecraft.Path));
            SS.Settings.Minecraft.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == "Path")
                {
                    MainCore.Launcher.InitializeLauncher(new MinecraftPath(SS.Settings.Minecraft.Path));
                    VersionButton.Content = MCVersionsCreator.GetNotSelectedVersion();
                    _ = MainCore.Launcher.RefreshVersions();
                }
            };
            MainCore.Launcher.VersionsRefreshed += Launcher_VersionsRefreshed;
            VersionButton.Content = MCVersionsCreator.GetNotSelectedVersion();
            btnCloseVerPane.Click += (_, _) => paneVersions.IsPaneOpen = false;
            _ = MainCore.Launcher.RefreshVersions();
            AccountsPage = new();
            if (SystemInformation.Instance.IsFirstRun)
            {
                ShowTips();
            }
        }
        public void ShowTips()
        {
            var tip = new TeachingTip()
            {
                IsLightDismissEnabled = true,
                Title = Localized.Welcome.ToLocalizedString(),
                Subtitle = Localized.NewHere.ToLocalizedString(),
                ActionButtonContent = Localized.Sure.ToLocalizedString(),
                CloseButtonContent = Localized.NoThanks.ToLocalizedString(),
                ActionButtonStyle = App.Current.Resources["AccentButtonStyle"] as Style,
                Background = App.Current.Resources["AcrylicInAppFillColorDefaultBrush"] as AcrylicBrush
            };
            tip.ActionButtonClick += (_, _) =>
            {
                tip.IsOpen = false;
                AccountTip.CloseButtonClick += (_, _) =>
                {
                    AccountTip.IsOpen = false;
                    VersionTip.CloseButtonClick += (_, _) =>
                    {
                        VersionTip.IsOpen = false;
                        VersionTip.CloseButtonClick += (_, _) =>
                        {
                            VersionTip.IsOpen = false;
                        };
                        LaunchTip.IsOpen = true;
                    };
                    VersionTip.IsOpen = true;
                };
                AccountTip.IsOpen = true;
            };
            tip.ShowAt(null);
        }
        public string GetLauncVer(bool returntag = false)
        {
            var s = (VersionButton.Content as Models.MinecraftVersion).Version;
            if (string.IsNullOrEmpty(s))
            {
                return null;
            }
            else
            {
                if (!returntag)
                {
                    if (s.StartsWith("vanilla-"))
                    {
                        s = s.Remove(0, 8);
                    }
                    else if (s.StartsWith("fabricMC-"))
                    {
                        s = s.Remove(0, 9);
                    }
                }
            }
            return s;
        }
        private async void Launcher_VersionsRefreshed(object sender, Core.Args.VersionsRefreshedEventArgs e)
        {
            if (e.Success)
            {
                UpdateVerTreeSource();
            }
            else
            {
                if (!MainCore.Launcher.UseOfflineLoader)
                {
                    var r = await MessageBox.Show(Localized.Error.ToLocalizedString(), Localized.RefreshVerFailed.ToLocalizedString(), MessageBoxButtons.Custom, Localized.Retry.ToLocalizedString(), Localized.SwitchOffline.ToLocalizedString());
                    // MessageBox.Show(Helpers.Settings.SettingsSystem.Serialize());
                    if (r == MessageBoxResults.CustomResult1)
                    {
                        _ = MainCore.Launcher.RefreshVersions();
                    }
                    else
                    {
                        MainCore.Launcher.SwitchToOffilineMode();
                        _ = MainCore.Launcher.RefreshVersions();
                    }
                }
                else
                {
                    await MessageBox.Show(Localized.Error.ToLocalizedString(), Localized.UnexpectedRestart.ToLocalizedString(), MessageBoxButtons.Ok);
                    await CoreApplication.RequestRestartAsync("");
                }
            }
        }

        private void VersionButton_Click(object sender, RoutedEventArgs e)
        {
            paneVersions.IsPaneOpen = !paneVersions.IsPaneOpen;
            if (paneVersions.IsPaneOpen)
            {
                txtbxFindVer.Focus(FocusState.Programmatic);
            }
            UpdateVerTreeSource();
        }

        private void tglMitVerSort_Click(object sender, RoutedEventArgs e) =>
            UpdateVerTreeSource();

        private void UpdateVerTreeSource()
        {
            btnVerSort.IsEnabled = !MainCore.Launcher.UseOfflineLoader;
            txtVerOfflineMode.Visibility = Core.MainCore.Launcher.UseOfflineLoader ? Visibility.Visible : Visibility.Collapsed;
            treeVer.ItemsSource = null;
            treeVer.ItemsSource = MCVersionsCreator.CreateVersions();
            txtEmptyVers.Visibility = !(treeVer.ItemsSource as IEnumerable<Models.MinecraftVersion>).Any() ? Visibility.Visible : Visibility.Collapsed;
        }

        private void txtbxFindVer_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            var suitableItems = new ObservableCollection<Models.MinecraftVersion>();
            var splitText = sender.Text.ToLower().Split(" ");
            foreach (var cat in MCVersionsCreator.CreateAllVersions())
            {
                var found = splitText.All((key) =>
                {
                    return cat.Version.ToLower().Contains(key);
                });
                if (found)
                {
                    suitableItems.Add(cat);
                }
            }
            if (string.IsNullOrEmpty(txtbxFindVer.Text))
            {
                treeVer.ItemsSource = MCVersionsCreator.CreateVersions();
            }
            else
            {
                treeVer.ItemsSource = suitableItems;
            }
            txtEmptyVers.Visibility = (treeVer.ItemsSource as IEnumerable<Models.MinecraftVersion>).Count() == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void treeVer_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
        {
            VersionButton.Content = ((Models.MinecraftVersion)args.InvokedItem).SubVersions.Count > 0 ? VersionButton.Content : ((Models.MinecraftVersion)args.InvokedItem);
            if (((Models.MinecraftVersion)args.InvokedItem).SubVersions.Count == 0)
            {
                paneVersions.IsPaneOpen = false;
            }
        }

        private void AccountButton_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.MainFrame.Content = AccountsPage;

            AccountsPage.Accounts.Add(MSession.GetOfflineSession("Noob").ToAccount());
        }
        private bool UI(bool value) => MainCore.Launcher.UIState = value;
        private async void LaunchButton_Click(object sender, RoutedEventArgs e)
        {
            var ver = (VersionButton.Content as Models.MinecraftVersion).GetLaunchVersion();
            if (Session == null)
            {
                if (await MessageBox.Show(Localized.Error.ToLocalizedString(), Localized.BegLogIn.ToLocalizedString(), MessageBoxButtons.OkCancel) == MessageBoxResults.Ok)
                {
                  //  _ = await new Login().ShowAsync();
                    UI(true);
                   // LaunchButton_Click(null, null);
                }
                UI(true);
                return;
            }
            if (ver == null)
            {
                UI(true);
                _ = await MessageBox.Show(Localized.Error.ToLocalizedString(), Localized.BegVer.ToLocalizedString(), MessageBoxButtons.Ok);
                VersionButton_Click(null, null);
                return;
            }
            if (DirectResoucres.MinRAM == 0) { _ = await MessageBox.Show(Localized.Error.ToLocalizedString(), Localized.WrongRAM.ToLocalizedString(), MessageBoxButtons.Ok); return; }
            if (SS.Settings.Minecraft.RAM == 0) { _ = await MessageBox.Show(Localized.Error.ToLocalizedString(), Localized.WrongRAM.ToLocalizedString(), MessageBoxButtons.Ok); return; }
            MainCore.Launcher.Launcher.FileDownloader = new AsyncParallelDownloader();
            var l = new MLaunchOption
            {
                MinimumRamMb = DirectResoucres.MinRAM,
                MaximumRamMb = SS.Settings.Minecraft.RAM,
                Session = Session,
            };

            if (SS.Settings.Minecraft.JVM.ScreenWidth != 0 && SS.Settings.Minecraft.JVM.ScreenHeight != 0)
            {
                l.ScreenWidth = SS.Settings.Minecraft.JVM.ScreenWidth;
                l.ScreenHeight = SS.Settings.Minecraft.JVM.ScreenHeight;
            }
            l.FullScreen = SS.Settings.Minecraft.JVM.FullScreen;
            l.JVMArguments = SS.Settings.Minecraft.JVM.Arguments;
            var process = await MainCore.Launcher.CreateProcessAsync(ver, l);
            if (process != null)
            {
                //StartProcess(process);
            }

            (await MainCore.Launcher.CreateProcessAsync(ver, new() { DockName = "Test", Session = MSession.GetOfflineSession("Noob"), MaximumRamMb = 4096, MinimumRamMb = 1024 })).Start();
        }
    }
}
