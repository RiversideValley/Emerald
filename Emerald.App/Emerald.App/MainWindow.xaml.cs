using Emerald.Core;
using Emerald.Core.Tasks;
using Emerald.WinUI.Helpers;
using Emerald.WinUI.Helpers.Settings.JSON;
using Emerald.WinUI.Helpers.Updater;
using Emerald.WinUI.Models;
using Emerald.WinUI.UserControls;
using Emerald.WinUI.Views;
using Emerald.WinUI.Views.Home;
using Emerald.WinUI.Views.Store;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using System;
using System.Linq;
using Windows.UI;
using SS = Emerald.WinUI.Helpers.Settings.SettingsSystem;
using Window = Microsoft.UI.Xaml.Window;

namespace Emerald.WinUI
{
    public sealed partial class MainWindow : Window
    {
        public static Views.Home.HomePage HomePage { get; private set; }

        public static Color BGTintColor { get; private set; }

        public static TaskView TaskView { get; private set; } = new();

        private static Flyout TaskViewFlyout = new();

        public static Frame MainFrame { get; private set; }
        public static NavigationView MainNavigationView { get; private set; }

        public InfoBadge TasksInfoBadge { get; private set; } = new() { Value = 0 };

        private (bool WantBackup, string Backup) BackupState = (false, "");

        /// <summary>
        /// <para>Item1 - The Index.<br/>
        /// Item2 - The source (0-Menu, 1-Footer, 2-Unknown).</para>
        /// </summary>
        private static (int Index, int Source) SelectedItemIndex;

