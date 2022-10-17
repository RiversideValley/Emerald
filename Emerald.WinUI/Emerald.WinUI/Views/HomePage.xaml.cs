using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Emerald.Core;
using System.Threading.Tasks;
using CmlLib.Core;
using Windows.Storage;
using Emerald.WinUI.Helpers;
using System.Collections.ObjectModel;
using Windows.ApplicationModel.Core;
using Emerald.WinUI.Enums;
// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Emerald.WinUI.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class HomePage : Page
    {
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
            this.Loaded -= InitializeWhenLoad;
        }

        private async void Launcher_VersionsRefreshed(object sender, Core.Args.VersionsRefreshedEventArgs e)
        {
            if (e.Success)
            {
                treeVer.ItemsSource = MCVersionsCreator.CreateVersions();
                txtEmptyVers.Visibility = !(treeVer.ItemsSource as IEnumerable<Models.MinecraftVersion>).Any() ? Visibility.Visible : Visibility.Collapsed;

            }
            else
            {
                if (!MainCore.Launcher.UseOfflineLoader)
                {
                    var r = await MessageBox.Show(Localized.Error.ToLocalizedString(), Localized.RefreshVerFailed.ToLocalizedString(), MessageBoxButtons.Custom, Localized.Retry.ToLocalizedString(), Localized.SwitchOffline.ToLocalizedString());
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
                    await MessageBox.Show(Localized.Error, Localized.UnexpectedRestart, MessageBoxButtons.Ok);
                    await CoreApplication.RequestRestartAsync("");
                }
            }
        }

        private void VersionButton_Click(object sender, RoutedEventArgs e)
        {
            paneVersions.IsPaneOpen = true;
            treeVer.ItemsSource = MCVersionsCreator.CreateVersions();
            txtEmptyVers.Visibility = !(treeVer.ItemsSource as IEnumerable<Models.MinecraftVersion>).Any() ? Visibility.Visible : Visibility.Collapsed;
        }

        private void tglMitVerSort_Click(object sender, RoutedEventArgs e)
        {
            var mit = sender as ToggleMenuFlyoutItem;
            if(mit.Text == "Release".ToLocalizedString())
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
            if(string.IsNullOrEmpty(txtbxFindVer.Text))
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
            if(((Models.MinecraftVersion)args.InvokedItem).SubVersions.Count == 0)
            {
                paneVersions.IsPaneOpen = false;
            }
        }
    }
}
