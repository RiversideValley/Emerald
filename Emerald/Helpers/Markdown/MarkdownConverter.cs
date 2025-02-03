using System;
using System.Collections.Generic;
using System.Linq; // For FirstOrDefault, IndexOf, etc.
using System.Threading.Tasks;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.UI.Text;                   // For FontWeights
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;             // For FontFamily, SolidColorBrush
using Microsoft.UI.Xaml.Media.Imaging;      // For BitmapImage
using Microsoft.UI.Xaml.Shapes;             // For Rectangle

// --- Aliases for Markdig (Markdown) types ---
using MdBlock = Markdig.Syntax.Block;
using MdParagraphBlock = Markdig.Syntax.ParagraphBlock;
using MdHeadingBlock = Markdig.Syntax.HeadingBlock;
using MdFencedCodeBlock = Markdig.Syntax.FencedCodeBlock;
using MdThematicBreakBlock = Markdig.Syntax.ThematicBreakBlock;
using MdQuoteBlock = Markdig.Syntax.QuoteBlock;
using MdListBlock = Markdig.Syntax.ListBlock;
using MdListItemBlock = Markdig.Syntax.ListItemBlock;
using MdContainerBlock = Markdig.Syntax.ContainerBlock;

using MdInline = Markdig.Syntax.Inlines.Inline;
using MdLiteralInline = Markdig.Syntax.Inlines.LiteralInline;
using MdLineBreakInline = Markdig.Syntax.Inlines.LineBreakInline;
using MdCodeInline = Markdig.Syntax.Inlines.CodeInline;
using MdEmphasisInline = Markdig.Syntax.Inlines.EmphasisInline;
using MdLinkInline = Markdig.Syntax.Inlines.LinkInline;
using MdContainerInline = Markdig.Syntax.Inlines.ContainerInline;

// --- Aliases for WinUI (RichTextBlock) types ---
using XamlBlock = Microsoft.UI.Xaml.Documents.Block;
using XamlParagraph = Microsoft.UI.Xaml.Documents.Paragraph;
using XamlRun = Microsoft.UI.Xaml.Documents.Run;
using XamlBold = Microsoft.UI.Xaml.Documents.Bold;
using XamlItalic = Microsoft.UI.Xaml.Documents.Italic;
using XamlHyperlink = Microsoft.UI.Xaml.Documents.Hyperlink;
using XamlSpan = Microsoft.UI.Xaml.Documents.Span;
using XamlInline = Microsoft.UI.Xaml.Documents.Inline;

namespace Emerald.Helpers.Markdown;

public static class MarkdownConverter
{
    /// <summary>
    /// Parses the given Markdown text and sets the corresponding formatted content
    /// on the provided RichTextBlock using Markdig.
    /// </summary>
    /// <param name="richTextBlock">The target RichTextBlock.</param>
    /// <param name="markdown">The Markdown text to convert.</param>
    public static async Task SetMarkdownTextAsync(RichTextBlock richTextBlock, string markdown)
    {
        // Build a pipeline with advanced extensions (footnotes, task lists, tables, etc.)
        var pipeline = new MarkdownPipelineBuilder()
                            .UseAdvancedExtensions()
                            .Build();

        // Parse the Markdown into a syntax tree
        MarkdownDocument document = Markdig.Markdown.Parse(markdown, pipeline);

        // Clear any existing blocks
        richTextBlock.Blocks.Clear();

        // Convert each Markdig block into a corresponding WinUI Block
        foreach (XamlBlock block in ConvertBlocks(document))
        {
            richTextBlock.Blocks.Add(block);
        }

        await Task.CompletedTask;
    }

    #region Conversion Methods

    private static IEnumerable<XamlBlock> ConvertBlocks(MarkdownDocument document)
    {
        foreach (MarkdownObject markdownObj in document)
        {
            foreach (XamlBlock block in ConvertBlock(markdownObj))
            {
                yield return block;
            }
        }
    }

    /// <summary>
    /// Converts a single Markdig MarkdownObject into one or more WinUI Blocks.
    /// </summary>
    private static IEnumerable<XamlBlock> ConvertBlock(MarkdownObject markdownObj)
    {
        switch (markdownObj)
        {
            case MdParagraphBlock pb:
                yield return ConvertParagraphBlock(pb);
                break;

            case MdHeadingBlock hb:
                yield return ConvertHeadingBlock(hb);
                break;

            case MdFencedCodeBlock fcb:
                yield return ConvertFencedCodeBlock(fcb);
                break;

            case MdThematicBreakBlock thb:
                yield return ConvertThematicBreakBlock(thb);
                break;

            case MdQuoteBlock qb:
                yield return ConvertQuoteBlock(qb);
                break;

            case MdListBlock lb:
                foreach (XamlBlock listBlock in ConvertListBlock(lb))
                    yield return listBlock;
                break;

            case Markdig.Extensions.Tables.Table table:
                foreach (XamlBlock tableBlock in ConvertTableBlock(table))
                    yield return tableBlock;
                break;

            case MdContainerBlock container:
                // A container block might hold child blocks, so convert them recursively
                foreach (MarkdownObject child in container)
                {
                    foreach (XamlBlock block in ConvertBlock(child))
                        yield return block;
                }
                break;

            default:
                // Optionally, handle unknown blocks as plain text or ignore them
                break;
        }
    }

