using Emerald.WinUI.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using SS = Emerald.WinUI.Helpers.Settings.SettingsSystem;

namespace Emerald.WinUI.UserControls
{
    //Copied from Emerald.UWP
    public sealed partial class ArgumentsListView : UserControl
    {
        private int count = 0;
        public ArgumentsListView()
        {
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
            UpdateMainSource();
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
}