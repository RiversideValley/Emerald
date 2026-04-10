using System;
using Microsoft.UI.Xaml.Data;

namespace Emerald.Helpers.Converters;

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is bool flag ? !flag : false;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => value is bool flag ? !flag : throw new InvalidOperationException();
}
