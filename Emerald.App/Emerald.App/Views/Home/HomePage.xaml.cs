using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Downloader;
using CommunityToolkit.WinUI.Helpers;
using Emerald.Core;
using Emerald.WinUI.Enums;
using Emerald.WinUI.Helpers;
using Emerald.WinUI.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using ProjBobcat.Class.Model.Optifine;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Windows.ApplicationModel.Core;
using Windows.Storage.Pickers;
using SS = Emerald.WinUI.Helpers.Settings.SettingsSystem;
using Task = System.Threading.Tasks.Task;

namespace Emerald.WinUI.Views.Home
{
    public sealed partial class HomePage : Page, INotifyPropertyChanged
    {
        public Process GameProcess { get; private set; }

        public AccountsPage AccountsPage { get; private set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        public void Set<T>(ref T obj, T value, string name = null)
        {
            obj = value;
            InvokePropertyChanged(name);
        }

        public void InvokePropertyChanged(string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private MSession _Session;
        public MSession Session { get => _Session; set => Set(ref _Session, value); }

        private string _Logs = "";
        public string Logs { get => _Logs; set => Set(ref _Logs, value ?? "", nameof(Logs)); }

        private bool _paneIsOpen = false;
        public bool PaneIsOpen
        {
            get => _paneIsOpen;
            set
            {
                Set(ref _paneIsOpen, value, nameof(PaneIsOpen));
                VersionsSelectorPanelColumnDefinition.Width = value ? new(364) : new(0);
            }
        }

        public Account SessionAsAccount
        {
            get => Session == null ? new MSession(Localized.Login.Localize(), "fake", null).ToAccount(false) : Session.ToAccount(false);
        }

        public HomePage()
        {
            InitializeComponent();

            Loaded += InitializeWhenLoad;
        }

        private void InitializeWhenLoad(object sender, RoutedEventArgs e)
            => Initialize();

        public async void Initialize()
        {
            Loaded -= InitializeWhenLoad;
            MinecraftPath mcP;
            bool retryMC = true;
            while (retryMC)
            {
                try
                {
                    mcP = new(SS.Settings.Minecraft.Path);
                    retryMC = false;
                }
                catch
                {
                    var r = await MessageBox.Show("Error".Localize(), "MCPathFailed".Localize().Replace("{Path}", SS.Settings.Minecraft.Path), MessageBoxButtons.CustomWithCancel, "Yes".Localize(), "SetDifMCPath".Localize());
                    if (r == MessageBoxResults.Cancel)
                        Process.GetCurrentProcess().Kill(); // Application.Current.Exit() didn't kill the process

                    else if (r == MessageBoxResults.CustomResult2)
                    {
                        var fop = new FolderPicker
                        {
                            CommitButtonText = "Select".Localize()
                        };
                        WinRT.Interop.InitializeWithWindow.Initialize(fop, WinRT.Interop.WindowNative.GetWindowHandle(App.Current.MainWindow));
                        var f = await fop.PickSingleFolderAsync();

                        if (f != null)
                            SS.Settings.Minecraft.Path = f.Path;
                    }
                }

            }
            App.Current.Launcher.InitializeLauncher(new MinecraftPath(SS.Settings.Minecraft.Path));

            SS.Settings.Minecraft.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == "Path")
                {
                    App.Current.Launcher.InitializeLauncher(new MinecraftPath(SS.Settings.Minecraft.Path));
                    VersionButton.Content = new MCVersionsCreator().GetNotSelectedVersion();
                    _ = App.Current.Launcher.RefreshVersions();
                }
            };

            App.Current.Launcher.VersionsRefreshed += Launcher_VersionsRefreshed;

            VersionButton.Content = new MCVersionsCreator().GetNotSelectedVersion();

            _ = App.Current.Launcher.RefreshVersions();

            AccountsPage = new();

            AccountsPage.BackRequested += (_, _) =>
            {
                PrimaryFrameGrid.Visibility = Visibility.Visible;
                SecondaryFrame.Visibility = Visibility.Collapsed;
                AccountsPage.UpdateMainSource();
            };

            AccountsPage.AccountLogged += (_, _) =>
            {
                PrimaryFrameGrid.Visibility = Visibility.Visible;
                SecondaryFrame.Visibility = Visibility.Collapsed;
                AccountsPage.UpdateMainSource();
            };

            if (SS.Accounts != null && SS.Settings.App.AutoLogin)
            {
                AccountsPage.UpdateSource();

                var l = AccountsPage.Accounts.Where(x => x.Last);
                Session = l.Any() ? l.FirstOrDefault().ToMSession() : null;
            }

            if (SystemInformation.Instance.IsFirstRun)
                ShowTips();

        }

