using Microsoft.UI.Xaml.Markup;

namespace Emerald.WinUI.Helpers
{
    [MarkupExtensionReturnType(ReturnType = typeof(string))]
    public sealed class Localize : MarkupExtension
    {
        public Core.Localized Name { get; set; }

        public string CustomName { get; set; }

        protected override object ProvideValue()
            => CustomName == null ? Name.Localize() : CustomName.Localize();
    }
}
