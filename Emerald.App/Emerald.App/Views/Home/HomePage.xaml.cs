using CmlLib.Core;
using CmlLib.Core.Auth;
using Emerald.Core;
using Emerald.WinUI.Enums;
using Emerald.WinUI.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.ApplicationModel.Core;
// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Emerald.WinUI.Views.Home
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class HomePage : Page
    {
        public AccountsPage AccountsPage { get; private set; }
        public HomePage()
        {
            this.InitializeComponent();
            this.Loaded += InitializeWhenLoad;
        }

        private void InitializeWhenLoad(object sender, RoutedEventArgs e) => Initialize();

        public void Initialize()
        {
            MainCore.Intialize();
            MainCore.Launcher.InitializeLauncher(new MinecraftPath(MinecraftPath.GetOSDefaultPath()));
            MainCore.Launcher.VersionsRefreshed += Launcher_VersionsRefreshed;
            VersionButton.Content = MCVersionsCreator.GetNotSelectedVersion();
            btnCloseVerPane.Click += (_, _) => paneVersions.IsPaneOpen = false;
            ToggleMenuFlyoutItem createItm(string name)
            {
                var itm = new ToggleMenuFlyoutItem();
                itm.Text = name;
                itm.Click += tglMitVerSort_Click;
                return itm;
            }
            btnVerSort.Flyout = new MenuFlyout()
            {
                Items =
                {
                    createItm("Release".ToLocalizedString()),
                    createItm("Snapshot".ToLocalizedString()),
                    createItm("OldBeta".ToLocalizedString()),
                    createItm("OldAlpha".ToLocalizedString()),
                    createItm("Custom".ToLocalizedString())
                }
            };
            _ = MainCore.Launcher.RefreshVersions();
            AccountsPage = new();
            this.Loaded -= InitializeWhenLoad;
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

        private void tglMitVerSort_Click(object sender, RoutedEventArgs e)
        {
            var mit = sender as ToggleMenuFlyoutItem;
            if (mit.Text == "Release".ToLocalizedString())
            {
                MCVersionsCreator.Configuration.Release = mit.IsChecked;
            }
            else if (mit.Text == "Snapshot".ToLocalizedString())
            {
                MCVersionsCreator.Configuration.Snapshot = mit.IsChecked;
            }
            else if (mit.Text == "Oldbeta".ToLocalizedString())
            {
                MCVersionsCreator.Configuration.OldBeta = mit.IsChecked;
            }
            else if (mit.Text == "OldAlpha".ToLocalizedString())
            {
                MCVersionsCreator.Configuration.OldAlpha = mit.IsChecked;
            }
            else if (mit.Text == "Custom".ToLocalizedString())
            {
                MCVersionsCreator.Configuration.Custom = mit.IsChecked;
            }
            UpdateVerTreeSource();
        }
        private void UpdateVerTreeSource()
        {
            btnVerSort.IsEnabled = !MainCore.Launcher.UseOfflineLoader;
            txtVerOfflineMode.Visibility = Core.MainCore.Launcher.UseOfflineLoader ? Visibility.Visible : Visibility.Collapsed;
            treeVer.ItemsSource = null;
            treeVer.ItemsSource = MCVersionsCreator.CreateVersions();
            txtEmptyVers.Visibility = !(treeVer.ItemsSource as IEnumerable<Models.MinecraftVersion>).Any() ? Visibility.Visible : Visibility.Collapsed;
        }
        private void btnVerSort_Click(object sender, RoutedEventArgs e)
        {
            var f = btnVerSort.Flyout as MenuFlyout;
            f.Items
                .Where(x => ((ToggleMenuFlyoutItem)x).Text == "Release".ToLocalizedString())
                .Select(x => (ToggleMenuFlyoutItem)x)
                .FirstOrDefault()
                .IsChecked = MCVersionsCreator.Configuration.Release;

            f.Items
                .Where(x => ((ToggleMenuFlyoutItem)x).Text == "Snapshot".ToLocalizedString())
                .Select(x => (ToggleMenuFlyoutItem)x)
                .FirstOrDefault()
                .IsChecked = MCVersionsCreator.Configuration.Snapshot;

            f.Items
                .Where(x => ((ToggleMenuFlyoutItem)x).Text == "OldBeta".ToLocalizedString())
                .Select(x => (ToggleMenuFlyoutItem)x)
                .FirstOrDefault()
                .IsChecked = MCVersionsCreator.Configuration.OldBeta;

            f.Items
                .Where(x => ((ToggleMenuFlyoutItem)x).Text == "OldAlpha".ToLocalizedString())
                .Select(x => (ToggleMenuFlyoutItem)x)
                .FirstOrDefault()
                .IsChecked = MCVersionsCreator.Configuration.OldAlpha;

            f.Items
                .Where(x => ((ToggleMenuFlyoutItem)x).Text == "Custom".ToLocalizedString())
                .Select(x => (ToggleMenuFlyoutItem)x)
                .FirstOrDefault()
                .IsChecked = MCVersionsCreator.Configuration.Custom;
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

        private int currentPage = 0;
        private void AccountButton_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.MainFrame.Content = AccountsPage;
            currentPage = 1;
            AccountsPage.Accounts.Add(MSession.GetOfflineSession("Noob").ToAccount());
        }

        private async void LaunchButton_Click(object sender, RoutedEventArgs e)
        {
            var ver = (VersionButton.Content as Models.MinecraftVersion).GetLaunchVersion();
            (await MainCore.Launcher.CreateProcessAsync(ver, new() { Session = MSession.GetOfflineSession("Noob"),MaximumRamMb = 4096,MinimumRamMb = 1024 })).Start();
        }
    }
}
