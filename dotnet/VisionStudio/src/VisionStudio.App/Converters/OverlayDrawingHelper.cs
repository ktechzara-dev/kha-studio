using System.Globalization;
using System.Windows;
using System.Windows.Media;
using OpenCvSharp;
using VisionStudio.Core.Overlays;

namespace VisionStudio.App.Converters;

/// <summary>
/// Renders sub-pixel vector overlays (lines, circles, rotated boxes, polygons, text, crosshairs)
/// onto a WPF DrawingContext with anti-aliasing and sub-pixel precision.
/// </summary>
public static class OverlayDrawingHelper
{
    private static readonly Typeface DefaultTypeface = new(
        new FontFamily("Segoe UI, Consolas, sans-serif"),
        FontStyles.Normal,
        FontWeights.SemiBold,
        FontStretches.Normal);

    /// <summary>
    /// Converts OpenCV Scalar (BGR or BGRA) to WPF Color.
    /// </summary>
    public static System.Windows.Media.Color ToWpfColor(Scalar s, byte alpha = 255)
    {
        // OpenCV Scalar is B, G, R, [A]
        var b = (byte)Math.Clamp(s.Val0, 0, 255);
        var g = (byte)Math.Clamp(s.Val1, 0, 255);
        var r = (byte)Math.Clamp(s.Val2, 0, 255);
        var a = s.Val3 > 0 ? (byte)Math.Clamp(s.Val3, 0, 255) : alpha;
        return System.Windows.Media.Color.FromArgb(a, r, g, b);
    }

    /// <summary>
    /// Renders a collection of overlay primitives onto a WPF DrawingContext.
    /// </summary>
    public static void RenderOverlays(DrawingContext dc, IEnumerable<IOverlayPrimitive>? overlays, double zoom = 1.0)
    {
        if (overlays == null) return;

        foreach (var overlay in overlays)
        {
            switch (overlay)
            {
                case LineOverlay line:
                    RenderLine(dc, line);
                    break;
                case CircleOverlay circle:
                    RenderCircle(dc, circle);
                    break;
                case RectOverlay rect:
                    RenderRect(dc, rect);
                    break;
                case RotatedRectOverlay rotRect:
                    RenderRotatedRect(dc, rotRect);
                    break;
                case PolygonOverlay poly:
                    RenderPolygon(dc, poly);
                    break;
                case TextOverlay text:
                    RenderText(dc, text);
                    break;
                case CrossOverlay cross:
                    RenderCross(dc, cross);
                    break;
            }
        }
    }

    private static void RenderLine(DrawingContext dc, LineOverlay line)
    {
        var color = ToWpfColor(line.Color);
        var pen = new Pen(new SolidColorBrush(color), Math.Max(1.0, line.Thickness));
        pen.Freeze();
        dc.DrawLine(pen, new System.Windows.Point(line.P1.X, line.P1.Y), new System.Windows.Point(line.P2.X, line.P2.Y));
    }

    private static void RenderCircle(DrawingContext dc, CircleOverlay circle)
    {
        var color = ToWpfColor(circle.Color);
        var pen = new Pen(new SolidColorBrush(color), Math.Max(1.0, circle.Thickness));
        pen.Freeze();

        Brush? fill = circle.Filled ? new SolidColorBrush(System.Windows.Media.Color.FromArgb(100, color.R, color.G, color.B)) : null;
        if (fill != null && fill.CanFreeze) fill.Freeze();

        dc.DrawEllipse(fill, pen, new System.Windows.Point(circle.Center.X, circle.Center.Y), circle.Radius, circle.Radius);
    }

    private static void RenderRect(DrawingContext dc, RectOverlay rect)
    {
        var color = ToWpfColor(rect.Color);
        var pen = new Pen(new SolidColorBrush(color), Math.Max(1.0, rect.Thickness));
        pen.Freeze();

        Brush? fill = rect.Filled ? new SolidColorBrush(System.Windows.Media.Color.FromArgb(80, color.R, color.G, color.B)) : null;
        if (fill != null && fill.CanFreeze) fill.Freeze();

        var r = new System.Windows.Rect(rect.Rect.X, rect.Rect.Y, rect.Rect.Width, rect.Rect.Height);
        dc.DrawRectangle(fill, pen, r);
    }

