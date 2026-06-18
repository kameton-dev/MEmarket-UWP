using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;

namespace MEmarket_UWP.Services
{
    public static class MarkdownRenderer
    {
        private enum SpanType { Plain, Bold, Italic, Strikethrough, Code, Link }
        
        public static void Render(RichTextBlock target, string markdown)
        {
            target.Blocks.Clear();
            if (string.IsNullOrEmpty(markdown))
                return;

            var lines = markdown.Replace("\r\n", "\n").Split('\n');

            bool inCodeBlock = false;
            var codeLines = new List<string>();
            var paragraphLines = new List<string>();

            Action commitParagraph = () =>
            {
                if (paragraphLines.Count > 0)
                {
                    var fullText = string.Join(" ", paragraphLines);
                    var p = new Paragraph { Margin = new Thickness(0, 0, 0, 8) };
                    ParseInlineRecursive(fullText, p.Inlines);
                    target.Blocks.Add(p);
                    paragraphLines.Clear();
                }
            };

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var trimmed = line.Trim();
                
                if (trimmed.StartsWith("```"))
                {
                    if (inCodeBlock)
                    {
                        var codeText = string.Join("\n", codeLines);
                        target.Blocks.Add(CreateCodeBlock(codeText));
                        codeLines.Clear();
                        inCodeBlock = false;
                    }
                    else
                    {
                        commitParagraph();
                        inCodeBlock = true;
                    }
                    continue;
                }

                if (inCodeBlock)
                {
                    codeLines.Add(line);
                    continue;
                }

                // TODO: фикс сепаратор
                if (trimmed == "---" || trimmed == "***" || trimmed == "___")
                {
                    commitParagraph();
                    target.Blocks.Add(CreateHorizontalRule());
                    continue;
                }

                if (trimmed.StartsWith("#"))
                {
                    int level = 0;
                    while (level < trimmed.Length && trimmed[level] == '#')
                    {
                        level++;
                    }

                    if (level > 0 && level <= 6 && level < trimmed.Length && trimmed[level] == ' ')
                    {
                        commitParagraph();
                        var headerText = trimmed.Substring(level + 1).Trim();
                        target.Blocks.Add(CreateHeader(headerText, level));
                        continue;
                    }
                }
                
                if (trimmed.StartsWith(">"))
                {
                    commitParagraph();
                    var quoteText = trimmed.Substring(1).Trim();
                    target.Blocks.Add(CreateBlockquote(quoteText));
                    continue;
                }
                
                if (trimmed.StartsWith("- ") || trimmed.StartsWith("* ") || trimmed.StartsWith("+ "))
                {
                    commitParagraph();
                    var listText = trimmed.Substring(2).Trim();
                    target.Blocks.Add(CreateListItem("• ", listText));
                    continue;
                }
                
                var numMatch = Regex.Match(trimmed, @"^(\d+)\.\s+(.*)$");
                if (numMatch.Success)
                {
                    commitParagraph();
                    var prefix = numMatch.Groups[1].Value + ". ";
                    var listText = numMatch.Groups[2].Value.Trim();
                    target.Blocks.Add(CreateListItem(prefix, listText));
                    continue;
                }
                
                if (string.IsNullOrWhiteSpace(line))
                {
                    commitParagraph();
                    continue;
                }
                
                paragraphLines.Add(trimmed);
            }
            
            commitParagraph();
        }

        #region Block Builders

        private static Paragraph CreateHeader(string text, int level)
        {
            var p = new Paragraph();
            double fontSize;
            var fontWeight = FontWeights.Bold;
            var margin = new Thickness(0, 10, 0, 4);

            switch (level)
            {
                case 1: fontSize = 22; margin = new Thickness(0, 14, 0, 6); break;
                case 2: fontSize = 19; margin = new Thickness(0, 12, 0, 5); break;
                case 3: fontSize = 17; margin = new Thickness(0, 10, 0, 4); break;
                case 4: fontSize = 15; margin = new Thickness(0, 8, 0, 4); break;
                default: fontSize = 14; margin = new Thickness(0, 6, 0, 4); break;
            }

            p.Margin = margin;

            var headerSpan = new Span { FontSize = fontSize, FontWeight = fontWeight };
            ParseInlineRecursive(text, headerSpan.Inlines);
            p.Inlines.Add(headerSpan);
            return p;
        }

