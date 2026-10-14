using System.Windows;
using System.Windows.Media;

namespace VisionStudio.App.Controls;

/// <summary>
/// Lightweight WPF FrameworkElement hosting a vector DrawingVisual for fast overlay rendering.
/// </summary>
public sealed class DrawingVisualHost : FrameworkElement
{
    private readonly VisualCollection _children;
    private readonly DrawingVisual _visual;

    public DrawingVisual DrawingVisual => _visual;

    public DrawingVisualHost()
    {
        _visual = new DrawingVisual();
        _children = new VisualCollection(this) { _visual };
    }

    protected override int VisualChildrenCount => _children.Count;

    protected override Visual GetVisualChild(int index)
    {
        if (index < 0 || index >= _children.Count) throw new ArgumentOutOfRangeException(nameof(index));
        return _children[index];
    }
}
