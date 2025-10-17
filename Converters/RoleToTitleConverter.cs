using System;
using System.Globalization;
using System.Windows.Data;

namespace GeminiGUI.Converters
{
    public class RoleToTitleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string role)
            {
                return role switch
                {
                    "user" => "Sie",
                    "model" => "Gemini",
                    _ => "Unbekannt"
                };
            }
            return "Unbekannt";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}