    private static XamlParagraph ConvertParagraphBlock(MdParagraphBlock paragraphBlock)
    {
        XamlParagraph paragraph = new XamlParagraph();

        if (paragraphBlock.Inline != null)
        {
            foreach (XamlInline inline in ConvertInlineCollection(paragraphBlock.Inline))
            {
                paragraph.Inlines.Add(inline);
            }
        }

        return paragraph;
    }

    private static XamlParagraph ConvertHeadingBlock(MdHeadingBlock headingBlock)
    {
        XamlParagraph paragraph = new XamlParagraph
        {
            FontSize = GetFontSizeForHeading(headingBlock.Level),
            FontWeight = FontWeights.Bold
        };

        if (headingBlock.Inline != null)
        {
            foreach (XamlInline inline in ConvertInlineCollection(headingBlock.Inline))
            {
                paragraph.Inlines.Add(inline);
            }
        }

        return paragraph;
    }

    private static double GetFontSizeForHeading(int level)
    {
        // Example mapping: H1=32, H2=28, H3=24, H4=20, H5=16, H6=14
        return level switch
        {
            1 => 32,
            2 => 28,
            3 => 24,
            4 => 20,
            5 => 16,
            6 => 14,
            _ => 14,
        };
    }

    /// <summary>
    /// Creates a paragraph for fenced code blocks. 
    /// Note that WinUI's Paragraph does not support a Background property,
    /// so if you want a background or a border, you'd need to wrap this in an InlineUIContainer.
    /// </summary>
    private static XamlParagraph ConvertFencedCodeBlock(MdFencedCodeBlock codeBlock)
    {
        XamlParagraph paragraph = new XamlParagraph
        {
            FontFamily = new FontFamily("Consolas")
        };

        // Combine all lines of code
        string codeText = "";
        foreach (var line in codeBlock.Lines.Lines)
        {
            codeText += line.Slice.ToString() + "\n";
        }
        paragraph.Inlines.Add(new XamlRun { Text = codeText });

        return paragraph;
    }

