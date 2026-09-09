using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;
namespace Emerald.Controls;

/// <summary>Wraps profile cards, measuring every card so long names cannot underflow the action area.</summary>
public sealed class ResponsiveCardPanel : Panel
{
    private const double Gap = 16;
    private const double MinimumWidth = 280;
    private int Columns(double width) => Math.Max(1, (int)((width + Gap) / (MinimumWidth + Gap)));

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? MinimumWidth : availableSize.Width;
        var columns = Columns(width);
        var itemWidth = Math.Max(0, (width - (columns - 1) * Gap) / columns);
        double height = 0;
        for (var i = 0; i < Children.Count; i += columns)
        {
            double rowHeight = 0;
            for (var j = i; j < Math.Min(i + columns, Children.Count); j++)
            {
                Children[j].Measure(new Size(itemWidth, double.PositiveInfinity));
                rowHeight = Math.Max(rowHeight, Children[j].DesiredSize.Height);
            }

            height += rowHeight + (i == 0 ? 0 : Gap);
        }

        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var columns = Columns(finalSize.Width);
        var width = Math.Max(0, (finalSize.Width - (columns - 1) * Gap) / columns);
        double y = 0;
        for (var i = 0; i < Children.Count; i += columns)
        {
            var height = Enumerable.Range(i, Math.Min(columns, Children.Count - i))
                .Max(j => Children[j].DesiredSize.Height);
            for (var j = i; j < Math.Min(i + columns, Children.Count); j++)
                Children[j].Arrange(new Rect((j - i) * (width + Gap), y, width, height));
            y += height + Gap;
        }

        return finalSize;
    }
}