        public void ShowTips()
        {
            var tip = new TeachingTip()
            {
                IsLightDismissEnabled = true,
                Title = Localized.Welcome.Localize(),
                Subtitle = Localized.NewHere.Localize(),
                ActionButtonContent = Localized.Sure.Localize(),
                CloseButtonContent = Localized.NoThanks.Localize(),
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

        private async void Launcher_VersionsRefreshed(object sender, Core.Args.VersionsRefreshedEventArgs e)
        {
            if (e.Success)
            {
                UpdateVerTreeSource();
            }
            else
            {
                if (!App.Current.Launcher.UseOfflineLoader)
                {
                    var r = await MessageBox.Show(Localized.Error.Localize(), Localized.RefreshVerFailed.Localize(), MessageBoxButtons.Custom, Localized.Retry.Localize(), Localized.SwitchOffline.Localize());
                    // MessageBox.Show(Helpers.Settings.SettingsSystem.Serialize());
                    if (r == MessageBoxResults.CustomResult1)
                    {
                        _ = App.Current.Launcher.RefreshVersions();
                    }
                    else
                    {
                        App.Current.Launcher.SwitchToOffilineMode();
                        _ = App.Current.Launcher.RefreshVersions();
                    }
                }
                else
                {
                    await MessageBox.Show(Localized.Error.Localize(), Localized.UnexpectedRestart.Localize(), MessageBoxButtons.Ok);
                    await CoreApplication.RequestRestartAsync("");
                }
            }
        }

        private void VersionButton_Click(object sender, RoutedEventArgs e)
        {
            PaneIsOpen = !PaneIsOpen;

            if (PaneIsOpen)
            {
                txtbxFindVer.Focus(FocusState.Programmatic);
            }

            UpdateVerTreeSource();
        }

        private void tglMitVerSort_Click(object sender, RoutedEventArgs e) =>
            UpdateVerTreeSource();

        private void UpdateVerTreeSource()
        {
            btnVerSort.IsEnabled = !App.Current.Launcher.UseOfflineLoader;
            txtVerOfflineMode.Visibility = App.Current.Launcher.UseOfflineLoader ? Visibility.Visible : Visibility.Collapsed;
            treeVer.ItemsSource = null;
            treeVer.ItemsSource = new MCVersionsCreator().CreateVersions();
            txtEmptyVers.Visibility = !(treeVer.ItemsSource as ObservableCollection<MinecraftVersion>).Any() ? Visibility.Visible : Visibility.Collapsed;
        }

        private void txtbxFindVer_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (txtbxFindVer.Text.IsNullEmptyOrWhiteSpace())
            {
                treeVer.ItemsSource = new MCVersionsCreator().CreateVersions();
                return;
            }
            var vers = new MCVersionsCreator().CreateVersions();
            var suitableItems = new ObservableCollection<MinecraftVersion>();
            var splitText = sender.Text.ToLower().Split(" ");

            void FindSubVers(MinecraftVersion ver)
            {
                foreach (var v in ver.SubVersions)
                {
                    FindSubVers(v);
                    var found = splitText.All((key) => v.Version.ToLower().Contains(key));
                    if (found)
                        suitableItems.Add(v);

                }
            }

            foreach (var ver in vers)
            {
                var found = splitText.All((key) => ver.Version.ToLower().Contains(key));
                if (found)
                    suitableItems.Add(ver);
                FindSubVers(ver);
            }

            treeVer.ItemsSource = suitableItems;

            txtEmptyVers.Visibility = !(treeVer.ItemsSource as IEnumerable<MinecraftVersion>).Any() ? Visibility.Visible : Visibility.Collapsed;
        }

        private void treeVer_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
        {
            VersionButton.Content = ((MinecraftVersion)args.InvokedItem).SubVersions.Count > 0 ? VersionButton.Content : ((MinecraftVersion)args.InvokedItem);
            if (((MinecraftVersion)args.InvokedItem).SubVersions.Count == 0)
            {
                PaneIsOpen = false;
            }
        }

        private void AccountButton_Click(object sender, RoutedEventArgs e)
        {
            SecondaryFrame.Content = AccountsPage;
            PrimaryFrameGrid.Visibility = Visibility.Collapsed;
            SecondaryFrame.Visibility = Visibility.Visible;
            AccountsPage.UpdateSource();
        }

        private bool UI(bool value)
            => App.Current.Launcher.UIState = value;

        private async Task Launch()
        {
            UI(false);
            var verItm = VersionButton.Content as MinecraftVersion;
            var ver = verItm.GetLaunchVersion();
            if (verItm.Version.StartsWith("fabricMC"))
            {
                await App.Current.Launcher.InitializeFabric(verItm.DisplayVersion.Replace(" Fabric", ""), ver);
            }
            else if (verItm.MISC != null)
            {
                if (verItm.MISC is OptifineDownloadVersionModel model)
                {
                    var r = await App.Current.Launcher.ConfigureOptifine(model);
                    if (r)
                        ver = model.ToFullVersion();
                    else
                        return;
                }
            }
            if (DirectResoucres.MinRAM == 0) { _ = await MessageBox.Show(Localized.Error.Localize(), Localized.WrongRAM.Localize(), MessageBoxButtons.Ok); return; }
            if (SS.Settings.Minecraft.RAM == 0) { _ = await MessageBox.Show(Localized.Error.Localize(), Localized.WrongRAM.Localize(), MessageBoxButtons.Ok); return; }
            App.Current.Launcher.Launcher.FileDownloader = new AsyncParallelDownloader();
            var l = new MLaunchOption
            {
                MinimumRamMb = DirectResoucres.MinRAM,
                MaximumRamMb = SS.Settings.Minecraft.RAM,
                Session = Session,
            };
            var w = SS.Settings.Minecraft.JVM.ScreenWidth;
            var h = SS.Settings.Minecraft.JVM.ScreenHeight;
            if (SS.Settings.Minecraft.JVM.SetSize)
            {
                l.ScreenWidth = Convert.ToInt32(w);
                l.ScreenHeight = Convert.ToInt32(h);
            }
            l.FullScreen = SS.Settings.Minecraft.JVM.FullScreen;
            l.JVMArguments = SS.Settings.Minecraft.JVM.Arguments;

            var process = await App.Current.Launcher.CreateProcessAsync(ver, l, true, !SS.Settings.Minecraft.Downloader.AssetsCheck, !SS.Settings.Minecraft.Downloader.HashCheck);

            if (process != null)
                StartProcess(process);

            UI(true);
        }

        private async void LaunchButton_Click(object sender, RoutedEventArgs e)
        {
            var verItm = VersionButton.Content as MinecraftVersion;
            var ver = verItm.GetLaunchVersion();

            if (Session == null)
            {
                if (await MessageBox.Show(Localized.Error.Localize(), Localized.BegLogIn.Localize(), MessageBoxButtons.Ok) == MessageBoxResults.Ok)
                {
                    void Cancel(object sender, EventArgs e)
                    {
                        AccountsPage.AccountLogged -= LoggedIn;
                        AccountsPage.BackRequested -= Cancel;
                    }

                    void LoggedIn(object sender, EventArgs e)
                    {
                        AccountsPage.AccountLogged -= LoggedIn;
                        AccountsPage.BackRequested -= Cancel;
                        LaunchButton_Click(null, null);
                    }

                    AccountsPage.AccountLogged += LoggedIn;
                    AccountsPage.BackRequested += Cancel;

                    AccountButton_Click(null, null);
                    UI(true);
                }

                UI(true);

                return;
            }
            if (ver == null)
            {
                UI(true);

                _ = await MessageBox.Show(Localized.Error.Localize(), Localized.BegVer.Localize(), MessageBoxButtons.Ok);

                VersionButton_Click(null, null);

                return;
            }
            await Launch();
        }

        private void StartProcess(Process process)
        {
            GameProcess = process;
            GameProcess.StartInfo.UseShellExecute = SS.Settings.Minecraft.IsAdmin ? true : GameProcess.StartInfo.UseShellExecute;
            GameProcess.StartInfo.Verb = SS.Settings.Minecraft.IsAdmin ? "runas" : GameProcess.StartInfo.Verb;
            if (SS.Settings.Minecraft.ReadLogs())
            {
                GameProcess.EnableRaisingEvents = true;
                GameProcess.StartInfo.RedirectStandardOutput = true;
                GameProcess.StartInfo.RedirectStandardError = true;
                GameProcess.OutputDataReceived += (s, e) => App.Current.MainWindow.DispatcherQueue.TryEnqueue(() => Logs += "\n" + e.Data);
                GameProcess.ErrorDataReceived += (s, e) => App.Current.MainWindow.DispatcherQueue.TryEnqueue(() => Logs += "\n" + e.Data);
            }
            var t = new Thread(async () =>
            {
                App.Current.MainWindow.DispatcherQueue.TryEnqueue(() => App.Current.Launcher.GameRuns = true);
                GameProcess.Start();
                if (SS.Settings.Minecraft.ReadLogs())
                {
                    GameProcess.BeginErrorReadLine();
                    GameProcess.BeginOutputReadLine();
                }
                await GameProcess.WaitForExitAsync();
                App.Current.MainWindow.DispatcherQueue.TryEnqueue(() => App.Current.Launcher.GameRuns = false);
            });
            t.Start();
        }

        private void NewsButton_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.MainNavigationView.SelectedItem = MainWindow.MainNavigationView.MenuItems[2];
            MainWindow.InvokeNavigate("News".Localize());
        }

