using System;
using System.Globalization;
using System.Windows.Data;

namespace GeminiGUI.Converters
{
    public class MultiBooleanToBooleanConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length == 0)
                return false;

            // Alle Werte müssen true sein, damit das Ergebnis true ist
            foreach (var value in values)
            {
                if (value is bool boolValue && !boolValue)
                    return false;
            }

            return true;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