        public MainWindow()
        {
            InitializeComponent();
            Title = "Emerald";

            MainNavigationView = NavView;
            MainNavigationView.ItemInvoked += MainNavigationView_ItemInvoked;
            MainFrame = frame;
            SS.APINoMatch += (_, e) => BackupState = (true, e);
            SS.LoadData();

            (Content as FrameworkElement).Loaded += Initialize;
        }
        public async void ShowMiniTask(string title, string message, InfoBarSeverity severity)
        {
            MiniTaskInfo.Title = title;
            MiniTaskInfo.Message = message;
            MiniTaskInfo.Severity = severity;

            MiniTaskInfo.Visibility = Visibility.Visible;
            await System.Threading.Tasks.Task.Delay(new TimeSpan(0, 0, 3));
            MiniTaskInfo.Visibility = Visibility.Collapsed;
        }
        public async void Initialize(object s, RoutedEventArgs e)
        {
            MicaBackground mica = WindowManager.IntializeWindow(this);

            if (mica != null)
            {
                mica.MicaController.Kind = (MicaKind)SS.Settings.App.Appearance.MicaType;
                SS.Settings.App.Appearance.PropertyChanged += (s, e)
                    => mica.MicaController.Kind = (MicaKind)SS.Settings.App.Appearance.MicaType;
            }
            MainNavigationView.Header = new NavViewHeader() { HeaderText = "Home".Localize(), HeaderMargin = GetNavViewHeaderMargin() };
            MainNavigationView.DisplayModeChanged += (_, _) => (MainNavigationView.Header as NavViewHeader).HeaderMargin = GetNavViewHeaderMargin();

            WindowManager.SetTitleBar(this, AppTitleBar);

            WinUIEx.WindowManager.Get(this).MinHeight = 400;
            WinUIEx.WindowManager.Get(this).MinWidth = 500;

            if (BackupState.WantBackup)
            {

                var r = await MessageBox.Show(
                     Localized.Error.Localize(),
                     Localized.LoadSettingsFailed.Localize(),
                     Enums.MessageBoxButtons.Custom,
                     Localized.OK.Localize(),
                     Localized.CreateOldSettingsBackup.Localize());

                if (r == Enums.MessageBoxResults.CustomResult2)
                {
                    _ = SS.CreateBackup(BackupState.Backup);
                }
            }

            MainNavigationView.SelectedItem = MainNavigationView.MenuItems[0];

            void Tasks()
            {
                var g = new TaskViewGrid(TaskView);

                g.ClearAllClicked += (_, _)
                    => TaskView.ClearAll();

                TaskViewFlyout.Content = g;
                TaskViewFlyout.Closed += (s, e)
                    => MainNavigationView.SelectedItem = SelectedItemIndex.Source == 0 ?
                    MainNavigationView.MenuItems[SelectedItemIndex.Index] :
                    (SelectedItemIndex.Source == 1 ?
                        MainNavigationView.FooterMenuItems[SelectedItemIndex.Index] :
                        MainNavigationView.SelectedItem);

                TasksHelper.TaskAddRequested += (_, e) =>
                {
                    if (e is TaskAddRequestedEventArgs task)
                    {
                        var c = string.Join(" ", (task.Name ?? "").Split(" ").Select(s => s.Localize()));
                        var id = TaskView.AddProgressTask(c, 0, InfoBarSeverity.Informational, true, task.ID);
                        if (task.Message != null)
                            TaskView.ChangeDescription(id, string.Join(" ", (task.Message ?? "").Split(" ").Select(s => s.Localize())));

                        TasksInfoBadge.Value++;
                        UpdateTasksInfoBadge();
                        ShowMiniTask(c, null, InfoBarSeverity.Informational);
                    }
                    else if (e is ProgressTaskEventArgs Ptask)
                    {
                        var c = string.Join(" ", (Ptask.Name ?? "").Split(" ").Select(s => s.Localize()));
                        var val = Ptask.Value / Ptask.MaxValue * 100;
                        TaskView.AddProgressTask(c, val, InfoBarSeverity.Informational, false, Ptask.ID);
                        TasksInfoBadge.Value++;
                        ShowMiniTask(c, null, InfoBarSeverity.Informational);
                        UpdateTasksInfoBadge();
                    }
                };

                TasksHelper.ProgressTaskEditRequested += (_, e) =>
                {
                    TaskView.ChangeDescription(e.ID, string.Join(" ", (e.Message ?? "").Split(" ").Select(s => s.Localize())));
                    TaskView.ChangeProgress(e.ID, e.Value);
                };

                TasksHelper.TaskCompleteRequested += (_, e) =>
                {
                    var c = TaskView.AllTasks.FirstOrDefault(t => t.ID == e.ID).Content;
                    TaskView.ChangeProgress(e.ID, 100);
                    TaskView.ChangeIndeterminate(e.ID, false);
                    TaskView.ChangeDescription(e.ID, string.Join(" ", (e.Message ?? "").Split(" ").Select(s => s.Localize())));
                    TaskView.ChangeSeverty(e.ID, e.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error);
                    ShowMiniTask(c, string.Join(" ", (e.Message ?? "").Split(" ").Select(s => s.Localize())), e.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error);

                };
            }

            Tasks();

            void Settings()
            {
                void TintColor()
                {
                    switch ((Helpers.Settings.Enums.MicaTintColor)SS.Settings.App.Appearance.MicaTintColor)
                    {
                        case Helpers.Settings.Enums.MicaTintColor.NoColor:
                            MainGrid.Background = null;
                            break;
                        case Helpers.Settings.Enums.MicaTintColor.AccentColor:
                            MainGrid.Background = new SolidColorBrush((Color)Application.Current.Resources["SystemAccentColor"])
                            {
                                Opacity = 0.1
                            };
                            break;
                        case Helpers.Settings.Enums.MicaTintColor.CustomColor:
                            var c = SS.Settings.App.Appearance.CustomMicaTintColor;
                            MainGrid.Background = new SolidColorBrush() { Color = c == null ? Color.FromArgb(255, 234, 0, 94) : Color.FromArgb((byte)c.Value.A, (byte)c.Value.R, (byte)c.Value.G, (byte)c.Value.B), Opacity = 0.1 };
                            break;
                    }
                }

                SS.Settings.App.Appearance.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName != null)
                    {
                        TintColor();
                        (Content as FrameworkElement).RequestedTheme = (ElementTheme)SS.Settings.App.Appearance.Theme;
                    }
                };

                (Content as FrameworkElement).ActualThemeChanged += (s, e) => SS.Settings.App.Appearance.InvokePropertyChanged();

                TintColor();
                (Content as FrameworkElement).RequestedTheme = (ElementTheme)SS.Settings.App.Appearance.Theme;
                SS.Settings.App.Appearance.InvokePropertyChanged();
            }

            Settings();

            HomePage = new();
            MainFrame.Content = HomePage;
            App.Current.Launcher.PropertyChanged += (_, _) =>
            {
                UpdateUI();
            };

