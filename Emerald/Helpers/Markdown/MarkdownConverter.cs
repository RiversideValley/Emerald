using System;
using System.Collections.Generic;
using System.Linq;
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
using Microsoft.UI.Xaml.Media.Imaging;     // For BitmapImage
using Microsoft.UI.Xaml.Shapes;            // For Rectangle

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
using System.Runtime.CompilerServices;

namespace Emerald.Helpers.Markdown
{
    public class MarkdownConverter
    {
        /// <summary>
        /// Parses the given Markdown text and sets the corresponding formatted content
        /// on the provided RichTextBlock using Markdig.
        /// </summary>
        /// <param name="richTextBlock">The target RichTextBlock.</param>
        /// <param name="markdown">The Markdown text to convert.</param>
        public async Task SetMarkdownTextAsync(RichTextBlock richTextBlock, string markdown)
        {
            this.Log().LogInformation("Starting Markdown conversion.");
            var pipeline = new MarkdownPipelineBuilder()
                                .UseAdvancedExtensions()
                                .Build();

            // Fully qualify Markdig.Markdown.Parse to avoid namespace conflicts.
            MarkdownDocument document = Markdig.Markdown.Parse(markdown, pipeline);
            richTextBlock.Blocks.Clear();

            int blockCount = 0;
            foreach (XamlBlock block in ConvertBlocks(document))
            {
                this.Log().LogDebug("Adding block of type {BlockType} to RichTextBlock.", block.GetType().Name);
                richTextBlock.Blocks.Add(block);
                blockCount++;
            }

            if (blockCount == 0)
            {
                this.Log().LogWarning("No blocks were added to the RichTextBlock. Conversion may have failed.");
            }
            else
            {
                this.Log().LogInformation("Added {Count} blocks to the RichTextBlock.", blockCount);
            }

            this.Log().LogInformation("Markdown conversion completed.");
            await Task.CompletedTask;
        }

        #region Conversion Methods

        private IEnumerable<XamlBlock> ConvertBlocks(MarkdownDocument document)
        {
            this.Log().LogDebug("Converting document with {Count} top-level items.", document.Count());
            foreach (MarkdownObject markdownObj in document)
            {
                foreach (XamlBlock block in ConvertBlock(markdownObj))
                {
                    this.Log().LogDebug("Converted block: {BlockType}.", block.GetType().Name);
                    yield return block;
                }
            }
        }

        private IEnumerable<XamlBlock> ConvertBlock(MarkdownObject markdownObj)
        {
            this.Log().LogDebug("Converting block of type {Type}.", markdownObj.GetType().Name);
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
                    foreach (MarkdownObject child in container)
                    {
                        foreach (XamlBlock block in ConvertBlock(child))
                            yield return block;
                    }
                    break;
                default:
                    this.Log().LogWarning("Unhandled markdown block type: {Type}. Returning raw content.", markdownObj.GetType().Name);
                    // Fallback: output the raw markdown content as text.
                    XamlParagraph fallback = new XamlParagraph();
                    fallback.Inlines.Add(new XamlRun { Text = markdownObj.ToString() });
                    yield return fallback;
                    break;
            }
        }

        private XamlParagraph ConvertParagraphBlock(MdParagraphBlock paragraphBlock)
        {
            this.Log().LogDebug("Converting ParagraphBlock.");
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

        private XamlParagraph ConvertHeadingBlock(MdHeadingBlock headingBlock)
        {
            this.Log().LogDebug("Converting HeadingBlock of level {Level}.", headingBlock.Level);
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

        private double GetFontSizeForHeading(int level)
        {
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

        private XamlParagraph ConvertFencedCodeBlock(MdFencedCodeBlock codeBlock)
        {
            this.Log().LogDebug("Converting FencedCodeBlock.");
            XamlParagraph paragraph = new XamlParagraph
            {
                FontFamily = new FontFamily("Consolas")
            };

            string codeText = "";
            // codeBlock.Lines is a struct, so we directly access its Lines collection.
            var lines = codeBlock.Lines.Lines;
            if (lines != null)
            {
                foreach (var line in lines)
                {
                    codeText += line.Slice.ToString() + "\n";
                }
            }
            paragraph.Inlines.Add(new XamlRun { Text = codeText });
            return paragraph;
        }

        private XamlParagraph ConvertThematicBreakBlock(MdThematicBreakBlock _)
        {
            this.Log().LogDebug("Converting ThematicBreakBlock.");
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

        private XamlParagraph ConvertQuoteBlock(MdQuoteBlock quoteBlock)
        {
            this.Log().LogDebug("Converting QuoteBlock.");
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

        private IEnumerable<XamlBlock> ConvertListBlock(MdListBlock listBlock)
        {
            this.Log().LogDebug("Converting ListBlock.");
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

        private IEnumerable<XamlBlock> ConvertTableBlock(Markdig.Extensions.Tables.Table table)
        {
            this.Log().LogDebug("Converting Table block.");
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

        private IEnumerable<XamlInline> ConvertInlineCollection(MdContainerInline container)
        {
            foreach (MarkdownObject inlineObj in container)
            {
                XamlInline converted = ConvertInline(inlineObj);
                if (converted != null)
                {
                    yield return converted;
                }
            }
        }

        private XamlInline ConvertInline(MarkdownObject markdownObj)
        {
            this.Log().LogDebug("Converting inline of type {Type}.", markdownObj.GetType().Name);
            switch (markdownObj)
            {
                case MdLiteralInline literal:
                    return new XamlRun { Text = literal.Content.ToString() };

                case MdLineBreakInline _:
                    return new LineBreak();

                case MdCodeInline codeInline:
                    return new XamlRun
                    {
                        Text = codeInline.Content,
                        FontFamily = new FontFamily("Consolas")
                    };

                case MdEmphasisInline emphasis:
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
                        Image image = new Image { Height = 100 };
                        BitmapImage bitmap = new BitmapImage();
                        try
                        {
                            bitmap.UriSource = new Uri(link.Url);
                        }
                        catch (Exception ex)
                        {
                            this.Log().LogError(ex, "Error loading image from {Url}", link.Url);
                        }
                        image.Source = bitmap;
                        return new InlineUIContainer { Child = image };
                    }
                    else
                    {
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
                    XamlSpan span = new XamlSpan();
                    foreach (XamlInline child in ConvertInlineCollection(container))
                    {
                        span.Inlines.Add(child);
                    }
                    return span;

                default:
                    this.Log().LogWarning("Unhandled inline type: {Type}.", markdownObj.GetType().Name);
                    return null;
            }
        }

        #endregion
    }
}
