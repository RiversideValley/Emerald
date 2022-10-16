﻿using Microsoft.UI.Xaml;
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
using Emerald.WinUI.Models;
using Emerald.WinUI.UserControls;
using Emerald.WinUI.Helpers;
using Emerald.WinUI.Views;
using Emerald.Core.Tasks;
// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Emerald.WinUI
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public static Views.HomePage HomePage { get; private set; }
        public static UserControls.TaskView TaskView { get; private set; } = new();
        public static Frame MainFrame { get; private set; }
        private InfoBadge TasksInfoBadge = new() { Value = 0 };
        public MainWindow()
        {
            this.InitializeComponent();
            Title = "Emerald";
            Initialize();
        }
        public void Initialize()
        {
            MainFrame = frame;
            NavView.MenuItems.Add(new NavViewItem() { Content = "Home".ToLocalizedString(), IconGlyph = "\uE10F" });
            NavView.MenuItems.Add(new NavViewItem() { Content = "Store".ToLocalizedString(), IconGlyph = "\uE14D" });
            NavView.FooterMenuItems.Add(new NavViewItem() { Content = "Tasks".ToLocalizedString(), IconGlyph = "\xe9d5",InfoBadge = TasksInfoBadge });
            NavView.FooterMenuItems.Add(new NavViewItem() { Content = "Logs".ToLocalizedString(), IconGlyph = "\xe756" });
            NavView.Header = new NavViewHeader() { HeaderText = "Home".ToLocalizedString(), HeaderMargin = GetNavViewHeaderMargin() };
            NavView.DisplayModeChanged += (_, _) => (NavView.Header as NavViewHeader).HeaderMargin = GetNavViewHeaderMargin();
            WindowManager.SetTitleBar(this, AppTitleBar);
            WinUIEx.WindowManager.Get(this).MinHeight = 400;
            WinUIEx.WindowManager.Get(this).MinWidth = 500;
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
                    TaskView.ChangeSeverty(ID.Value, e.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error);
                }
            };
            HomePage = new();
            MainFrame.Content = HomePage;

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
        private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (!args.IsSettingsInvoked)
            {
                var h = (NavView.SelectedItem as NavViewItem).Content.ToString();
                if(h == "Home".ToLocalizedString())
                {
                    MainFrame.Content = HomePage;
                }
                else if (h == "Store".ToLocalizedString())
                {
                    
                }
                else if (h == "Tasks".ToLocalizedString())
                {
                    MainFrame.Content = TaskView;
                    TasksInfoBadge.Value = 0;
                }
                else if (h == "Logs".ToLocalizedString())
                {

                }
                UpdateTasksInfoBadge();
                (NavView.Header as NavViewHeader).HeaderText = h;
                (NavView.Header as NavViewHeader).HeaderMargin = GetNavViewHeaderMargin();
            }
        }
    }
}
