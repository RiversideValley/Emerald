using Microsoft.UI.Xaml.Markup;

namespace Emerald.WinUI.Helpers
{
    [MarkupExtensionReturnType(ReturnType = typeof(string))]
    public sealed class Localize : MarkupExtension
    {
        public string Name { get; set; }

        protected override object ProvideValue()
            => Name.Localize();
    }
}
