using Emerald.Core;
using Emerald.Core.Tasks;
using Emerald.WinUI.Helpers;
using Emerald.WinUI.Models;
using Emerald.WinUI.UserControls;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System;
using System.Linq;
using Windows.UI;
using SS = Emerald.WinUI.Helpers.Settings.SettingsSystem;

namespace Emerald.WinUI
{
    public sealed partial class MainWindow : Window
    {
        public static Views.Home.HomePage HomePage { get; private set; }
        public static Color BGTintColor { get; private set; }
        public static TaskView TaskView { get; private set; } = new();
        private Flyout TaskViewFlyout = new();
        public static Frame MainFrame { get; private set; }
        private InfoBadge TasksInfoBadge = new() { Value = 0 };
        public MainWindow()
        {
            this.InitializeComponent();
            Title = "Emerald";
            (this.Content as FrameworkElement).Loaded += Initialize;
        }
        public void Initialize(object s, RoutedEventArgs e)
        {
            MainFrame = frame;
            SS.APINoMatch += async (_, e) =>
            {
                var r = await MessageBox.Show(
                     Localized.Error.ToLocalizedString(),
                     Localized.LoadSettingsFailed.ToLocalizedString(),
                     Enums.MessageBoxButtons.Custom,
                     Localized.OK.ToLocalizedString(),
                     Localized.CreateOldSettingsBackup.ToLocalizedString());
                if (r == Enums.MessageBoxResults.CustomResult2)
                {
                    SS.CreateBackup(e);
                }
            };
            SS.LoadData();

            MicaBackground mica = WindowManager.IntializeWindow(this);
            mica.MicaController.Kind = (MicaKind)SS.Settings.App.Appearance.MicaType;
            SS.Settings.App.Appearance.PropertyChanged += (s, e) =>
                mica.MicaController.Kind = (MicaKind)SS.Settings.App.Appearance.MicaType;

            NavView.MenuItems.Add(new SquareNavigationViewItem("Home".ToLocalizedString(), true, new(new("ms-appx:///Assets/NavigationViewIcons/home.png"))));
            NavView.MenuItems.Add(new SquareNavigationViewItem("Store".ToLocalizedString(), false, new(new("ms-appx:///Assets/NavigationViewIcons/store.png"))));
            NavView.FooterMenuItems.Add(new SquareNavigationViewItem("Tasks".ToLocalizedString(), false, new(new("ms-appx:///Assets/NavigationViewIcons/tasks.png")), TasksInfoBadge));
            NavView.FooterMenuItems.Add(new SquareNavigationViewItem("Logs".ToLocalizedString(), false, new(new("ms-appx:///Assets/NavigationViewIcons/logs.png"))));
            NavView.FooterMenuItems.Add(new SquareNavigationViewItem("Settings".ToLocalizedString(), false, new(new("ms-appx:///Assets/NavigationViewIcons/settings.png"))));
            NavView.Header = new NavViewHeader() { HeaderText = "Home".ToLocalizedString(), HeaderMargin = GetNavViewHeaderMargin() };
            NavView.DisplayModeChanged += (_, _) => (NavView.Header as NavViewHeader).HeaderMargin = GetNavViewHeaderMargin();
            WindowManager.SetTitleBar(this, AppTitleBar);
            WinUIEx.WindowManager.Get(this).MinHeight = 400;
            WinUIEx.WindowManager.Get(this).MinWidth = 500;
            NavView.SelectedItem = NavView.MenuItems[0];
            void Tasks()
            {
                var g = new TaskViewGrid(TaskView);
                g.ClearAllClicked += (_, _) => TaskView.ClearAll();
                TaskViewFlyout.Content = g;
                TaskViewFlyout.Closed += (s, e) =>
                NavView.SelectedItem = SelectedItemIndex.Item2 == 0 ?
                    NavView.MenuItems[SelectedItemIndex.Item1] :
                    (SelectedItemIndex.Item2 == 1 ?
                        NavView.FooterMenuItems[SelectedItemIndex.Item1] :
                        NavView.SelectedItem);
                TasksHelper.TaskAddRequested += (_, e) =>
                        {
                            TaskView.AddProgressTask(e.Name.ToLocalizedString(), 0, InfoBarSeverity.Informational, true, e.TaskID);
                            TasksInfoBadge.Value++;
                            UpdateTasksInfoBadge();
                        };
                TasksHelper.TaskCompleteRequested += (_, e) =>
                {
                    int? ID = TaskView.SearchByUniqueThingsToString(e.ID.ToString()).First();
                    if (ID != null)
                    {
                        TaskView.ChangeProgress(ID.Value, 100);
                        TaskView.ChangeIndeterminate(ID.Value, false);
                        TaskView.ChangeDescription(ID.Value, e.Message);
                        TaskView.ChangeSeverty(ID.Value, e.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error);
                    }
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
                            MicaTintColorBrush.Color = Colors.Transparent;
                            BGTintColor = Colors.Transparent;
                            break;
                        case Helpers.Settings.Enums.MicaTintColor.AccentColor:
                            MicaTintColorBrush.Color = (Color)Application.Current.Resources["SystemAccentColor"];
                            BGTintColor = (Color)Application.Current.Resources["SystemAccentColor"];
                            break;
                        case Helpers.Settings.Enums.MicaTintColor.CustomColor:
                            var c = SS.Settings.App.Appearance.CustomMicaTintColor;
                            MicaTintColorBrush.Color = c == null ? Color.FromArgb(255, 234, 0, 94) : Color.FromArgb((byte)c.Value.A, (byte)c.Value.R, (byte)c.Value.G, (byte)c.Value.B);
                            BGTintColor = c == null ? Color.FromArgb(255, 234, 0, 94) : Color.FromArgb((byte)c.Value.A, (byte)c.Value.R, (byte)c.Value.G, (byte)c.Value.B);
                            break;
                    }
                }
                SS.Settings.App.Appearance.PropertyChanged += (s, e) =>
                {
                    TintColor();
                    (this.Content as FrameworkElement).RequestedTheme = (ElementTheme)SS.Settings.App.Appearance.Theme;

                };
                TintColor();
                (this.Content as FrameworkElement).RequestedTheme = (ElementTheme)SS.Settings.App.Appearance.Theme;
            }
            Settings();
            HomePage = new();
            MainFrame.Content = HomePage;
            (this.Content as FrameworkElement).Loaded -= Initialize;
        }
        private void UpdateTasksInfoBadge() =>
            TasksInfoBadge.Visibility = MainFrame.Content == TaskView || TasksInfoBadge.Value == 0 ? Visibility.Collapsed : Visibility.Visible;
        private Thickness GetNavViewHeaderMargin()
        {
            if (NavView.DisplayMode == NavigationViewDisplayMode.Minimal)
            {
                NavView.IsPaneToggleButtonVisible = true;
                return new Thickness(35, -40, 0, 0);
            }
            else
            {
                NavView.IsPaneToggleButtonVisible = false;
                return new Thickness(-30, -20, 0, 10);
            }
        }

