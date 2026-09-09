using Microsoft.UI.Xaml.Markup;

namespace Emerald.Helpers;

[MarkupExtensionReturnType(ReturnType = typeof(string))]
public class ConditionString : MarkupExtension
{
    public string TrueString { get; set; }

    public string FalseString { get; set; }

    public bool Condition { get; set; }

    public string Result
        => Condition ? TrueString : FalseString;

    public override string ToString()
    {
        return Result;
    }

    protected override object ProvideValue()
    {
        return Result;
    }
}
