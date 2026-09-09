using CommunityToolkit.Mvvm.ComponentModel;
using Emerald.CoreX.Services;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
namespace Emerald.ViewModels;

public partial class QuickProfileCardViewModel(QuickProfile profile, QuickProfileValidation validation, string? instance = null, string? account = null) : ObservableObject
{
    public QuickProfile Profile { get; } = profile;
    public string Name => Profile.Name;
    public string Glyph => QuickProfileGlyphs.Resolve(Profile.GlyphKey);
    public bool UsesBlockIcon => Profile.IconKind == QuickProfileIconKind.Block && !string.IsNullOrWhiteSpace(Profile.BlockIconFileName);
    public bool UsesGlyphIcon => !UsesBlockIcon;
    public string? BlockIconSource => UsesBlockIcon ? $"ms-appx:///Assets/blocks/{Uri.EscapeDataString(Path.GetFileName(Profile.BlockIconFileName!))}" : null;
    public string Target => Profile.TargetDisplayNameSnapshot ?? DashboardText.Target(Profile.TargetKind);
    public string Context => string.Join(" · ", new[] { instance, account }.Where(x => !string.IsNullOrWhiteSpace(x)));
    public bool NeedsAttention => !validation.IsValid;
    public bool CanLaunch => validation.IsValid;
    public string Status => NeedsAttention ? DashboardText.Get("NeedsAttention") : Target;
    public SolidColorBrush AccentBrush => new(Color.FromArgb(255, (byte)(Profile.AccentArgb >> 16), (byte)(Profile.AccentArgb >> 8), (byte)Profile.AccentArgb));
    public SolidColorBrush TintBrush => new(Color.FromArgb(24, (byte)(Profile.AccentArgb >> 16), (byte)(Profile.AccentArgb >> 8), (byte)Profile.AccentArgb));
    [ObservableProperty] private bool _isSelected;
}

public sealed partial record QuickProfileGlyphOption(string Key, string Label, string Glyph);

public static class QuickProfileGlyphs
{
    public static IReadOnlyList<QuickProfileGlyphOption> All { get; } =
    [
        new("Play", DashboardText.Get("Play"), "\uE768"),
        new("Server", DashboardText.Get("Server"), "\uE968"),
        new("World", DashboardText.Get("World"), "\uE909"),
        new("Adventure", DashboardText.Get("Adventure"), "\uE7FC"),
        new("Build", DashboardText.Get("Build"), "\uE70F"),
        new("Multiplayer", DashboardText.Get("Multiplayer"), "\uE716"),
        new("Favorite", DashboardText.Get("Favorite"), "\uE734"),
        new("Home", DashboardText.Get("Home"), "\uE80F"),
        new("Search", DashboardText.Get("Search"), "\uE721"),
        new("Download", DashboardText.Get("Download"), "\uE896"),
        new("Library", DashboardText.Get("Library"), "\uE8F1"),
        new("Store", DashboardText.Get("Store"), "\uE719")
    ];

    public static string Resolve(string? key) => All.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase))?.Glyph ?? "\uE768";
}
