using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SDLauncher_UWP
{
    class XAMLHtml
    {
        public class HtmlProperties : DependencyObject
        {
            public static readonly DependencyProperty HtmlProperty =
                DependencyProperty.RegisterAttached(
                    "Html",
                    typeof(string),
                    typeof(HtmlProperties),
                    new PropertyMetadata(null, HtmlChanged));

            private static RichTextBlock _currentObject;

            private static void HtmlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            {
                var richText = d as RichTextBlock;
                if (richText == null) return;

                _currentObject = richText;

                //Generate blocks
                var xhtml = e.NewValue as string;
                var blocks = GenerateBlocksForHtml(xhtml);

                _currentObject = null;

                //Add the blocks to the RichTextBlock
                richText.Blocks.Clear();
                foreach (var b in blocks)
                    richText.Blocks.Add(b);
            }

            private static List<Block> GenerateBlocksForHtml(string xhtml)
            {
                var blocks = new List<Block>();

                try
                {
                    var doc = new HtmlDocument();
                    doc.LoadHtml(xhtml);

                    var block = GenerateParagraph(doc.DocumentNode);
                    blocks.Add(block);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }

                return blocks;
            }

            // TODO this method seams to be removing necessary spaces in #text nodes
            private static string CleanText(string input)
            {
                var clean = Windows.Data.Html.HtmlUtilities.ConvertToText(input);
                //clean = System.Net.WebUtility.HtmlEncode(clean);
                if (clean == "\0")
                    clean = "\n";
                return clean;
            }

            private static Block GenerateBlockForTopNode(HtmlNode node)
                => GenerateParagraph(node);


            private static void AddChildren(Paragraph p, HtmlNode node)
            {
                var added = false;
                foreach (var child in node.ChildNodes)
                {
                    var i = GenerateBlockForNode(child);
                    if (i != null)
                    {
                        p.Inlines.Add(i);
                        added = true;
                    }
                }
                if (!added)
                {
                    p.Inlines.Add(new Run { Text = CleanText(node.InnerText) });
                }
            }

            private static void AddChildren(Span s, HtmlNode node)
            {
                var added = false;

                foreach (var child in node.ChildNodes)
                {
                    var i = GenerateBlockForNode(child);
                    if (i != null)
                    {
                        s.Inlines.Add(i);
                        added = true;
                    }
                }
                if (!added)
                {
                    s.Inlines.Add(new Run { Text = CleanText(node.InnerText) });
                }
            }

            private static Inline GenerateBlockForNode(HtmlNode node)
            {
                switch (node.Name)
                {


                    case "b":
                    case "B":
                    case "strong":
                    case "STRONG":
                        return GenerateBold(node);
                    case "i":
                    case "I":
                    case "em":
                    case "EM":
                        return GenerateItalic(node);
                    case "u":
                    case "U":
                        return GenerateUnderline(node);
                    case "br":
                    case "BR":
                        return new LineBreak();
                    default:
                        return GenerateSpanWNewLine(node);
                }

            }

            private static Inline GenerateBold(HtmlNode node)
            {
                var bold = new Bold();
                AddChildren(bold, node);
                return bold;
            }

            private static Inline GenerateUnderline(HtmlNode node)
            {
                var underline = new Underline();
                AddChildren(underline, node);
                return underline;
            }

            private static Inline GenerateItalic(HtmlNode node)
            {
                var italic = new Italic();
                AddChildren(italic, node);
                return italic;
            }

            private static Block GenerateParagraph(HtmlNode node)
            {
                var paragraph = new Paragraph();
                AddChildren(paragraph, node);
                return paragraph;
            }


            private static Inline GenerateSpanWNewLine(HtmlNode node)
            {
                var span = new Span();
                AddChildren(span, node);
                if (span.Inlines.Count > 0)
                    span.Inlines.Add(new LineBreak());
                return span;
            }

        }
    }
}
