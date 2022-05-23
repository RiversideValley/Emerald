using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace SDLauncher_UWP.UserControls
{
    public sealed partial class ArgumentsListView : UserControl
    {
        private int count = 0;
        public ArgumentsListView()
        {
            this.InitializeComponent();
            view.ItemsSource = source;
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
        private void UpdateSource()
        {
            source = new List<ArgTemplate>();
            foreach (var item in vars.JVMArgs)
            {
                count++;
                var r = new ArgTemplate { Arg = item, Count = count };
                source.Add(r);
            }
        }
         private void RefreshView()
        {
            view.ItemsSource = null;
            view.ItemsSource = source;
        }
        private void UpdateMainSource()
        {
            vars.JVMArgs = new List<string>();
            foreach (var item in source)
            {
                vars.JVMArgs.Add(item.Arg);
            }
        }
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
            if(sender is TextBox bx)
            {
                foreach (var item in source)
                {
                    if(item.Count == int.Parse(bx.Tag.ToString()))
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
    }
    public class ArgTemplate
    {
        public string Arg { get; set; }
        public int Count { get; set; }
    }
}