        private static Paragraph CreateCodeBlock(string codeText)
        {
            var p = new Paragraph { Margin = new Thickness(0, 6, 0, 6) };

            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(20, 128, 128, 128)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(40, 128, 128, 128)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8),
                CornerRadius = new CornerRadius(4),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var tb = new TextBlock
            {
                Text = codeText,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Colors.LightGray)
            };

            border.Child = tb;

            var container = new InlineUIContainer { Child = border };
            p.Inlines.Add(container);
            return p;
        }

        private static Paragraph CreateHorizontalRule()
        {
            var p = new Paragraph { Margin = new Thickness(0, 10, 0, 10) };

            var border = new Border
            {
                Height = 1,
                Background = new SolidColorBrush(Color.FromArgb(50, 128, 128, 128)),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var container = new InlineUIContainer { Child = border };
            p.Inlines.Add(container);
            return p;
        }

        private static Paragraph CreateBlockquote(string text)
        {
            var p = new Paragraph { Margin = new Thickness(10, 4, 0, 6) };

            var accentRun = new Run
            {
                Text = "┃ ",
                Foreground = new SolidColorBrush(Color.FromArgb(100, 128, 128, 128)),
                FontWeight = FontWeights.Bold
            };
            p.Inlines.Add(accentRun);

            var quoteSpan = new Span
            {
                FontStyle = FontStyle.Italic,
                Foreground = new SolidColorBrush(Color.FromArgb(180, 128, 128, 128))
            };
            ParseInlineRecursive(text, quoteSpan.Inlines);
            p.Inlines.Add(quoteSpan);

            return p;
        }

        private static Paragraph CreateListItem(string prefix, string text)
        {
            var p = new Paragraph { Margin = new Thickness(16, 0, 0, 4) };

            var prefixRun = new Run
            {
                Text = prefix,
                FontWeight = FontWeights.Bold
            };
            p.Inlines.Add(prefixRun);

            var contentSpan = new Span();
            ParseInlineRecursive(text, contentSpan.Inlines);
            p.Inlines.Add(contentSpan);

            return p;
        }

        #endregion

        #region Inline Parsing

        private static void ParseInlineRecursive(string text, IList<Inline> target)
        {
            if (string.IsNullOrEmpty(text)) return;

            int firstIdx = -1;
            int patternLength = 0;
            SpanType detectedType = SpanType.Plain;
            string content = "";
            string extra = "";
            
            var linkRegex = new Regex(@"^\[(?<text>[^\]]+)\]\((?<url>[^)]+)\)");
            var boldRegex1 = new Regex(@"^\*\*(?<body>[^*]+)\*\*");
            var boldRegex2 = new Regex(@"^__(?<body>[^_]+)__");
            var italicRegex1 = new Regex(@"^\*(?<body>[^*]+)\*");
            var italicRegex2 = new Regex(@"^_(?<body>[^_]+)_");
            var strikeRegex = new Regex(@"^~~(?<body>[^~]+)~~");
            var codeRegex = new Regex(@"^`(?<body>[^`]+)`");
            
            for (int i = 0; i < text.Length; i++)
            {
                var remaining = text.Substring(i);
                
                var match = linkRegex.Match(remaining);
                if (match.Success)
                {
                    firstIdx = i;
                    patternLength = match.Length;
                    detectedType = SpanType.Link;
                    content = match.Groups["text"].Value;
                    extra = match.Groups["url"].Value;
                    break;
                }
                match = boldRegex1.Match(remaining);
                if (match.Success)
                {
                    firstIdx = i;
                    patternLength = match.Length;
                    detectedType = SpanType.Bold;
                    content = match.Groups["body"].Value;
                    break;
                }
                match = boldRegex2.Match(remaining);
                if (match.Success)
                {
                    firstIdx = i;
                    patternLength = match.Length;
                    detectedType = SpanType.Bold;
                    content = match.Groups["body"].Value;
                    break;
                }
                match = italicRegex1.Match(remaining);
                if (match.Success)
                {
                    firstIdx = i;
                    patternLength = match.Length;
                    detectedType = SpanType.Italic;
                    content = match.Groups["body"].Value;
                    break;
                }
                match = italicRegex2.Match(remaining);
                if (match.Success)
                {
                    firstIdx = i;
                    patternLength = match.Length;
                    detectedType = SpanType.Italic;
                    content = match.Groups["body"].Value;
                    break;
                }
                match = strikeRegex.Match(remaining);
                if (match.Success)
                {
                    firstIdx = i;
                    patternLength = match.Length;
                    detectedType = SpanType.Strikethrough;
                    content = match.Groups["body"].Value;
                    break;
                }
                match = codeRegex.Match(remaining);
                if (match.Success)
                {
                    firstIdx = i;
                    patternLength = match.Length;
                    detectedType = SpanType.Code;
                    content = match.Groups["body"].Value;
                    break;
                }
            }
            
            if (firstIdx == -1)
            {
                target.Add(new Run { Text = text });
                return;
            }
            
            if (firstIdx > 0)
            {
                target.Add(new Run { Text = text.Substring(0, firstIdx) });
            }
            
            switch (detectedType)
            {
                case SpanType.Bold:
                    var boldSpan = new Bold();
                    ParseInlineRecursive(content, boldSpan.Inlines);
                    target.Add(boldSpan);
                    break;
                case SpanType.Italic:
                    var italicSpan = new Italic();
                    ParseInlineRecursive(content, italicSpan.Inlines);
                    target.Add(italicSpan);
                    break;
                case SpanType.Strikethrough:
                    var strikeSpan = new Span();
                    var strikeList = new List<Inline>();
                    ParseInlineRecursive(content, strikeList);
                    foreach (var inline in strikeList)
                    {
                        if (inline is Run run)
                        {
                            run.TextDecorations = TextDecorations.Strikethrough;
                        }
                        strikeSpan.Inlines.Add(inline);
                    }
                    target.Add(strikeSpan);
                    break;
                case SpanType.Code:
                    var codeRun = new Run
                    {
                        Text = content,
                        FontFamily = new FontFamily("Consolas"),
                        FontSize = 13,
                        Foreground = new SolidColorBrush(Colors.LightGray)
                    };
                    target.Add(codeRun);
                    break;
                case SpanType.Link:
                    try
                    {
                        var hyperlink = new Hyperlink();
                        if (Uri.TryCreate(extra, UriKind.Absolute, out var uri))
                        {
                            hyperlink.NavigateUri = uri;
                        }
                        ParseInlineRecursive(content, hyperlink.Inlines);
                        target.Add(hyperlink);
                    }
                    catch
                    {
                        var fallbackSpan = new Span();
                        fallbackSpan.Inlines.Add(new Run { Text = $"[{content}]({extra})" });
                        target.Add(fallbackSpan);
                    }
                    break;
            }
            
            int nextIndex = firstIdx + patternLength;
            if (nextIndex < text.Length)
            {
                ParseInlineRecursive(text.Substring(nextIndex), target);
            }
        }

        #endregion
    }
}
