using Microsoft;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace Emerald.Helpers.Converters;

public class StepToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not int currentStep || parameter is not string targetStepString || !int.TryParse(targetStepString, out int targetStep))
        {
            return Visibility.Collapsed;
        }

        return currentStep == targetStep ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