        /// <summary>
        /// <para>Item1 - The Index.<br/>
        /// Item2 - The source (0-Menu, 1-Footer, 2-Unknown).</para>
        /// </summary>
        private (int, int) SelectedItemIndex;
        private void UpdateSelectedItem() =>
            SelectedItemIndex = NavView.SelectedItem is SquareNavigationViewItem item ?
                (
                 ((NavView.SelectedItem as SquareNavigationViewItem).Name.ToString() == "Tasks".ToLocalizedString()) ?
                    SelectedItemIndex
                    :
                    (NavView.MenuItems.IndexOf(NavView.MenuItems.FirstOrDefault(x => (SquareNavigationViewItem)x == item)) == -1 ?
                        (
                         NavView.FooterMenuItems.IndexOf(NavView.FooterMenuItems.FirstOrDefault(x => (SquareNavigationViewItem)x == item)) == -1 ?
                            (1, 2)
                            :
                            (NavView.FooterMenuItems.IndexOf(NavView.FooterMenuItems.FirstOrDefault(x => (SquareNavigationViewItem)x == item)), 1)
                         )
                        :
                        (NavView.MenuItems.IndexOf(NavView.MenuItems.FirstOrDefault(x => (SquareNavigationViewItem)x == item)), 0))
                )
                : (1, 2);
        private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (!args.IsSettingsInvoked && NavView.SelectedItem is SquareNavigationViewItem itm)
            {
                try
                {
                    var h = itm.Name.ToString();
                    if (h == "Home".ToLocalizedString())
                    {
                        MainFrame.Content = HomePage;
                    }
                    else if (h == "Store".ToLocalizedString())
                    { }
                    else if (h == "Tasks".ToLocalizedString())
                    {
                        TaskViewFlyout.ShowAt(args.InvokedItemContainer, new() { Placement = FlyoutPlacementMode.Right, ShowMode = FlyoutShowMode.Standard });
                        TasksInfoBadge.Value = 0;
                    }
                    else if (h == "Logs".ToLocalizedString())
                    { }
                    else if (h == "Settings".ToLocalizedString())
                    {
                        NavigateOnce(typeof(Views.Settings.SettingsPage));
                    }
                    UpdateTasksInfoBadge();
                    (NavView.Header as NavViewHeader).HeaderText = h == "Tasks".ToLocalizedString() ? (NavView.Header as NavViewHeader).HeaderText : h;
                    (NavView.Header as NavViewHeader).HeaderMargin = GetNavViewHeaderMargin();
                }
                catch
                {

                }

                var pitm = ((SquareNavigationViewItem)(SelectedItemIndex.Item2 == 0 ?
                        NavView.MenuItems[SelectedItemIndex.Item1] :
                        (SelectedItemIndex.Item2 == 1 ?
                            NavView.FooterMenuItems[SelectedItemIndex.Item1] :
                            NavView.SelectedItem)));
                pitm.IsSelected = pitm == itm;
                UpdateSelectedItem();
            }
        }
        private void NavigateOnce(Type type)
        {
            if (MainFrame.Content == null || MainFrame.Content.GetType() != type)
            {
                MainFrame.Navigate(type);
            }
        }
    }
}
