using CommunityToolkit.Mvvm.ComponentModel;
using Emerald.CoreX.Services;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
namespace Emerald.ViewModels;
public partial class QuickProfileCardViewModel(QuickProfile profile, QuickProfileValidation validation, string? instance = null, string? account = null) : ObservableObject
{
    public QuickProfile Profile { get; } = profile;
    public string Name => Profile.Name;
    public string Glyph => Profile.GlyphKey switch { "Server" => "\uE968", "World" => "\uE909", "Adventure" => "\uE7FC", "Build" => "\uE70F", _ => "\uE768" };
    public string Target => Profile.TargetDisplayNameSnapshot ?? DashboardText.Target(Profile.TargetKind);
    public string Context => string.Join(" · ", new[] { instance, account }.Where(x => !string.IsNullOrWhiteSpace(x)));
    public bool NeedsAttention => !validation.IsValid;
    public bool CanLaunch => validation.IsValid;
    public string Status => NeedsAttention ? DashboardText.Get("NeedsAttention") : Target;
    public SolidColorBrush AccentBrush => new(Color.FromArgb(255, (byte)(Profile.AccentArgb >> 16), (byte)(Profile.AccentArgb >> 8), (byte)Profile.AccentArgb));
    public SolidColorBrush TintBrush => new(Color.FromArgb(24, (byte)(Profile.AccentArgb >> 16), (byte)(Profile.AccentArgb >> 8), (byte)Profile.AccentArgb));
    [ObservableProperty] private bool _isSelected;
}
