using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using CommunityToolkit.Mvvm.DependencyInjection;
using Emerald.CoreX.Helpers;
using Emerald.CoreX.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage.Pickers;
namespace Emerald.UserControls;
public sealed partial class ArgumentsListView : UserControl
{
    public ObservableCollection<string> Args
    {
        get => (ObservableCollection<string>)GetValue(ArgsProperty);
        set => SetValue(ArgsProperty, value);
    }

    public static readonly DependencyProperty ArgsProperty =
        DependencyProperty.Register(
            nameof(Args),
            typeof(ObservableCollection<string>),
            typeof(ArgumentsListView),
            new PropertyMetadata(new ObservableCollection<string>())
        );

    public ArgumentsListView()
    {
        InitializeComponent();
        view.ItemsSource = Args;
    }

    private void btnAdd_Click(object sender, RoutedEventArgs e)
    {
        Args.Add(string.Empty);
        view.SelectedIndex = Args.Count - 1;
    }

    private void btnRemove_Click(object sender, RoutedEventArgs e)
    {
        foreach (var selected in view.SelectedItems.Cast<string>().ToList())
            Args.Remove(selected);
    }

    private void view_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        btnRemove.IsEnabled = view.SelectedItems.Any();
    }
}
