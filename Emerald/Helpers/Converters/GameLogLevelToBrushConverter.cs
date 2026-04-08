using Emerald.CoreX.Runtime;
using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace Emerald.Helpers.Converters;

public class GameLogLevelToBrushConverter : IValueConverter
{
    public SolidColorBrush FatalBrush { get; set; } = new(Colors.IndianRed);
    public SolidColorBrush ErrorBrush { get; set; } = new(Colors.LightCoral);
    public SolidColorBrush WarningBrush { get; set; } = new(Colors.Goldenrod);
    public SolidColorBrush InfoBrush { get; set; } = new(Colors.LightGray);
    public SolidColorBrush DebugBrush { get; set; } = new(Colors.LightSkyBlue);
    public SolidColorBrush TraceBrush { get; set; } = new(Colors.DarkGray);
    public SolidColorBrush UnknownBrush { get; set; } = new(Colors.Gray);

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not GameLogLevel level)
        {
            return UnknownBrush;
        }

        return level switch
        {
            GameLogLevel.Fatal => FatalBrush,
            GameLogLevel.Error => ErrorBrush,
            GameLogLevel.Warn => WarningBrush,
            GameLogLevel.Info => InfoBrush,
            GameLogLevel.Debug => DebugBrush,
            GameLogLevel.Trace => TraceBrush,
            _ => UnknownBrush
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}