            if (SS.Settings.App.Updates.CheckAtStartup)
                App.Current.Updater.CheckForUpdates(true);
            (Content as FrameworkElement).Loaded -= Initialize;
        }
        private static void UpdateUI()
        {
            var t = MainFrame.Content.GetType();
            MainFrame.IsEnabled = t == typeof(LogsPage) || t == typeof(NewsPage) || t == typeof(StorePage) || App.Current.Launcher.UIState;
        }
        public void UpdateTasksInfoBadge() =>
            TasksInfoBadge.Visibility = MainFrame.Content == TaskView || TasksInfoBadge.Value == 0 ? Visibility.Collapsed : Visibility.Visible;

        private static Thickness GetNavViewHeaderMargin()
        {
            if (MainNavigationView.DisplayMode == NavigationViewDisplayMode.Minimal)
            {
                MainNavigationView.IsPaneToggleButtonVisible = true;
                return new Thickness(35, -40, 0, 0);
            }
            else
            {
                MainNavigationView.IsPaneToggleButtonVisible = false;
                return new Thickness(-30, -20, 0, 10);
            }
        }

        private static void UpdateSelectedItem() =>
            SelectedItemIndex = MainNavigationView.SelectedItem is SquareNavigationViewItem item ?
                (
                 ((MainNavigationView.SelectedItem as SquareNavigationViewItem).Name.ToString() == "Tasks".Localize()) ?
                    SelectedItemIndex
                    :
                    (MainNavigationView.MenuItems.IndexOf(MainNavigationView.MenuItems.FirstOrDefault(x => (SquareNavigationViewItem)x == item)) == -1 ?
                        (
                         MainNavigationView.FooterMenuItems.IndexOf(MainNavigationView.FooterMenuItems.FirstOrDefault(x => (SquareNavigationViewItem)x == item)) == -1 ?
                            (1, 2)
                            :
                            (MainNavigationView.FooterMenuItems.IndexOf(MainNavigationView.FooterMenuItems.FirstOrDefault(x => (SquareNavigationViewItem)x == item)), 1)
                         )
                        :
                        (MainNavigationView.MenuItems.IndexOf(MainNavigationView.MenuItems.FirstOrDefault(x => (SquareNavigationViewItem)x == item)), 0))
                )
                : (1, 2);
        public static void InvokeNavigate(string h, NavigationViewItemInvokedEventArgs args = null)
        {
            void NavigateOnce(Type type)
            {
                if (MainFrame.Content == null || MainFrame.Content.GetType() != type)
                {
                    MainFrame.Navigate(type);
                }
            }

            try
            {
                if (h == "Home".Localize())
                {
                    MainFrame.Content = HomePage;
                }
                else if (h == "Store".Localize())
                {
                    NavigateOnce(typeof(Views.Store.StorePage));
                }
                else if (h == "Tasks".Localize() && args != null)
                {
                    TaskViewFlyout.ShowAt(args.InvokedItemContainer, new() { Placement = FlyoutPlacementMode.Right, ShowMode = FlyoutShowMode.Standard });
                    (App.Current.MainWindow as MainWindow).TasksInfoBadge.Value = 0;
                }
                else if (h == "Logs".Localize())
                {
                    NavigateOnce(typeof(Views.LogsPage));
                }
                else if (h == "Settings".Localize())
                {
                    NavigateOnce(typeof(Views.Settings.SettingsPage));
                }
                else if (h == "News".Localize())
                {
                    NavigateOnce(typeof(Views.Home.NewsPage));
                }

                (App.Current.MainWindow as MainWindow).UpdateTasksInfoBadge();
                UpdateUI();
                (MainNavigationView.Header as NavViewHeader).HeaderText = h == "Tasks".Localize() ? (MainNavigationView.Header as NavViewHeader).HeaderText : h;
                (MainNavigationView.Header as NavViewHeader).HeaderMargin = GetNavViewHeaderMargin();
            }
            catch
            {
            }

            var pitm = (SquareNavigationViewItem)(SelectedItemIndex.Source == 0 ?
                    MainNavigationView.MenuItems[SelectedItemIndex.Index] :
                    (SelectedItemIndex.Source == 1 ?
                        MainNavigationView.FooterMenuItems[SelectedItemIndex.Index] :
                        MainNavigationView.SelectedItem));
            pitm.IsSelected = pitm == MainNavigationView.SelectedItem as SquareNavigationViewItem;

            UpdateSelectedItem();
        }
        private static void MainNavigationView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (!args.IsSettingsInvoked && MainNavigationView.SelectedItem is SquareNavigationViewItem itm)
                InvokeNavigate(itm.Name.ToString(), args);
        }
    }
}
