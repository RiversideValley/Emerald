using Microsoft.UI.Xaml.Markup;

namespace Emerald.WinUI.Helpers
{
    [MarkupExtensionReturnType(ReturnType = typeof(string))]
    public sealed class LocalizeString : MarkupExtension
    {
        public Core.Localized Name { get; set; }

        protected override object ProvideValue() => Name.ToLocalizedString();
    }
}
