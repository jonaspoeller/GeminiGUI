using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using GeminiGUI.Helpers;

namespace GeminiGUI.Behaviors
{
    public static class MarkdownBehavior
    {
        public static readonly DependencyProperty MarkdownTextProperty =
            DependencyProperty.RegisterAttached(
                "MarkdownText",
                typeof(string),
                typeof(MarkdownBehavior),
                new PropertyMetadata(string.Empty, OnMarkdownTextChanged));

        public static string GetMarkdownText(DependencyObject obj)
        {
            return (string)obj.GetValue(MarkdownTextProperty);
        }

        public static void SetMarkdownText(DependencyObject obj, string value)
        {
            obj.SetValue(MarkdownTextProperty, value);
        }

        private static async void OnMarkdownTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var markdown = e.NewValue as string;
            
            // Support TextBlock, StackPanel, and RichTextBox
            if (d is TextBlock textBlock)
            {
                if (string.IsNullOrEmpty(markdown))
                {
                    textBlock.Inlines.Clear();
                    textBlock.Text = string.Empty;
                    return;
                }

                // Parse markdown in background thread
                var segments = await Task.Run(() => MarkdownHelper.ParseMarkdown(markdown)).ConfigureAwait(true);

                // Apply to UI on UI thread (fast!)
                MarkdownHelper.ApplyMarkdownToTextBlock(textBlock, segments);
            }
            else if (d is StackPanel stackPanel)
            {
                if (string.IsNullOrEmpty(markdown))
                {
                    stackPanel.Children.Clear();
                    return;
                }

                // Parse markdown in background thread
                var segments = await Task.Run(() => MarkdownHelper.ParseMarkdown(markdown)).ConfigureAwait(true);

                // Apply to UI on UI thread with proper blocks
                MarkdownHelper.ApplyMarkdownToStackPanel(stackPanel, segments);
            }
            else if (d is RichTextBox richTextBox)
            {
                if (string.IsNullOrEmpty(markdown))
                {
                    richTextBox.Document.Blocks.Clear();
                    return;
                }

                // Parse markdown in background thread
                var segments = await Task.Run(() => MarkdownHelper.ParseMarkdown(markdown)).ConfigureAwait(true);

                // Apply to UI on UI thread
                MarkdownHelper.ApplyMarkdownToRichTextBox(richTextBox, segments);
            }
        }
    }
}

