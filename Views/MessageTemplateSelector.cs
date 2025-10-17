using System.Windows;
using System.Windows.Controls;
using GeminiGUI.Models;

namespace GeminiGUI.Views
{
    public class MessageTemplateSelector : DataTemplateSelector
    {
        public DataTemplate ChatMessageTemplate { get; set; } = null!;
        public DataTemplate DateSeparatorTemplate { get; set; } = null!;

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            return item switch
            {
                ChatMessage => ChatMessageTemplate,
                DateSeparatorMessage => DateSeparatorTemplate,
                _ => base.SelectTemplate(item, container)
            };
        }
    }
}
