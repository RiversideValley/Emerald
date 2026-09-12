using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace Emerald.Controls;

/// <summary>Equal-width tracks for short charts and responsive card rows.</summary>
public sealed class EqualColumnPanel : Panel
{
    public double Spacing { get; set; } = 8;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? Children.Count * 48 : availableSize.Width;
        var totalSpacing = Spacing * Math.Max(0, Children.Count - 1);
        var itemWidth = Math.Max(0, (width - totalSpacing) / Math.Max(1, Children.Count));
        double height = 0;
        foreach (var child in Children)
        {
            child.Measure(new Size(itemWidth, availableSize.Height));
            height = Math.Max(height, child.DesiredSize.Height);
        }

        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var totalSpacing = Spacing * Math.Max(0, Children.Count - 1);
        var width = Math.Max(0, (finalSize.Width - totalSpacing) / Math.Max(1, Children.Count));
        for (var i = 0; i < Children.Count; i++)
        {
            Children[i].Arrange(new Rect(i * (width + Spacing), 0, width, finalSize.Height));
        }

        return finalSize;
    }
}
