using Microsoft.UI.Xaml.Markup;

namespace Emerald.WinUI.Helpers
{
    [MarkupExtensionReturnType(ReturnType = typeof(string))]
    public sealed class Localize : MarkupExtension
    {
        public enum ResourceFile
        {
            Main,
            Settings
        }

        public Core.Localized Name { get; set; }

        public string CustomName { get; set; }

        public ResourceFile RESW { get; set; } = ResourceFile.Main;

        protected override object ProvideValue()
            => CustomName == null ? Name.Localize() : CustomName.Localize(RESW switch { ResourceFile.Settings => "Settings", _ => null });
    }
}
