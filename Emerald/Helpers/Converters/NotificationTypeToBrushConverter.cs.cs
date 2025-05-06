using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI;
using Emerald.CoreX.Notifications;

namespace Emerald.Helpers.Converters;

public class NotificationTypeToBrushConverter : IValueConverter
{
    public SolidColorBrush InfoBrush { get; set; } = new SolidColorBrush(Colors.LightBlue);
    public SolidColorBrush WarningBrush { get; set; } = new SolidColorBrush(Colors.LightGoldenrodYellow);
    public SolidColorBrush ErrorBrush { get; set; } = new SolidColorBrush(Colors.LightCoral);
    public SolidColorBrush SuccessBrush { get; set; } = new SolidColorBrush(Colors.LightGreen);
    public SolidColorBrush ProgressBrush { get; set; } = new SolidColorBrush(Colors.LightGray);

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is NotificationType type)
        {
            return type switch
            {
                NotificationType.Info => InfoBrush,
                NotificationType.Warning => WarningBrush,
                NotificationType.Error => ErrorBrush,
                NotificationType.Success => SuccessBrush,
                NotificationType.Progress => ProgressBrush,
                _ => new SolidColorBrush(Colors.Transparent),
            };
        }
        return new SolidColorBrush(Colors.Transparent);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}
