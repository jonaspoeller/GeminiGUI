using System;
using System.Globalization;
using System.Windows.Data;

namespace GeminiGUI.Converters
{
    public class BooleanToVisibilityTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isVisible)
            {
                return isVisible ? "Verbergen" : "Anzeigen";
            }
            return "Anzeigen";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}


