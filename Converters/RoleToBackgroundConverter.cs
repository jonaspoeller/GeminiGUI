using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace GeminiGUI.Converters
{
    public class RoleToBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string role)
            {
                return role switch
                {
                    "user" => new SolidColorBrush(Color.FromRgb(66, 133, 244)), // Google Blue
                    "model" => new SolidColorBrush(Color.FromRgb(45, 45, 48)), // Dark Gray
                    _ => new SolidColorBrush(Color.FromRgb(45, 45, 48))
                };
            }
            return new SolidColorBrush(Color.FromRgb(45, 45, 48));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