        private void btnCloseVerPane_Click(object sender, RoutedEventArgs e)
        {
            PaneIsOpen = false;
        }

        private void btnRefreshVers_Click(object sender, RoutedEventArgs e)
        {
            SS.Settings.Minecraft.InvokePropertyChanged("Path");
        }
        private void AdaptiveItemPane_OnStacked(object sender, EventArgs e)
        {
            AccountButton.HorizontalContentAlignment = HorizontalAlignment.Center;
            AccountButton.Padding = new(24, 6, 0, 6);

            var r = (AccountButton.ContentTemplateRoot as UserControls.AdaptiveItemPane);
            r.OnlyStacked = true;
            r.Update();
            var s = (r.MiddlePane as StackPanel);
            s.VerticalAlignment = VerticalAlignment.Top;
            s.Children.OfType<TextBlock>().ToList().ForEach(x => x.HorizontalAlignment = HorizontalAlignment.Center);

            NewsButton.HorizontalContentAlignment = ChangelogsButton.HorizontalContentAlignment = HorizontalAlignment.Center;
        }

        private void AdaptiveItemPane_OnStretched(object sender, EventArgs e)
        {
            AccountButton.HorizontalContentAlignment = HorizontalAlignment.Left;
            AccountButton.Padding = new(6);

            var r = (AccountButton.ContentTemplateRoot as UserControls.AdaptiveItemPane);
            r.OnlyStacked = false;
            r.Update();
            var s = (r.MiddlePane as StackPanel);
            s.VerticalAlignment = VerticalAlignment.Center;
            s.Children.OfType<TextBlock>().ToList().ForEach(x => x.HorizontalAlignment = HorizontalAlignment.Left);

            NewsButton.HorizontalContentAlignment = ChangelogsButton.HorizontalContentAlignment = HorizontalAlignment.Left;

        }
    }
}
