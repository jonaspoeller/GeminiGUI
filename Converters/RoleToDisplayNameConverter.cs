using System;
using System.Globalization;
using System.Windows.Data;

namespace GeminiGUI.Converters
{
    public class RoleToDisplayNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string role)
            {
                return role.ToLower() switch
                {
                    "user" => "Nutzer",
                    "model" => "Gemini",
                    "assistant" => "Gemini",
                    _ => role
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


