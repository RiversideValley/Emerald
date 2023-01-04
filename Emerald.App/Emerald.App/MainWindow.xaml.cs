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
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
            MainFrame = frame;
            SS.APINoMatch += (_, e) => BackupState = (true, e);
            SS.LoadData();
            Title = "Emerald";
            (this.Content as FrameworkElement).Loaded += Initialize;
        }
        private (bool WantBackup, string Backup) BackupState = (false, "");
        public async void Initialize(object s, RoutedEventArgs e)
        {
            MicaBackground mica = WindowManager.IntializeWindow(this);
            mica.MicaController.Kind = (MicaKind)SS.Settings.App.Appearance.MicaType;
            SS.Settings.App.Appearance.PropertyChanged += (s, e) =>
                mica.MicaController.Kind = (MicaKind)SS.Settings.App.Appearance.MicaType;
            NavView.Header = new NavViewHeader() { HeaderText = "Home".ToLocalizedString(), HeaderMargin = GetNavViewHeaderMargin() };
            NavView.DisplayModeChanged += (_, _) => (NavView.Header as NavViewHeader).HeaderMargin = GetNavViewHeaderMargin();
            WindowManager.SetTitleBar(this, AppTitleBar);
            WinUIEx.WindowManager.Get(this).MinHeight = 400;
            WinUIEx.WindowManager.Get(this).MinWidth = 500;
            if (BackupState.WantBackup)
            {

                var r = await MessageBox.Show(
                     Localized.Error.ToLocalizedString(),
                     Localized.LoadSettingsFailed.ToLocalizedString(),
                     Enums.MessageBoxButtons.Custom,
                     Localized.OK.ToLocalizedString(),
                     Localized.CreateOldSettingsBackup.ToLocalizedString());
                if (r == Enums.MessageBoxResults.CustomResult2)
                {
                    SS.CreateBackup(BackupState.Backup);
                }
            }
            NavView.SelectedItem = NavView.MenuItems[0];
            void Tasks()
            {
                var g = new TaskViewGrid(TaskView);
                g.ClearAllClicked += (_, _) => TaskView.ClearAll();
                TaskViewFlyout.Content = g;
                TaskViewFlyout.Closed += (s, e) =>
                NavView.SelectedItem = SelectedItemIndex.Source == 0 ?
                    NavView.MenuItems[SelectedItemIndex.Index] :
                    (SelectedItemIndex.Source == 1 ?
                        NavView.FooterMenuItems[SelectedItemIndex.Index] :
                        NavView.SelectedItem);
                TasksHelper.TaskAddRequested += (_, e) =>
                        {
                            if (e is TaskAddRequestedEventArgs task)
                            {
                                TaskView.AddProgressTask(string.Join(" ", (task.Name ?? "").Split(" ").Select(s => s.ToLocalizedString())), 0, InfoBarSeverity.Informational, true, task.ID);
                                TasksInfoBadge.Value++;
                                UpdateTasksInfoBadge();
                            }
                            else if(e is ProgressTaskEventArgs Ptask)
                            { 
                                var val = Ptask.Value / Ptask.MaxValue * 100;
                                TaskView.AddProgressTask(string.Join(" ", (Ptask.Name ?? "").Split(" ").Select(s => s.ToLocalizedString())), val, InfoBarSeverity.Informational, false, Ptask.ID);
                                TasksInfoBadge.Value++;
                                UpdateTasksInfoBadge();
                            }
                        };
                TasksHelper.ProgressTaskEditRequested += (_, e) =>
                {
                    var val = e.Value / e.MaxValue * 100;
                    int? ID = TaskView.SearchByUniqueThingsToString(e.ID.ToString()).First();
                    if (ID != null)
                    {
                        TaskView.ChangeDescription(ID.Value, string.Join(" ", (e.Message ?? "").Split(" ").Select(s => s.ToLocalizedString())));
                        TaskView.ChangeProgress(ID.Value, e.Value);
                    }
                };
                TasksHelper.TaskCompleteRequested += (_, e) =>
                {
                    int? ID = TaskView.SearchByUniqueThingsToString(e.ID.ToString()).First();
                    if (ID != null)
                    {
                        TaskView.ChangeProgress(ID.Value, 100);
                        TaskView.ChangeIndeterminate(ID.Value, false);
                        TaskView.ChangeDescription(ID.Value, string.Join(" ", (e.Message ?? "").Split(" ").Select(s => s.ToLocalizedString())));
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
        private (int Index, int Source) SelectedItemIndex;
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
            void NavigateOnce(Type type)
            {
                if (MainFrame.Content == null || MainFrame.Content.GetType() != type)
                {
                    MainFrame.Navigate(type);
                }
            }
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
                    {
                        NavigateOnce(typeof(Views.LogsPage));
                    }
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

                var pitm = ((SquareNavigationViewItem)(SelectedItemIndex.Source == 0 ?
                        NavView.MenuItems[SelectedItemIndex.Index] :
                        (SelectedItemIndex.Source == 1 ?
                            NavView.FooterMenuItems[SelectedItemIndex.Index] :
                            NavView.SelectedItem)));
                pitm.IsSelected = pitm == itm;
                UpdateSelectedItem();
            }
        }
    }
}
