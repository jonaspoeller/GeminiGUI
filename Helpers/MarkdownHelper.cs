using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace GeminiGUI.Helpers
{
    public static class MarkdownHelper
    {
        public static List<MarkdownSegment> ParseMarkdown(string markdown)
        {
            var segments = new List<MarkdownSegment>();
            var lines = markdown.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            bool inCodeBlock = false;
            StringBuilder codeBlockContent = new StringBuilder();
            string codeBlockLanguage = string.Empty;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                // Check for code block fences
                if (line.TrimStart().StartsWith("```"))
                {
                    if (!inCodeBlock)
                    {
                        inCodeBlock = true;
                        codeBlockLanguage = line.Trim().Substring(3).Trim();
                    }
                    else
                    {
                        inCodeBlock = false;
                        segments.Add(new MarkdownSegment
                        {
                            Type = SegmentType.CodeBlock,
                            Text = codeBlockContent.ToString(),
                            Language = codeBlockLanguage
                        });
                        codeBlockContent.Clear();
                    }
                    continue;
                }

                if (inCodeBlock)
                {
                    codeBlockContent.AppendLine(line);
                    continue;
                }

                // Check for headings
                if (line.StartsWith("### "))
                {
                    segments.Add(new MarkdownSegment
                    {
                        Type = SegmentType.Heading3,
                        Text = line.Substring(4)
                    });
                }
                else if (line.StartsWith("## "))
                {
                    segments.Add(new MarkdownSegment
                    {
                        Type = SegmentType.Heading2,
                        Text = line.Substring(3)
                    });
                }
                else if (line.StartsWith("# "))
                {
                    segments.Add(new MarkdownSegment
                    {
                        Type = SegmentType.Heading1,
                        Text = line.Substring(2)
                    });
                }
                else
                {
                    // Parse inline markdown
                    segments.AddRange(ParseInlineMarkdown(line));
                }
                
                // Add newline after each line (except last)
                if (i < lines.Length - 1)
                {
                    segments.Add(new MarkdownSegment { Type = SegmentType.Newline });
                }
            }

            return segments;
        }

        private static List<MarkdownSegment> ParseInlineMarkdown(string line)
        {
            var segments = new List<MarkdownSegment>();
            int index = 0;
            
            while (index < line.Length)
            {
                // 1. Check for inline code first (highest priority)
                if (line[index] == '`')
                {
                    int endIndex = line.IndexOf('`', index + 1);
                    if (endIndex != -1)
                    {
                        // Valid inline code found
                        segments.Add(new MarkdownSegment
                        {
                            Type = SegmentType.InlineCode,
                            Text = line.Substring(index + 1, endIndex - index - 1)
                        });
                        index = endIndex + 1;
                        continue;
                    }
                }
                
                // 2. Check for bold **text**
                if (index < line.Length - 1 && line[index] == '*' && line[index + 1] == '*')
                {
                    int searchStart = index + 2;
                    int endIndex = -1;
                    
                    // Find matching closing **
                    while (searchStart < line.Length - 1)
                    {
                        if (line[searchStart] == '*' && line[searchStart + 1] == '*')
                        {
                            // Don't match across backticks
                            string between = line.Substring(index + 2, searchStart - index - 2);
                            if (!between.Contains('`'))
                            {
                                endIndex = searchStart;
                                break;
                            }
                        }
                        searchStart++;
                    }
                    
                    if (endIndex != -1)
                    {
                        // Valid bold found
                        segments.Add(new MarkdownSegment
                        {
                            Type = SegmentType.Bold,
                            Text = line.Substring(index + 2, endIndex - index - 2)
                        });
                        index = endIndex + 2;
                        continue;
                    }
                }
                
                // 3. Check for italic *text* (but not **)
                if (line[index] == '*' && (index == line.Length - 1 || line[index + 1] != '*'))
                {
                    int searchStart = index + 1;
                    int endIndex = -1;
                    
                    // Find matching closing *
                    while (searchStart < line.Length)
                    {
                        if (line[searchStart] == '*' && (searchStart == 0 || line[searchStart - 1] != '*'))
                        {
                            // Don't match across backticks
                            string between = line.Substring(index + 1, searchStart - index - 1);
                            if (!between.Contains('`'))
                            {
                                endIndex = searchStart;
                                break;
                            }
                        }
                        searchStart++;
                    }
                    
                    if (endIndex != -1)
                    {
                        // Valid italic found
                        segments.Add(new MarkdownSegment
                        {
                            Type = SegmentType.Italic,
                            Text = line.Substring(index + 1, endIndex - index - 1)
                        });
                        index = endIndex + 1;
                        continue;
                    }
                }
                
                // 4. Normal character - find next special char
                int nextSpecial = line.Length;
                for (int i = index + 1; i < line.Length; i++)
                {
                    if (line[i] == '`' || line[i] == '*')
                    {
                        nextSpecial = i;
                        break;
                    }
                }
                
                // Add normal text segment
                segments.Add(new MarkdownSegment
                {
                    Type = SegmentType.Normal,
                    Text = line.Substring(index, nextSpecial - index)
                });
                index = nextSpecial;
            }
            
            return segments;
        }

        // Convert parsed segments to Inlines on UI thread - FAST!
        public static void ApplyMarkdownToTextBlock(System.Windows.Controls.TextBlock textBlock, List<MarkdownSegment> segments)
        {
            textBlock.Inlines.Clear();

            foreach (var segment in segments)
            {
                switch (segment.Type)
                {
                    case SegmentType.Normal:
                        textBlock.Inlines.Add(new Run(segment.Text));
                        break;

                    case SegmentType.Bold:
                        textBlock.Inlines.Add(new Bold(new Run(segment.Text)));
                        break;

                    case SegmentType.Italic:
                        textBlock.Inlines.Add(new Italic(new Run(segment.Text)));
                        break;

                    case SegmentType.InlineCode:
                        var inlineCodeRun = new Run(segment.Text)
                        {
                            Background = new SolidColorBrush(Color.FromRgb(240, 240, 240)),
                            Foreground = new SolidColorBrush(Color.FromRgb(214, 51, 132)),
                            FontFamily = new FontFamily("Consolas, Courier New"),
                            FontSize = 13
                        };
                        textBlock.Inlines.Add(inlineCodeRun);
                        break;

                    case SegmentType.CodeBlock:
                        // Add spacing before code block
                        textBlock.Inlines.Add(new LineBreak());
                        textBlock.Inlines.Add(new LineBreak());
                        
                        var codeBlockRun = new Run(segment.Text.TrimEnd('\r', '\n'))
                        {
                            Background = new SolidColorBrush(Color.FromRgb(40, 44, 52)),
                            Foreground = new SolidColorBrush(Color.FromRgb(171, 178, 191)),
                            FontFamily = new FontFamily("Consolas, Courier New"),
                            FontSize = 13
                        };
                        
                        // Add padding around code block using Span
                        var codeSpan = new Span(codeBlockRun)
                        {
                            Background = new SolidColorBrush(Color.FromRgb(40, 44, 52))
                        };
                        
                        textBlock.Inlines.Add(codeSpan);
                        
                        // Add spacing after code block
                        textBlock.Inlines.Add(new LineBreak());
                        textBlock.Inlines.Add(new LineBreak());
                        break;

                    case SegmentType.Newline:
                        textBlock.Inlines.Add(new LineBreak());
                        break;

                    case SegmentType.Heading1:
                        textBlock.Inlines.Add(new LineBreak());
                        var h1Run = new Run(segment.Text)
                        {
                            FontSize = 24,
                            FontWeight = FontWeights.Bold,
                            Foreground = new SolidColorBrush(Color.FromRgb(26, 26, 26))
                        };
                        textBlock.Inlines.Add(h1Run);
                        textBlock.Inlines.Add(new LineBreak());
                        break;

                    case SegmentType.Heading2:
                        textBlock.Inlines.Add(new LineBreak());
                        var h2Run = new Run(segment.Text)
                        {
                            FontSize = 20,
                            FontWeight = FontWeights.Bold,
                            Foreground = new SolidColorBrush(Color.FromRgb(26, 26, 26))
                        };
                        textBlock.Inlines.Add(h2Run);
                        textBlock.Inlines.Add(new LineBreak());
                        break;

                    case SegmentType.Heading3:
                        textBlock.Inlines.Add(new LineBreak());
                        var h3Run = new Run(segment.Text)
                        {
                            FontSize = 16,
                            FontWeight = FontWeights.Bold,
                            Foreground = new SolidColorBrush(Color.FromRgb(26, 26, 26))
                        };
                        textBlock.Inlines.Add(h3Run);
                        textBlock.Inlines.Add(new LineBreak());
                        break;
                }
            }
        }

        // Apply markdown to StackPanel - supports proper code blocks!
        public static void ApplyMarkdownToStackPanel(System.Windows.Controls.StackPanel stackPanel, List<MarkdownSegment> segments)
        {
            stackPanel.Children.Clear();

            System.Windows.Controls.TextBlock currentTextBlock = null;
            
            foreach (var segment in segments)
            {
                if (segment.Type == SegmentType.CodeBlock)
                {
                    // Create a proper code block with Border
                    var codeTextBlock = new System.Windows.Controls.TextBlock
                    {
                        Text = segment.Text.TrimEnd('\r', '\n'),
                        FontFamily = new FontFamily("Consolas, Courier New"),
                        FontSize = 13,
                        Foreground = new SolidColorBrush(Color.FromRgb(171, 178, 191)),
                        TextWrapping = TextWrapping.NoWrap
                    };

                    var scrollViewer = new System.Windows.Controls.ScrollViewer
                    {
                        HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                        VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                        Content = codeTextBlock,
                        MaxHeight = 400
                    };

                    var border = new System.Windows.Controls.Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(40, 44, 52)),
                        CornerRadius = new CornerRadius(8),
                        Padding = new Thickness(16, 12, 16, 12),
                        Margin = new Thickness(0, 8, 0, 8),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(60, 64, 72)),
                        BorderThickness = new Thickness(1),
                        Child = scrollViewer
                    };

                    stackPanel.Children.Add(border);
                    currentTextBlock = null; // Reset current text block
                }
                else
                {
                    // For all other segments, use inline text
                    if (currentTextBlock == null)
                    {
                        currentTextBlock = new System.Windows.Controls.TextBlock
                        {
                            TextWrapping = TextWrapping.Wrap,
                            Foreground = new SolidColorBrush(Color.FromRgb(26, 26, 26))
                        };
                        stackPanel.Children.Add(currentTextBlock);
                    }

                    // Add segment to current text block
                    switch (segment.Type)
                    {
                        case SegmentType.Normal:
                            currentTextBlock.Inlines.Add(new Run(segment.Text));
                            break;

                        case SegmentType.Bold:
                            currentTextBlock.Inlines.Add(new Bold(new Run(segment.Text)));
                            break;

                        case SegmentType.Italic:
                            currentTextBlock.Inlines.Add(new Italic(new Run(segment.Text)));
                            break;

                        case SegmentType.InlineCode:
                            var inlineCodeRun = new Run(segment.Text)
                            {
                                Background = new SolidColorBrush(Color.FromRgb(240, 240, 240)),
                                Foreground = new SolidColorBrush(Color.FromRgb(214, 51, 132)),
                                FontFamily = new FontFamily("Consolas, Courier New"),
                                FontSize = 13
                            };
                            currentTextBlock.Inlines.Add(inlineCodeRun);
                            break;

                        case SegmentType.Newline:
                            currentTextBlock.Inlines.Add(new LineBreak());
                            break;

                        case SegmentType.Heading1:
                            currentTextBlock.Inlines.Add(new LineBreak());
                            var h1Run = new Run(segment.Text)
                            {
                                FontSize = 24,
                                FontWeight = FontWeights.Bold
                            };
                            currentTextBlock.Inlines.Add(h1Run);
                            currentTextBlock.Inlines.Add(new LineBreak());
                            break;

                        case SegmentType.Heading2:
                            currentTextBlock.Inlines.Add(new LineBreak());
                            var h2Run = new Run(segment.Text)
                            {
                                FontSize = 20,
                                FontWeight = FontWeights.Bold
                            };
                            currentTextBlock.Inlines.Add(h2Run);
                            currentTextBlock.Inlines.Add(new LineBreak());
                            break;

                        case SegmentType.Heading3:
                            currentTextBlock.Inlines.Add(new LineBreak());
                            var h3Run = new Run(segment.Text)
                            {
                                FontSize = 16,
                                FontWeight = FontWeights.Bold
                            };
                            currentTextBlock.Inlines.Add(h3Run);
                            currentTextBlock.Inlines.Add(new LineBreak());
                            break;
                    }
                }
            }
        }

        // Apply markdown to RichTextBox - supports formatting AND text selection!
        public static void ApplyMarkdownToRichTextBox(System.Windows.Controls.RichTextBox richTextBox, List<MarkdownSegment> segments)
        {
            richTextBox.Document.Blocks.Clear();
            
            var paragraph = new System.Windows.Documents.Paragraph();
            paragraph.Margin = new Thickness(0);
            
            foreach (var segment in segments)
            {
                switch (segment.Type)
                {
                    case SegmentType.Normal:
                        paragraph.Inlines.Add(new System.Windows.Documents.Run(segment.Text));
                        break;

                    case SegmentType.Bold:
                        paragraph.Inlines.Add(new System.Windows.Documents.Bold(
                            new System.Windows.Documents.Run(segment.Text)));
                        break;

                    case SegmentType.Italic:
                        paragraph.Inlines.Add(new System.Windows.Documents.Italic(
                            new System.Windows.Documents.Run(segment.Text)));
                        break;

                    case SegmentType.InlineCode:
                        var inlineCodeRun = new System.Windows.Documents.Run(segment.Text)
                        {
                            Background = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                            Foreground = new SolidColorBrush(Color.FromRgb(214, 51, 132)),
                            FontFamily = new FontFamily("Consolas, Courier New")
                        };
                        paragraph.Inlines.Add(inlineCodeRun);
                        break;

                    case SegmentType.CodeBlock:
                        // Finish current paragraph
                        if (paragraph.Inlines.Count > 0)
                        {
                            richTextBox.Document.Blocks.Add(paragraph);
                            paragraph = new System.Windows.Documents.Paragraph();
                            paragraph.Margin = new Thickness(0);
                        }

                        // Add code block as separate paragraph
                        var codeBlockParagraph = new System.Windows.Documents.Paragraph(
                            new System.Windows.Documents.Run(segment.Text.TrimEnd('\r', '\n')))
                        {
                            Background = new SolidColorBrush(Color.FromRgb(40, 44, 52)),
                            Foreground = new SolidColorBrush(Color.FromRgb(171, 178, 191)),
                            FontFamily = new FontFamily("Consolas, Courier New"),
                            FontSize = 13,
                            Padding = new Thickness(12, 8, 12, 8),
                            Margin = new Thickness(0, 8, 0, 8)
                        };
                        richTextBox.Document.Blocks.Add(codeBlockParagraph);
                        break;

                    case SegmentType.Newline:
                        paragraph.Inlines.Add(new System.Windows.Documents.LineBreak());
                        break;

                    case SegmentType.Heading1:
                        paragraph.Inlines.Add(new System.Windows.Documents.LineBreak());
                        var h1Run = new System.Windows.Documents.Run(segment.Text)
                        {
                            FontSize = 24,
                            FontWeight = FontWeights.Bold
                        };
                        paragraph.Inlines.Add(h1Run);
                        paragraph.Inlines.Add(new System.Windows.Documents.LineBreak());
                        break;

                    case SegmentType.Heading2:
                        paragraph.Inlines.Add(new System.Windows.Documents.LineBreak());
                        var h2Run = new System.Windows.Documents.Run(segment.Text)
                        {
                            FontSize = 20,
                            FontWeight = FontWeights.Bold
                        };
                        paragraph.Inlines.Add(h2Run);
                        paragraph.Inlines.Add(new System.Windows.Documents.LineBreak());
                        break;

                    case SegmentType.Heading3:
                        paragraph.Inlines.Add(new System.Windows.Documents.LineBreak());
                        var h3Run = new System.Windows.Documents.Run(segment.Text)
                        {
                            FontSize = 16,
                            FontWeight = FontWeights.Bold
                        };
                        paragraph.Inlines.Add(h3Run);
                        paragraph.Inlines.Add(new System.Windows.Documents.LineBreak());
                        break;
                }
            }
            
            // Add final paragraph if it has content
            if (paragraph.Inlines.Count > 0)
            {
                richTextBox.Document.Blocks.Add(paragraph);
            }
        }
    }

    public class MarkdownSegment
    {
        public SegmentType Type { get; set; }
        public string Text { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
    }

    public enum SegmentType
    {
        Normal,
        Bold,
        Italic,
        InlineCode,
        CodeBlock,
        Newline,
        Heading1,
        Heading2,
        Heading3
    }
}
