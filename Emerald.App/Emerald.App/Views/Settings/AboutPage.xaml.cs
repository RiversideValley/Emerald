using CommunityToolkit.WinUI.UI.Controls;
using Emerald.Core;
using Emerald.WinUI.Enums;
using Emerald.WinUI.Helpers;
using Emerald.WinUI.Helpers.Settings.JSON;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using SS = Emerald.WinUI.Helpers.Settings.SettingsSystem;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Emerald.WinUI.Views.Settings
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class AboutPage : Page, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void Set<T>(ref T obj, T value, string name = null)
        {
            obj = value;
            InvokePropertyChanged(name);
        }
        private void InvokePropertyChanged(string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        private bool _LoadBackupCMDBar = false;
        private bool LoadBackupCMDBar
        {
            get => _LoadBackupCMDBar;
            set => Set(ref _LoadBackupCMDBar, value);
        }


        public AboutPage()
        {
            InitializeComponent();
            Start();
        }

        private async void Start()
        {
            tglWinHello.IsEnabled = await WindowsHello.IsAvailable();
            tglWinHello.IsOn = SS.Settings.App.WindowsHello;
            UpdateBackupList();
        }
        private async void UpdateBackupList()
        {
            lvBackups.ItemsSource = null;
            lvBackups.ItemsSource = await SS.GetBackups();
        }
        private void Version_Click(object sender, RoutedEventArgs e)
        {
            vTip.IsOpen = true;
        }

        private void vTip_ActionButtonClick(TeachingTip sender, object args)
        {
            vTip.IsOpen = false;
        }

        private void vTip_CloseButtonClick(TeachingTip sender, object args)
        {
            var VerData = new DataPackage();
            VerData.RequestedOperation = DataPackageOperation.Copy;
            VerData.SetText($"{"Version".Localize()}: {DirectResoucres.AppVersion}\n{"BuildType".Localize()}: {DirectResoucres.BuildType}");
            Clipboard.SetContent(VerData);
        }

        private void lvBackups_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
            LoadBackupCMDBar = lvBackups.SelectedItems.Any();

        private async void CreateBackup_Click(object sender, RoutedEventArgs e)
        {
            if (await WindowsHelloResult())
            {
                await SS.CreateBackup(SS.Settings.Serialize());
                UpdateBackupList();
            }
        }
        private async Task<bool> WindowsHelloResult() =>
            !SS.Settings.App.WindowsHello || (SS.Settings.App.WindowsHello && (!await WindowsHello.IsAvailable() || WindowsHello.IsRecentlyAuthenticated(5) || await WindowsHello.Authenticate()));
        private async void ToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (SS.Settings.App.WindowsHello && !tglWinHello.IsOn)
            {
                if (!await WindowsHello.Authenticate())
                {
                    tglWinHello.IsOn = true;
                    _ = MessageBox.Show("Error".Localize("Settings"), "WinHelloChangeFailed".Localize("Settings"), MessageBoxButtons.Ok);
                }
                else
                {
                    SS.Settings.App.WindowsHello = false;
                }
            }
            else
            {
                SS.Settings.App.WindowsHello = true;
            }
        }

        private async void DeleteBackup_Click(object sender, RoutedEventArgs e)
        {
            if (await WindowsHelloResult())
            {
                foreach (var itm in lvBackups.SelectedItems.Cast<SettingsBackup>())
                {
                    await SS.DeleteBackup(itm.Time);
                }
                UpdateBackupList();
            }
        }

        private async void ViewBackup_Click(object sender, RoutedEventArgs e)
        {
            if (await WindowsHelloResult())
            {
                var b = (SettingsBackup)lvBackups.SelectedItems[0];
                _ = new ScrollViewer()
                {
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Content = new MarkdownTextBlock()
                    {
                        CornerRadius = new() { TopLeft = 7, BottomLeft = 7, BottomRight = 7, TopRight = 7 },
                        Padding = new(0),
                        Text = $"```json\n{b.Backup}\n```",
                        TextWrapping = TextWrapping.WrapWholeWords
                    }
                }.ToContentDialog($"{b.Time.ToLongDateString()} {b.Time.ToShortTimeString()}", Localized.OK.Localize()).ShowAsync();
            }
        }

        private async void LoadBackup_Click(object sender, RoutedEventArgs e)
        {
            if (await WindowsHelloResult())
            {
                var b = (SettingsBackup)lvBackups.SelectedItems[0];
            }
        }
    }
}
