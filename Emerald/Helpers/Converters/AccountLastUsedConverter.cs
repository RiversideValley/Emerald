using System.Globalization;
using Microsoft.UI.Xaml.Data;

namespace Emerald.Helpers.Converters;

public sealed class AccountLastUsedConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is DateTime dateTime
            ? dateTime.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)
            : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
