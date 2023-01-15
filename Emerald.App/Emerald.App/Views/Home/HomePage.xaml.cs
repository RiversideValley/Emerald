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
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using SS = Emerald.WinUI.Helpers.Settings.SettingsSystem;

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

        public void Initialize()
        {
            Loaded -= InitializeWhenLoad;

            App.Launcher.InitializeLauncher(new MinecraftPath(SS.Settings.Minecraft.Path));

            SS.Settings.Minecraft.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == "Path")
                {
                    App.Launcher.InitializeLauncher(new MinecraftPath(SS.Settings.Minecraft.Path));
                    VersionButton.Content = new MCVersionsCreator().GetNotSelectedVersion();
                    _ = App.Launcher.RefreshVersions();
                }
            };

            App.Launcher.VersionsRefreshed += Launcher_VersionsRefreshed;

            VersionButton.Content = new MCVersionsCreator().GetNotSelectedVersion();

            _ = App.Launcher.RefreshVersions();

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

            if (SS.Settings.Minecraft.Accounts != null && SS.Settings.App.AutoLogin)
            {
                AccountsPage.UpdateSource();

                var l = AccountsPage.Accounts.Where(x => x.Last);
                Session = l.Any() ? l.FirstOrDefault().ToMSession() : null;
            }

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
                if (!App.Launcher.UseOfflineLoader)
                {
                    var r = await MessageBox.Show(Localized.Error.Localize(), Localized.RefreshVerFailed.Localize(), MessageBoxButtons.Custom, Localized.Retry.Localize(), Localized.SwitchOffline.Localize());
                    // MessageBox.Show(Helpers.Settings.SettingsSystem.Serialize());
                    if (r == MessageBoxResults.CustomResult1)
                    {
                        _ = App.Launcher.RefreshVersions();
                    }
                    else
                    {
                        App.Launcher.SwitchToOffilineMode();
                        _ = App.Launcher.RefreshVersions();
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
            btnVerSort.IsEnabled = !App.Launcher.UseOfflineLoader;
            txtVerOfflineMode.Visibility = App.Launcher.UseOfflineLoader ? Visibility.Visible : Visibility.Collapsed;
            treeVer.ItemsSource = null;
            treeVer.ItemsSource = new MCVersionsCreator().CreateVersions();
            txtEmptyVers.Visibility = !(treeVer.ItemsSource as ObservableCollection<Models.MinecraftVersion>).Any() ? Visibility.Visible : Visibility.Collapsed;
        }

        private void txtbxFindVer_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            var vers = new MCVersionsCreator().CreateVersions();
            var suitableItems = new ObservableCollection<Models.MinecraftVersion>();
            var splitText = sender.Text.ToLower().Split(" ");
            foreach (var ver in vers)
            {
                var found = splitText.All((key) =>
                {
                    return ver.Version.ToLower().Contains(key);
                });
                if (found)
                {
                    suitableItems.Add(ver);
                }
            }
            if (string.IsNullOrEmpty(txtbxFindVer.Text))
            {
                treeVer.ItemsSource = new MCVersionsCreator().CreateVersions();
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
            => App.Launcher.UIState = value;

        private async Task Launch()
        {
            UI(false);
            var verItm = (VersionButton.Content as Models.MinecraftVersion);
            var ver = verItm.GetLaunchVersion();
            if (verItm.Version.StartsWith("fabricMC"))
            {
                await App.Launcher.InitializeFabric(verItm.DisplayVersion.Replace(" Fabric", ""), ver);
            }
            else if (verItm.MISC != null)
            {
                if (verItm.MISC is OptifineDownloadVersionModel model)
                {
                    var r = await App.Launcher.ConfigureOptifine(model);
                    if (r)
                        ver = model.ToFullVersion();
                    else
                        return;
                }
            }
            if (DirectResoucres.MinRAM == 0) { _ = await MessageBox.Show(Localized.Error.Localize(), Localized.WrongRAM.Localize(), MessageBoxButtons.Ok); return; }
            if (SS.Settings.Minecraft.RAM == 0) { _ = await MessageBox.Show(Localized.Error.Localize(), Localized.WrongRAM.Localize(), MessageBoxButtons.Ok); return; }
            App.Launcher.Launcher.FileDownloader = new AsyncParallelDownloader();
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
            var process = await App.Launcher.CreateProcessAsync(ver, l);
            if (process != null)
            {
                StartProcess(process);
            }
            UI(true);
        }

        private async void LaunchButton_Click(object sender, RoutedEventArgs e)
        {

            var verItm = (VersionButton.Content as Models.MinecraftVersion);
            var ver = verItm.GetLaunchVersion();
            if (Session == null)
            {
                if (await MessageBox.Show(Localized.Error.Localize(), Localized.BegLogIn.Localize(), MessageBoxButtons.OkCancel) == MessageBoxResults.Ok)
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
            if (SS.Settings.Minecraft.ReadLogs())
            {
                GameProcess.EnableRaisingEvents = true;
                GameProcess.StartInfo.RedirectStandardOutput = true;
                GameProcess.StartInfo.RedirectStandardError = true;
                GameProcess.OutputDataReceived += (s, e) => App.MainWindow.DispatcherQueue.TryEnqueue(() => Logs += "\n" + e.Data);
                GameProcess.ErrorDataReceived += (s, e) => App.MainWindow.DispatcherQueue.TryEnqueue(() => Logs += "\n" + e.Data);
            }
            var t = new Thread(async () => 
            {
                App.MainWindow.DispatcherQueue.TryEnqueue(() => App.Launcher.GameRuns = true);
                GameProcess.Start();
                if (SS.Settings.Minecraft.ReadLogs())
                {
                    GameProcess.BeginErrorReadLine();
                    GameProcess.BeginOutputReadLine();
                }
                await GameProcess.WaitForExitAsync();
                App.MainWindow.DispatcherQueue.TryEnqueue(() => App.Launcher.GameRuns = false);
            });
            t.Start();
        }

        private void NewsButton_Click(object sender, RoutedEventArgs e)
        {
            var n = new NewsPage();
            n.BackRequested += (_, _) =>
            {
                SecondaryFrame.Content = null;
                PrimaryFrameGrid.Visibility = Visibility.Visible;
                SecondaryFrame.Visibility = Visibility.Collapsed;
            };
            SecondaryFrame.Content= n;
            PrimaryFrameGrid.Visibility = Visibility.Collapsed;
            SecondaryFrame.Visibility = Visibility.Visible;
        }

        private void btnCloseVerPane_Click(object sender, RoutedEventArgs e)
        {
            PaneIsOpen = false;
        }
    }
}
