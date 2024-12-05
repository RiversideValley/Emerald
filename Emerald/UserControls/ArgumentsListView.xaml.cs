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
using System.Collections.ObjectModel;
using Emerald.Models;
using CommonServiceLocator;


namespace Emerald.UserControls;

    //Copied from Emerald.UWP
    public sealed partial class ArgumentsListView : UserControl
    {
        private int count = 0;
    private readonly Helpers.Settings.SettingsSystem SS;
        public ArgumentsListView()
        {
        SS = ServiceLocator.Current.GetInstance<Helpers.Settings.SettingsSystem>();
            InitializeComponent();
            view.ItemsSource = Source;
            UpdateSource();
        }

        private ObservableCollection<ArgTemplate> Source = new();
        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            count++;
            var r = new ArgTemplate { Arg = "", Count = count };
            Source.Add(r);
            UpdateMainSource();
            view.SelectedItem = r;
        }
        public void UpdateSource()
        {
            Source.Clear();
            if (SS.Settings.Minecraft.JVM.Arguments != null)
            {
                foreach (var item in SS.Settings.Minecraft.JVM.Arguments)
                {
                    count++;
                    var r = new ArgTemplate { Arg = item, Count = count };
                    r.PropertyChanged += (_, _) =>
                    {
                        UpdateMainSource();
                    };
                    Source.Add(r);
                }
            }
            btnRemove.IsEnabled = Source.Any();
        }
        private void UpdateMainSource() =>
            SS.Settings.Minecraft.JVM.Arguments = Source.Select(x => x.Arg).ToArray();

        private void btnRemove_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in view.SelectedItems)
            {
                Source.Remove((ArgTemplate)item);
            }
            UpdateMainSource();
        }

        private void TextBox_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            view.SelectedIndex = Source.IndexOf(Source.FirstOrDefault(x => x.Count == ((sender as FrameworkElement).DataContext as ArgTemplate).Count));
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            view.SelectedIndex = Source.IndexOf(Source.FirstOrDefault(x => x.Count == ((sender as FrameworkElement).DataContext as ArgTemplate).Count));
            UpdateMainSource();
        }

        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            view.SelectedIndex = Source.IndexOf(Source.FirstOrDefault(x => x.Count == ((sender as FrameworkElement).DataContext as ArgTemplate).Count));
        }

        private void view_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            btnRemove.IsEnabled = view.SelectedItems.Any();
        }
    }
