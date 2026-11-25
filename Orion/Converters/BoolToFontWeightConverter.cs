using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Data;
using System;

namespace Orion
{
    public class BoolToFontWeightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language) => value is true ? FontWeights.SemiBold : FontWeights.Normal;

        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }
}
