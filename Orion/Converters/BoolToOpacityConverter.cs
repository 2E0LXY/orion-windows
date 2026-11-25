using Microsoft.UI.Xaml.Data;
using System;

namespace Orion
{
    public class BoolToOpacityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language) => value is true ? 1.0 : 0.55;

        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }
}
