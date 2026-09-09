using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Emerald.ViewModels;
namespace Emerald.Controls;

public sealed partial class QuickProfileContent : UserControl
{
    public static readonly DependencyProperty ProfileProperty = DependencyProperty.Register(nameof(Profile),
        typeof(QuickProfileCardViewModel), typeof(QuickProfileContent), new PropertyMetadata(null, Changed));

    public QuickProfileCardViewModel? Profile
    {
        get => (QuickProfileCardViewModel?)GetValue(ProfileProperty);
        set => SetValue(ProfileProperty, value);
    }

    public QuickProfileContent()
    {
        InitializeComponent();
    }

    private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((QuickProfileContent)d).ContentRoot.DataContext = e.NewValue;
}
