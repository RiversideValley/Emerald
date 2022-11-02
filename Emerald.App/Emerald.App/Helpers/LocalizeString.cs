using Microsoft.UI.Xaml.Markup;

namespace Emerald.WinUI.Helpers
{
    [MarkupExtensionReturnType(ReturnType = typeof(string))]
    public sealed class LocalizeString : MarkupExtension
    {
        public enum ResourceFile
        {
            Main,
            Settings
        }
        public Core.Localized Name { get; set; }
        public string CustomName { get; set; }
        public ResourceFile RESW { get; set; } = ResourceFile.Main;
        protected override object ProvideValue() => CustomName == null ? Name.ToLocalizedString() : CustomName.ToLocalizedString(RESW switch { ResourceFile.Settings => "Settings", _ => null });
    }
}
