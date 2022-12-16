using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Emerald.WinUI.Helpers
{
    [MarkupExtensionReturnType(ReturnType = typeof(Microsoft.UI.Xaml.Controls.FontIcon))]
    public sealed class FontIcon : MarkupExtension
    {
        public string Glyph { get; set; }
        public int FontSize { get; set; } = 16;

        protected override object ProvideValue() => new Microsoft.UI.Xaml.Controls.FontIcon() { Glyph = Glyph, FontSize = FontSize };
    }
}
