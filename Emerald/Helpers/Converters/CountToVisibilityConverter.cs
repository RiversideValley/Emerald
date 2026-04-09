using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Emerald.Helpers.Converters;

public class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is int count)
        {
            int targetCount = 0;
            if (parameter is string paramStr && int.TryParse(paramStr, out int parsed))
            {
                targetCount = parsed;
            }

            return count == targetCount ? Visibility.Visible : Visibility.Collapsed;
        }

        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
