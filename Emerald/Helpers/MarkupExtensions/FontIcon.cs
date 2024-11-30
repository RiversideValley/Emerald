using Microsoft.UI.Xaml.Markup;

namespace Emerald.Helpers;

[MarkupExtensionReturnType(ReturnType = typeof(Microsoft.UI.Xaml.Controls.FontIcon))]
public sealed class FontIcon : MarkupExtension
{
    public string Glyph { get; set; } = "\xe8a5";

    public int FontSize { get; set; } = 16;

    protected override object ProvideValue()
        => new Microsoft.UI.Xaml.Controls.FontIcon() { Glyph = Glyph, FontSize = FontSize };
}