    private static void RenderRotatedRect(DrawingContext dc, RotatedRectOverlay rotRect)
    {
        var color = ToWpfColor(rotRect.Color);
        var pen = new Pen(new SolidColorBrush(color), Math.Max(1.0, rotRect.Thickness));
        pen.Freeze();

        var pts = rotRect.Rect.Points();
        if (pts.Length < 4) return;

        var streamGeo = new StreamGeometry();
        using (var ctx = streamGeo.Open())
        {
            ctx.BeginFigure(new System.Windows.Point(pts[0].X, pts[0].Y), isFilled: false, isClosed: true);
            for (int i = 1; i < pts.Length; i++)
            {
                ctx.LineTo(new System.Windows.Point(pts[i].X, pts[i].Y), isStroked: true, isSmoothJoin: false);
            }
        }
        streamGeo.Freeze();
        dc.DrawGeometry(null, pen, streamGeo);
    }

    private static void RenderPolygon(DrawingContext dc, PolygonOverlay poly)
    {
        if (poly.Points == null || poly.Points.Count < 2) return;

        var color = ToWpfColor(poly.Color);
        var pen = new Pen(new SolidColorBrush(color), Math.Max(1.0, poly.Thickness));
        pen.Freeze();

        Brush? fill = poly.Filled ? new SolidColorBrush(System.Windows.Media.Color.FromArgb(80, color.R, color.G, color.B)) : null;
        if (fill != null && fill.CanFreeze) fill.Freeze();

        var streamGeo = new StreamGeometry();
        using (var ctx = streamGeo.Open())
        {
            ctx.BeginFigure(new System.Windows.Point(poly.Points[0].X, poly.Points[0].Y), isFilled: poly.Filled, isClosed: true);
            for (int i = 1; i < poly.Points.Count; i++)
            {
                ctx.LineTo(new System.Windows.Point(poly.Points[i].X, poly.Points[i].Y), isStroked: true, isSmoothJoin: false);
            }
        }
        streamGeo.Freeze();
        dc.DrawGeometry(fill, pen, streamGeo);
    }

    private static void RenderText(DrawingContext dc, TextOverlay text)
    {
        if (string.IsNullOrEmpty(text.Text)) return;

        var color = ToWpfColor(text.Color);
        var brush = new SolidColorBrush(color);
        brush.Freeze();

        var fontSize = Math.Max(11.0, text.FontScale * 20.0);
        var formattedText = new FormattedText(
            text.Text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            DefaultTypeface,
            fontSize,
            brush,
            1.25); // PixelsPerDip

        // Draw translucent dark background box behind text for readability on any background
        var bgRect = new System.Windows.Rect(text.Org.X - 2, text.Org.Y - formattedText.Height, formattedText.Width + 4, formattedText.Height + 2);
        var bgBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(180, 15, 23, 42));
        bgBrush.Freeze();
        dc.DrawRoundedRectangle(bgBrush, null, bgRect, 2, 2);

        dc.DrawText(formattedText, new System.Windows.Point(text.Org.X, text.Org.Y - formattedText.Height));
    }

    private static void RenderCross(DrawingContext dc, CrossOverlay cross)
    {
        var color = ToWpfColor(cross.Color);
        var pen = new Pen(new SolidColorBrush(color), Math.Max(1.0, cross.Thickness));
        pen.Freeze();

        var half = cross.Size / 2.0;
        dc.DrawLine(pen, new System.Windows.Point(cross.Center.X - half, cross.Center.Y), new System.Windows.Point(cross.Center.X + half, cross.Center.Y));
        dc.DrawLine(pen, new System.Windows.Point(cross.Center.X, cross.Center.Y - half), new System.Windows.Point(cross.Center.X, cross.Center.Y + half));
    }
}
