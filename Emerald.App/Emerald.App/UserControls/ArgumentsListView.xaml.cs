// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Emerald.WinUI.Models;
using SS = Emerald.WinUI.Helpers.Settings.SettingsSystem;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Emerald.WinUI.UserControls
{
    //Copied from Emerald.UWP
    public sealed partial class ArgumentsListView : UserControl
    {
        private int count = 0;
        public ArgumentsListView()
        {
            this.InitializeComponent();
            view.ItemsSource = source;
            UpdateSource();
        }


        private List<ArgTemplate> source = new List<ArgTemplate>();
        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            count++;
            var r = new ArgTemplate { Arg = "", Count = count };
            source.Add(r);
            RefreshView();
            view.SelectedItem = r;
        }
        public void UpdateSource()
        {
            source = new List<ArgTemplate>();
            if (SS.Settings.Minecraft.JVM.Arguments != null)
            {
                foreach (var item in SS.Settings.Minecraft.JVM.Arguments)
                {
                    count++;
                    var r = new ArgTemplate { Arg = item, Count = count };
                    source.Add(r);
                }
            }
            btnRemove.IsEnabled = source.Count != 0;
            RefreshView();
        }
        private void RefreshView()
        {
            view.ItemsSource = null;
            UpdateMainSource();
            view.ItemsSource = source;
        }
        private void UpdateMainSource() =>
            SS.Settings.Minecraft.JVM.Arguments = source.Select(x => x.Arg).ToArray();
        
        private void btnRemove_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in view.SelectedItems)
            {
                source.Remove((ArgTemplate)item);
            }
            RefreshView();
        }

        private void TextBox_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (sender is TextBox bx)
            {
                foreach (var item in source)
                {
                    if (item.Count == int.Parse(bx.Tag.ToString()))
                    {
                        view.SelectedItem = null;
                        view.SelectedItem = item;
                        return;
                    }
                }
            }
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

            if (sender is TextBox bx)
            {
                foreach (var item in source)
                {
                    if (item.Count == int.Parse(bx.Tag.ToString()))
                    {
                        item.Arg = bx.Text;
                        view.SelectedItem = null;
                        view.SelectedItem = item;
                        UpdateMainSource();
                        return;
                    }
                }
            }
        }

        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {

            if (sender is TextBox bx)
            {
                foreach (var item in source)
                {
                    if (item.Count == int.Parse(bx.Tag.ToString()))
                    {
                        view.SelectedItem = null;
                        view.SelectedItem = item;
                        return;
                    }
                }
            }
        }

        private void view_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            btnRemove.IsEnabled = view.SelectedItems.Count != 0;
        }
    }
}