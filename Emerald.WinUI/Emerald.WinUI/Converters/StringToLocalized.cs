using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Emerald.WinUI.Converters
{
    public class StringToLocalized: IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return Helpers.Localizer.ToLocalizedString(value.ToString());
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return Helpers.Localizer.ToLocalizedString(value.ToString());
        }
    }
}
