using Emerald.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
namespace Emerald.Controls;
public sealed partial class QuickProfileEditor : UserControl
{
 public QuickProfileEditorViewModel ViewModel { get; }
 public QuickProfileEditor(QuickProfileEditorViewModel viewModel)
 {
  ViewModel = viewModel; InitializeComponent(); DataContext = ViewModel;
  Setup.IsExpanded = ViewModel.HasError;
  foreach (var icon in ViewModel.Icons)
  {
   var glyph = icon.Value switch { "Server" => "\uE968", "World" => "\uE909", "Adventure" => "\uE7FC", "Build" => "\uE70F", _ => "\uE768" };
   var button = new ToggleButton { Content = new FontIcon { Glyph = glyph }, IsChecked = ViewModel.Icon == icon, Width = 40, Height = 40 };
   ToolTipService.SetToolTip(button, icon.Label); Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, icon.Label);
   button.Click += (_, _) => { ViewModel.Icon = icon; foreach (ToggleButton b in IconChoices.Children) b.IsChecked = ReferenceEquals(b, button); };
   IconChoices.Children.Add(button);
  }
  foreach (var color in ViewModel.Colors)
  {
   var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
   row.Children.Add(new Border { Width = 18, Height = 18, CornerRadius = new(9), Background = new SolidColorBrush(Color.FromArgb(255, (byte)(color.Value >> 16), (byte)(color.Value >> 8), (byte)color.Value)) });
   row.Children.Add(new TextBlock { Text = color.Label });
   var button = new ToggleButton { Content = row, IsChecked = ViewModel.Accent == color, Margin = new(0, 0, 8, 8) };
   Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, color.Label);
   button.Click += (_, _) => { ViewModel.Accent = color; foreach (ToggleButton b in ColorChoices.Children) b.IsChecked = ReferenceEquals(b, button); };
   Grid.SetColumn(button, ColorChoices.Children.Count % 3);
   Grid.SetRow(button, ColorChoices.Children.Count / 3);
   ColorChoices.Children.Add(button);
  }
 }
}
