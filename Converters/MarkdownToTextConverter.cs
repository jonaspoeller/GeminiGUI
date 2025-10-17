using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Data;

namespace GeminiGUI.Converters
{
    public class MarkdownToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string markdownText)
            {
                return ConvertMarkdownToText(markdownText);
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        private string ConvertMarkdownToText(string markdown)
        {
            if (string.IsNullOrEmpty(markdown))
                return string.Empty;

            var result = markdown;

            // Remove code blocks (```code```)
            result = Regex.Replace(result, @"```[\s\S]*?```", "[Code-Block]", RegexOptions.Multiline);

            // Remove inline code (`code`)
            result = Regex.Replace(result, @"`([^`]+)`", "[Code: $1]", RegexOptions.Multiline);

            // Remove bold text (***text*** or **text**)
            result = Regex.Replace(result, @"\*\*\*([^*]+)\*\*\*", "$1", RegexOptions.Multiline);
            result = Regex.Replace(result, @"\*\*([^*]+)\*\*", "$1", RegexOptions.Multiline);

            // Remove italic text (*text*)
            result = Regex.Replace(result, @"\*([^*]+)\*", "$1", RegexOptions.Multiline);

            // Remove headers (# ## ###)
            result = Regex.Replace(result, @"^#{1,6}\s+", "", RegexOptions.Multiline);

            // Remove links [text](url)
            result = Regex.Replace(result, @"\[([^\]]+)\]\([^)]+\)", "$1", RegexOptions.Multiline);

            // Remove images ![alt](url)
            result = Regex.Replace(result, @"!\[([^\]]*)\]\([^)]+\)", "[Bild: $1]", RegexOptions.Multiline);

            return result;
        }
    }
}


