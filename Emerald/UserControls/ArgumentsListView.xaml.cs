using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Emerald.Models;

namespace Emerald.UserControls;

public sealed partial class ArgumentsListView : UserControl
{
    // Public API → strings
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
            new PropertyMetadata(new ObservableCollection<string>(), OnArgsChanged)
        );

    // Internal collection for binding
    private readonly ObservableCollection<LaunchArg> _internal = new();

    public ArgumentsListView()
    {
        InitializeComponent();
        view.ItemsSource = _internal;
    }

    private static void OnArgsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (ArgumentsListView)d;

        if (e.OldValue is ObservableCollection<string> oldCol)
            oldCol.CollectionChanged -= control.ExternalChanged;

        if (e.NewValue is ObservableCollection<string> newCol)
        {
            newCol.CollectionChanged += control.ExternalChanged;
            control.SyncFromExternal();
        }
    }

    // Sync external → internal
    private void SyncFromExternal()
    {
        _internal.Clear();

        if (Args == null) return;

        foreach (var s in Args)
        {
            var arg = new LaunchArg { Value = s };
            arg.PropertyChanged += InternalArgChanged;
            _internal.Add(arg);
        }
    }

    // Sync internal → external
    private void SyncToExternal()
    {
        if (Args == null) return;

        Args.CollectionChanged -= ExternalChanged;
        Args.Clear();
        foreach (var arg in _internal)
            Args.Add(arg.Value);
        Args.CollectionChanged += ExternalChanged;
    }

    private void InternalArgChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LaunchArg.Value))
            SyncToExternal();
    }

    private void ExternalChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        SyncFromExternal();

    private void btnAdd_Click(object sender, RoutedEventArgs e)
    {
        var newArg = new LaunchArg { Value = string.Empty };
        newArg.PropertyChanged += InternalArgChanged;
        _internal.Add(newArg);
        view.SelectedIndex = _internal.Count - 1;
        SyncToExternal();
    }

    private void btnRemove_Click(object sender, RoutedEventArgs e)
    {
        foreach (var selected in view.SelectedItems.Cast<LaunchArg>().ToList())
        {
            selected.PropertyChanged -= InternalArgChanged;
            _internal.Remove(selected);
        }

        SyncToExternal();
    }

    private void view_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        btnRemove.IsEnabled = view.SelectedItems.Any();
    }
    private void TextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is LaunchArg arg)
            view.SelectedItem = arg;
    }
}
