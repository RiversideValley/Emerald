
using Microsoft.UI.Xaml.Data;

namespace Emerald.Helpers.Converters;
public class BoolToVisibility : IValueConverter
    {
        public bool Reversed { get; set; }

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return (value is bool b) ? (((Reversed || parameter == "reversed") && parameter != "normal") ? (!b ? Visibility.Visible : Visibility.Collapsed) : (b ? Visibility.Visible : Visibility.Collapsed)) : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new InvalidOperationException();
        }
    }