using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using GeminiGUI.Converters;

namespace GeminiGUI.Views
{
    public partial class ChatMessageControl : UserControl
    {
        private readonly RoleToDisplayNameConverter _roleConverter = new RoleToDisplayNameConverter();

        public static readonly DependencyProperty RoleProperty =
            DependencyProperty.Register(nameof(Role), typeof(string), typeof(ChatMessageControl),
                new PropertyMetadata(string.Empty, OnRoleChanged));

        public static readonly DependencyProperty ContentProperty =
            DependencyProperty.Register(nameof(Content), typeof(string), typeof(ChatMessageControl),
                new PropertyMetadata(string.Empty, OnContentChanged));

        public static readonly DependencyProperty TimestampProperty =
            DependencyProperty.Register(nameof(Timestamp), typeof(DateTime), typeof(ChatMessageControl),
                new PropertyMetadata(DateTime.Now, OnTimestampChanged));

        public string Role
        {
            get => (string)GetValue(RoleProperty);
            set => SetValue(RoleProperty, value);
        }

        public string Content
        {
            get => (string)GetValue(ContentProperty);
            set => SetValue(ContentProperty, value);
        }

        public DateTime Timestamp
        {
            get => (DateTime)GetValue(TimestampProperty);
            set => SetValue(TimestampProperty, value);
        }

        public ChatMessageControl()
        {
            InitializeComponent();
        }

