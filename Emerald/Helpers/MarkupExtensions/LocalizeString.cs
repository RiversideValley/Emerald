using Microsoft.UI.Xaml.Markup;
using CommunityToolkit.Mvvm;
using Emerald.CoreX.Helpers;
namespace Emerald.Helpers;

[MarkupExtensionReturnType(ReturnType = typeof(string))]
public sealed class Localize : MarkupExtension
{
    public string KeyName { get; set; }

    protected override object ProvideValue()
        => KeyName.Localize();
}
