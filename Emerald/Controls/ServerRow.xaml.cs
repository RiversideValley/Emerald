using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Emerald.ViewModels;
namespace Emerald.Controls;
public sealed partial class ServerRow : UserControl
{
 public static readonly DependencyProperty ModelProperty = DependencyProperty.Register(nameof(Model), typeof(ServerRowViewModel), typeof(ServerRow), new PropertyMetadata(null, Changed));
 public ServerRowViewModel? Model { get => (ServerRowViewModel?)GetValue(ModelProperty); set => SetValue(ModelProperty, value); }
 public event EventHandler? Selected;
 public event EventHandler? Play;
 public event EventHandler? Favorite;
 public event EventHandler? Options;
 public ServerRow() => InitializeComponent();
 public void ShowOptions(MenuFlyout flyout) => flyout.ShowAt(OptionsButton);
 private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((ServerRow)d).Root.DataContext = e.NewValue;
 private void Select_Click(object sender, RoutedEventArgs e) => Selected?.Invoke(this, EventArgs.Empty);
 private void Play_Click(object sender, RoutedEventArgs e) => Play?.Invoke(this, EventArgs.Empty);
 private void Favorite_Click(object sender, RoutedEventArgs e) => Favorite?.Invoke(this, EventArgs.Empty);
 private void Menu_Click(object sender, RoutedEventArgs e) => Options?.Invoke(this, EventArgs.Empty);
}