        private static void OnRoleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ChatMessageControl control)
            {
                control.UpdateRole();
            }
        }

        private static void OnContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ChatMessageControl control)
            {
                control.UpdateContent();
            }
        }

        private static void OnTimestampChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ChatMessageControl control)
            {
                control.UpdateTimestamp();
            }
        }

        private void UpdateRole()
        {
            var displayName = _roleConverter.Convert(Role, typeof(string), null, System.Globalization.CultureInfo.CurrentCulture);
            RoleTextBlock.Text = displayName?.ToString() ?? Role;
        }

        private void UpdateTimestamp()
        {
            if (Timestamp != default(DateTime))
            {
                TimestampTextBlock.Text = Timestamp.ToString("HH:mm");
            }
            else
            {
                TimestampTextBlock.Text = "";
            }
        }

        private void UpdateContent()
        {
            ContentRichTextBox.Document = new FlowDocument();
            
            if (string.IsNullOrEmpty(Content))
                return;

            // Neue, einfachere Markdown-Verarbeitung
            var paragraph = new Paragraph();
            var inlines = ParseMarkdownSimple(Content);
            
            foreach (var inline in inlines)
            {
                paragraph.Inlines.Add(inline);
            }
            
            ContentRichTextBox.Document.Blocks.Add(paragraph);
        }

        private List<Inline> ParseMarkdownSimple(string markdown)
        {
            var inlines = new List<Inline>();
            var text = markdown;

            // 1. Code-Blöcke zuerst verarbeiten (wichtigste Funktion)
            var codeBlockPattern = @"```(\w+)?\s*([\s\S]*?)```";
            var codeBlockMatches = Regex.Matches(text, codeBlockPattern);

            int lastIndex = 0;
            foreach (Match match in codeBlockMatches)
            {
                // Text vor Code-Block hinzufügen
                if (match.Index > lastIndex)
                {
                    var beforeText = text.Substring(lastIndex, match.Index - lastIndex);
                    ProcessSimpleText(beforeText, inlines);
                }

                // Code-Block hinzufügen
                var language = match.Groups[1].Value.Trim();
                var codeContent = match.Groups[2].Value.Trim();
                
                // Language-Label
                if (!string.IsNullOrEmpty(language))
                {
                    var langRun = new Run($"\n{language.ToUpper()}\n")
                    {
                        FontFamily = new FontFamily("Segoe UI"),
                        FontSize = 10,
                        Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                        FontWeight = FontWeights.Bold
                    };
                    inlines.Add(langRun);
                }

                // Code-Block als TextBox
                var codeBlock = new InlineUIContainer();
                var codeTextBox = new TextBox
                {
                    Text = codeContent,
                    FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                    FontSize = 12,
                    Background = GetCodeBackground(language),
                    Foreground = GetCodeForeground(language),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(8),
                    Margin = new Thickness(0, 4, 0, 4),
                    IsReadOnly = true,
                    TextWrapping = TextWrapping.Wrap,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    MaxHeight = 300
                };
                codeBlock.Child = codeTextBox;
                inlines.Add(codeBlock);

                lastIndex = match.Index + match.Length;
            }

            // 2. Verbleibenden Text verarbeiten
            if (lastIndex < text.Length)
            {
                var remainingText = text.Substring(lastIndex);
                ProcessSimpleText(remainingText, inlines);
            }
            else if (codeBlockMatches.Count == 0)
            {
                // Keine Code-Blöcke gefunden, gesamten Text verarbeiten
                ProcessSimpleText(text, inlines);
            }

            return inlines;
        }

        private void ProcessSimpleText(string text, List<Inline> inlines)
        {
            // Einfache Verarbeitung: nur inline code und basic formatting
            var inlineCodePattern = @"`([^`]+)`";
            var inlineCodeMatches = Regex.Matches(text, inlineCodePattern);

            if (inlineCodeMatches.Count > 0)
            {
                int lastIndex = 0;
                foreach (Match match in inlineCodeMatches)
                {
                    // Text vor inline code
                    if (match.Index > lastIndex)
                    {
                        var beforeText = text.Substring(lastIndex, match.Index - lastIndex);
                        ProcessBasicFormatting(beforeText, inlines);
                    }

                    // Inline code
                    var codeContent = match.Groups[1].Value;
                    var codeRun = new Run(codeContent)
                    {
                        FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                        Background = new SolidColorBrush(Color.FromRgb(240, 240, 240)),
                        Foreground = new SolidColorBrush(Color.FromRgb(220, 38, 38))
                    };
                    inlines.Add(codeRun);

                    lastIndex = match.Index + match.Length;
                }

                // Verbleibender Text
                if (lastIndex < text.Length)
                {
                    var remainingText = text.Substring(lastIndex);
                    ProcessBasicFormatting(remainingText, inlines);
                }
            }
            else
            {
                ProcessBasicFormatting(text, inlines);
            }
        }

        private void ProcessBasicFormatting(string text, List<Inline> inlines)
        {
            // Nur basic formatting: bold und italic
            var boldPattern = @"\*\*\*(.*?)\*\*\*";
            var italicPattern = @"\*(.*?)\*";
            var boldOnlyPattern = @"\*\*(.*?)\*\*";

            // Bold-Italic zuerst
            var boldItalicMatches = Regex.Matches(text, boldPattern);
            if (boldItalicMatches.Count > 0)
            {
                ProcessWithMatches(text, boldItalicMatches, inlines, (content) => new Run(content)
                {
                    FontWeight = FontWeights.Bold,
                    FontStyle = FontStyles.Italic
                });
                return;
            }

            // Dann Bold
            var boldMatches = Regex.Matches(text, boldOnlyPattern);
            if (boldMatches.Count > 0)
            {
                ProcessWithMatches(text, boldMatches, inlines, (content) => new Run(content)
                {
                    FontWeight = FontWeights.Bold
                });
                return;
            }

            // Dann Italic
            var italicMatches = Regex.Matches(text, italicPattern);
            if (italicMatches.Count > 0)
            {
                ProcessWithMatches(text, italicMatches, inlines, (content) => new Run(content)
                {
                    FontStyle = FontStyles.Italic
                });
                return;
            }

            // Kein Formatting, normaler Text
            var normalRun = new Run(text)
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(26, 26, 26))
            };
            inlines.Add(normalRun);
        }

        private void ProcessWithMatches(string text, MatchCollection matches, List<Inline> inlines, Func<string, Run> createFormattedRun)
        {
            int lastIndex = 0;
            foreach (Match match in matches)
            {
                // Text vor dem Match
                if (match.Index > lastIndex)
                {
                    var beforeText = text.Substring(lastIndex, match.Index - lastIndex);
                    var normalRun = new Run(beforeText)
                    {
                        FontFamily = new FontFamily("Segoe UI"),
                        FontSize = 14,
                        Foreground = new SolidColorBrush(Color.FromRgb(26, 26, 26))
                    };
                    inlines.Add(normalRun);
                }

                // Formatierter Text
                var content = match.Groups[1].Value;
                var formattedRun = createFormattedRun(content);
                formattedRun.FontFamily = new FontFamily("Segoe UI");
                formattedRun.FontSize = 14;
                formattedRun.Foreground = new SolidColorBrush(Color.FromRgb(26, 26, 26));
                inlines.Add(formattedRun);

                lastIndex = match.Index + match.Length;
            }

            // Verbleibender Text
            if (lastIndex < text.Length)
            {
                var remainingText = text.Substring(lastIndex);
                var normalRun = new Run(remainingText)
                {
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 14,
                    Foreground = new SolidColorBrush(Color.FromRgb(26, 26, 26))
                };
                inlines.Add(normalRun);
            }
        }

        private List<Inline> ParseMarkdown(string markdown)
        {
            var inlines = new List<Inline>();
            var text = markdown;

            // Split by code blocks first (with optional language)
            var codeBlockPattern = @"```(\w+)?\s*([\s\S]*?)```";
            var codeBlockMatches = Regex.Matches(text, codeBlockPattern);

            int lastIndex = 0;
            foreach (Match match in codeBlockMatches)
            {
                // Add text before code block
                if (match.Index > lastIndex)
                {
                    var beforeText = text.Substring(lastIndex, match.Index - lastIndex);
                    ProcessInlineMarkdown(beforeText, inlines);
                }

                // Add code block
                var language = match.Groups[1].Value.Trim();
                var codeContent = match.Groups[2].Value.Trim();
                
                // Add language label if specified
                if (!string.IsNullOrEmpty(language))
                {
                    var langRun = new Run($"\n{language.ToUpper()}\n")
                    {
                        FontFamily = new FontFamily("Segoe UI"),
                        FontSize = 10,
                        Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                        FontWeight = FontWeights.Bold
                    };
                    inlines.Add(langRun);
                }
                else
                {
                    var codeRun = new Run($"\n")
                    {
                        FontFamily = new FontFamily("Consolas, Courier New, monospace")
                    };
                    inlines.Add(codeRun);
                }
                
                // Add code block as separate element
                var codeBlock = new InlineUIContainer();
                var codeTextBox = new TextBox
                {
                    Text = codeContent,
                    FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                    FontSize = 12,
                    Background = GetCodeBackground(language),
                    Foreground = GetCodeForeground(language),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(8),
                    Margin = new Thickness(0, 4, 0, 4),
                    IsReadOnly = true,
                    TextWrapping = TextWrapping.Wrap,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    MaxHeight = 300
                };
                codeBlock.Child = codeTextBox;
                inlines.Add(codeBlock);
                
                var codeEndRun = new Run($"\n")
                {
                    FontFamily = new FontFamily("Consolas, Courier New, monospace")
                };
                inlines.Add(codeEndRun);

                lastIndex = match.Index + match.Length;
            }

            // Add remaining text
            if (lastIndex < text.Length)
            {
                var remainingText = text.Substring(lastIndex);
                ProcessInlineMarkdown(remainingText, inlines);
            }

            // If no code blocks found, process entire text
            else if (codeBlockMatches.Count == 0)
            {
                ProcessInlineMarkdown(text, inlines);
            }

            return inlines;
        }

        private void ProcessInlineMarkdown(string text, List<Inline> inlines)
        {
            // Process inline code first (backticks)
            var inlineCodePattern = @"`([^`]+)`";
            var inlineCodeMatches = Regex.Matches(text, inlineCodePattern);
            
            if (inlineCodeMatches.Count > 0)
            {
                int lastIndex = 0;
                foreach (Match match in inlineCodeMatches)
                {
                    // Add text before inline code
                    if (match.Index > lastIndex)
                    {
                        var beforeText = text.Substring(lastIndex, match.Index - lastIndex);
                        ProcessBoldItalic(beforeText, inlines);
                    }
                    
                    // Add inline code
                    var codeContent = match.Groups[1].Value;
                    var codeRun = new Run(codeContent)
                    {
                        FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                        Background = new SolidColorBrush(Color.FromRgb(240, 240, 240)),
                        Foreground = new SolidColorBrush(Color.FromRgb(220, 38, 38))
                    };
                    inlines.Add(codeRun);
                    
                    lastIndex = match.Index + match.Length;
                }
                
                // Add remaining text
                if (lastIndex < text.Length)
                {
                    var remainingText = text.Substring(lastIndex);
                    ProcessBoldItalic(remainingText, inlines);
                }
            }
            else
            {
                ProcessBoldItalic(text, inlines);
            }
        }
        
        private void ProcessBoldItalic(string text, List<Inline> inlines)
        {
            // Process bold and italic
            var patterns = new[]
            {
                new { Pattern = @"(\*\*\*|___)(.*?)\1", Style = "BoldItalic" },
                new { Pattern = @"(\*\*|__)(.*?)\1", Style = "Bold" },
                new { Pattern = @"(\*|_)(.*?)\1", Style = "Italic" }
            };

            var processedText = text;
            var replacements = new List<(int start, int end, string content, string style)>();

            foreach (var pattern in patterns)
            {
                var matches = Regex.Matches(processedText, pattern.Pattern);
                foreach (Match match in matches)
                {
                    replacements.Add((match.Index, match.Index + match.Length, match.Groups[2].Value, pattern.Style));
                }
            }

            // Sort by position and process
            replacements.Sort((a, b) => a.start.CompareTo(b.start));

            int currentIndex = 0;
            foreach (var replacement in replacements)
            {
                // Add text before replacement
                if (replacement.start > currentIndex)
                {
                    var beforeText = processedText.Substring(currentIndex, replacement.start - currentIndex);
                    inlines.Add(new Run(beforeText));
                }

                // Add formatted text
                var run = new Run(replacement.content);
                switch (replacement.style)
                {
                    case "Bold":
                        run.FontWeight = FontWeights.Bold;
                        break;
                    case "Italic":
                        run.FontStyle = FontStyles.Italic;
                        break;
                    case "BoldItalic":
                        run.FontWeight = FontWeights.Bold;
                        run.FontStyle = FontStyles.Italic;
                        break;
                }
                inlines.Add(run);

                currentIndex = replacement.end;
            }

            // Add remaining text
            if (currentIndex < processedText.Length)
            {
                var remainingText = processedText.Substring(currentIndex);
                inlines.Add(new Run(remainingText));
            }

            // If no replacements found, add entire text
            if (replacements.Count == 0)
            {
                inlines.Add(new Run(processedText));
            }
        }
        
        private SolidColorBrush GetCodeBackground(string language)
        {
            return language.ToLower() switch
            {
                "python" => new SolidColorBrush(Color.FromRgb(255, 248, 220)), // Python gelb
                "javascript" => new SolidColorBrush(Color.FromRgb(255, 255, 240)), // JS creme
                "typescript" => new SolidColorBrush(Color.FromRgb(240, 248, 255)), // TS blau
                "csharp" => new SolidColorBrush(Color.FromRgb(248, 255, 248)), // C# grün
                "java" => new SolidColorBrush(Color.FromRgb(255, 248, 248)), // Java rot
                "cpp" => new SolidColorBrush(Color.FromRgb(248, 248, 255)), // C++ lila
                "c" => new SolidColorBrush(Color.FromRgb(248, 248, 255)), // C lila
                "html" => new SolidColorBrush(Color.FromRgb(255, 250, 240)), // HTML orange
                "css" => new SolidColorBrush(Color.FromRgb(240, 255, 240)), // CSS grün
                "json" => new SolidColorBrush(Color.FromRgb(255, 255, 240)), // JSON gelb
                "xml" => new SolidColorBrush(Color.FromRgb(255, 250, 240)), // XML orange
                "sql" => new SolidColorBrush(Color.FromRgb(240, 255, 255)), // SQL cyan
                "bash" => new SolidColorBrush(Color.FromRgb(40, 40, 40)), // Bash schwarz
                "powershell" => new SolidColorBrush(Color.FromRgb(1, 36, 86)), // PowerShell blau
                _ => new SolidColorBrush(Color.FromRgb(248, 248, 248)) // Standard grau
            };
        }
        
        private SolidColorBrush GetCodeForeground(string language)
        {
            return language.ToLower() switch
            {
                "bash" => new SolidColorBrush(Color.FromRgb(255, 255, 255)), // Bash weiß
                "powershell" => new SolidColorBrush(Color.FromRgb(255, 255, 255)), // PowerShell weiß
                _ => new SolidColorBrush(Color.FromRgb(33, 37, 41)) // Standard dunkel
            };
        }
    }
}