    private static XamlParagraph ConvertThematicBreakBlock(MdThematicBreakBlock _)
    {
        // A simple horizontal rule
        XamlParagraph paragraph = new XamlParagraph();

        Rectangle line = new Rectangle
        {
            Height = 1,
            Fill = new SolidColorBrush(Microsoft.UI.Colors.Gray),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        InlineUIContainer container = new InlineUIContainer { Child = line };
        paragraph.Inlines.Add(container);

        return paragraph;
    }

    private static XamlParagraph ConvertQuoteBlock(MdQuoteBlock quoteBlock)
    {
        // WinUI Paragraph doesn't have Margin, so we use TextIndent for a simple indentation
        XamlParagraph paragraph = new XamlParagraph
        {
            TextIndent = 20
        };

        foreach (MarkdownObject child in quoteBlock)
        {
            foreach (XamlBlock block in ConvertBlock(child))
            {
                if (block is XamlParagraph childParagraph)
                {
                    foreach (XamlInline inline in childParagraph.Inlines)
                    {
                        paragraph.Inlines.Add(inline);
                    }
                    paragraph.Inlines.Add(new LineBreak());
                }
            }
        }

        return paragraph;
    }

    private static IEnumerable<XamlBlock> ConvertListBlock(MdListBlock listBlock)
    {
        int index = 1;
        foreach (var listItem in listBlock)
        {
            if (listItem is MdListItemBlock item)
            {
                foreach (MarkdownObject child in item)
                {
                    foreach (XamlBlock block in ConvertBlock(child))
                    {
                        if (block is XamlParagraph paragraph)
                        {
                            // Prepend a bullet or number
                            string prefix = listBlock.IsOrdered ? $"{index}. " : "• ";
                            XamlRun prefixRun = new XamlRun { Text = prefix };

                            var firstInline = paragraph.Inlines.FirstOrDefault();
                            if (firstInline != null)
                            {
                                int idx = paragraph.Inlines.IndexOf(firstInline);
                                if (idx >= 0)
                                {
                                    paragraph.Inlines.Insert(idx, prefixRun);
                                }
                            }
                            else
                            {
                                paragraph.Inlines.Add(prefixRun);
                            }

                            // Use TextIndent to visually separate list items
                            paragraph.TextIndent = 20;
                            yield return paragraph;
                        }
                        else
                        {
                            yield return block;
                        }
                    }
                }
                index++;
            }
        }
    }

    private static IEnumerable<XamlBlock> ConvertTableBlock(Markdig.Extensions.Tables.Table table)
    {
        // Basic table conversion: each row is a Paragraph, cells separated by tabs
        bool isHeader = true;
        foreach (MarkdownObject rowObj in table)
        {
            if (rowObj is Markdig.Extensions.Tables.TableRow row)
            {
                XamlParagraph paragraph = new XamlParagraph();
                int cellIndex = 0;

                foreach (MarkdownObject cellObj in row)
                {
                    if (cellObj is Markdig.Extensions.Tables.TableCell cell)
                    {
                        string cellText = "";
                        foreach (MarkdownObject child in cell)
                        {
                            if (child is MdParagraphBlock pb && pb.Inline != null)
                            {
                                foreach (XamlInline inline in ConvertInlineCollection(pb.Inline))
                                {
                                    if (inline is XamlRun run)
                                    {
                                        cellText += run.Text;
                                    }
                                }
                            }
                        }

                        XamlRun runCell = new XamlRun { Text = cellText };
                        if (isHeader)
                        {
                            runCell.FontWeight = FontWeights.Bold;
                        }
                        paragraph.Inlines.Add(runCell);

                        // Add a tab between cells (except after the last cell)
                        if (cellIndex < row.Count - 1)
                        {
                            paragraph.Inlines.Add(new XamlRun { Text = "\t" });
                        }
                        cellIndex++;
                    }
                }

                yield return paragraph;
                isHeader = false;
            }
        }
    }

    /// <summary>
    /// Converts a Markdig inline container into a collection of WinUI inlines.
    /// </summary>
    private static IEnumerable<XamlInline> ConvertInlineCollection(MdContainerInline container)
    {
        foreach (MarkdownObject inlineObj in container)
        {
            XamlInline converted = ConvertInline(inlineObj);
            if (converted != null)
                yield return converted;
        }
    }

    /// <summary>
    /// Converts a single Markdig inline element into a WinUI Inline.
    /// </summary>
    private static XamlInline ConvertInline(MarkdownObject markdownObj)
    {
        switch (markdownObj)
        {
            case MdLiteralInline literal:
                return new XamlRun { Text = literal.Content.ToString() };

            case MdLineBreakInline _:
                return new LineBreak();

            case MdCodeInline codeInline:
                // Note: WinUI's Run doesn't have a Background property. 
                // If you want a background highlight, wrap this in an InlineUIContainer with a Border.
                return new XamlRun
                {
                    Text = codeInline.Content,
                    FontFamily = new FontFamily("Consolas")
                };

            case MdEmphasisInline emphasis:
                // Double emphasis => Bold; single => Italic
                if (emphasis.IsDouble)
                {
                    XamlBold bold = new XamlBold();
                    foreach (XamlInline child in ConvertInlineCollection(emphasis))
                    {
                        bold.Inlines.Add(child);
                    }
                    return bold;
                }
                else
                {
                    XamlItalic italic = new XamlItalic();
                    foreach (XamlInline child in ConvertInlineCollection(emphasis))
                    {
                        italic.Inlines.Add(child);
                    }
                    return italic;
                }

            case MdLinkInline link:
                if (link.IsImage)
                {
                    // Render images (local or remote) as an Image control in an InlineUIContainer
                    Image image = new Image { Height = 100 };
                    BitmapImage bitmap = new BitmapImage();

                    try
                    {
                        bitmap.UriSource = new Uri(link.Url);
                    }
                    catch
                    {
                        // If URL is invalid, ignore or handle as needed
                    }

                    image.Source = bitmap;
                    return new InlineUIContainer { Child = image };
                }
                else
                {
                    // Render hyperlinks
                    XamlHyperlink hyperlink = new XamlHyperlink
                    {
                        NavigateUri = new Uri(link.Url)
                    };

                    foreach (XamlInline child in ConvertInlineCollection(link))
                    {
                        hyperlink.Inlines.Add(child);
                    }
                    return hyperlink;
                }

            case MdContainerInline container:
                // A container might hold nested inlines (like nested emphasis)
                XamlSpan span = new XamlSpan();
                foreach (XamlInline child in ConvertInlineCollection(container))
                {
                    span.Inlines.Add(child);
                }
                return span;

            default:
                // Unknown inline => return null or handle differently
                return null;
        }
    }

    #endregion
}
