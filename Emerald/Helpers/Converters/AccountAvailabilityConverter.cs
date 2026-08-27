using Emerald.CoreX.Helpers;
using Emerald.CoreX.Services.Auth;
using Emerald.Helpers;
using Microsoft.UI.Xaml.Data;

namespace Emerald.Helpers.Converters;

public sealed class AccountAvailabilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is AccountAvailability availability
            ? $"AccountStatus{availability}".Localize()
            : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
