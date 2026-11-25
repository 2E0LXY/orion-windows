using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;

namespace Orion
{
    /// <summary>
    /// Converts a boolean (IsActive) to a background brush for tab styling.
    /// </summary>
    public class BoolToBackgroundConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool isActive && isActive)
                return Application.Current.Resources["CardBackgroundFillColorDefaultBrush"] as SolidColorBrush;

            return new SolidColorBrush(Colors.Transparent); // Transparent but hit-testable
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }
}
