using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Emerald.Helpers.Converters;

public sealed class StringToVisibilityConverter : IValueConverter
{
    public bool CollapsedWhenEmpty { get; set; } = true;

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var hasText = !string.IsNullOrWhiteSpace(value as string);
        return hasText == CollapsedWhenEmpty ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
