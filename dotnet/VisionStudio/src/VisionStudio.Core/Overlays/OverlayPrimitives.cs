using OpenCvSharp;

namespace VisionStudio.Core.Overlays;

/// <summary>
/// Common interface for visual inspection overlay primitives.
/// </summary>
public interface IOverlayPrimitive
{
    /// <summary>
    /// Renders the overlay directly onto an OpenCV Mat.
    /// </summary>
    void Render(Mat canvas);
}

/// <summary>
/// Straight line overlay primitive.
/// </summary>
public sealed record LineOverlay(Point2f P1, Point2f P2, Scalar Color, int Thickness = 1) : IOverlayPrimitive
{
    public void Render(Mat canvas)
    {
        Cv2.Line(canvas, (Point)P1, (Point)P2, Color, Thickness, LineTypes.AntiAlias);
    }
}

/// <summary>
/// Circle overlay primitive.
/// </summary>
public sealed record CircleOverlay(Point2f Center, float Radius, Scalar Color, int Thickness = 1, bool Filled = false) : IOverlayPrimitive
{
    public void Render(Mat canvas)
    {
        Cv2.Circle(canvas, (Point)Center, (int)Radius, Color, Filled ? -1 : Thickness, LineTypes.AntiAlias);
    }
}

/// <summary>
/// Axis-aligned rectangle overlay primitive.
/// </summary>
public sealed record RectOverlay(Rect2f Rect, Scalar Color, int Thickness = 1, bool Filled = false) : IOverlayPrimitive
{
    public void Render(Mat canvas)
    {
        var r = new Rect((int)Rect.X, (int)Rect.Y, (int)Rect.Width, (int)Rect.Height);
        Cv2.Rectangle(canvas, r, Color, Filled ? -1 : Thickness, LineTypes.AntiAlias);
    }
}

/// <summary>
/// Rotated rectangle overlay primitive.
/// </summary>
public sealed record RotatedRectOverlay(RotatedRect Rect, Scalar Color, int Thickness = 1) : IOverlayPrimitive
{
    public void Render(Mat canvas)
    {
        var pts = Rect.Points();
        for (int i = 0; i < 4; i++)
        {
            Cv2.Line(canvas, (Point)pts[i], (Point)pts[(i + 1) % 4], Color, Thickness, LineTypes.AntiAlias);
        }
    }
}

/// <summary>
/// Polygon overlay primitive.
/// </summary>
public sealed record PolygonOverlay(IReadOnlyList<Point2f> Points, Scalar Color, int Thickness = 1, bool Filled = false) : IOverlayPrimitive
{
    public void Render(Mat canvas)
    {
        if (Points == null || Points.Count < 2) return;
        var cvPoints = Points.Select(p => (Point)p).ToArray();
        if (Filled)
        {
            Cv2.FillPoly(canvas, new[] { cvPoints }, Color, LineTypes.AntiAlias);
        }
        else
        {
            Cv2.Polylines(canvas, new[] { cvPoints }, isClosed: true, Color, Thickness, LineTypes.AntiAlias);
        }
    }
}

/// <summary>
/// Text label overlay primitive.
/// </summary>
public sealed record TextOverlay(string Text, Point2f Org, Scalar Color, double FontScale = 0.6, int Thickness = 1) : IOverlayPrimitive
{
    public void Render(Mat canvas)
    {
        if (string.IsNullOrEmpty(Text)) return;
        try
        {
            // Cv2.PutText P/Invoke uses ANSI marshaler which crashes on emojis/non-ASCII.
            // Sanitize for OpenCV Mat rendering; WPF UI viewer uses full Unicode text directly.
            var ascii = System.Text.RegularExpressions.Regex.Replace(Text, @"[^\u0020-\u007E]+", " ");
            Cv2.PutText(canvas, ascii, (Point)Org, HersheyFonts.HersheySimplex, FontScale, Color, Thickness, LineTypes.AntiAlias);
        }
        catch
        {
            // Suppress marshaling errors on OpenCV rasterization
        }
    }
}

/// <summary>
/// Reticle crosshair overlay primitive.
/// </summary>
public sealed record CrossOverlay(Point2f Center, float Size, Scalar Color, int Thickness = 1) : IOverlayPrimitive
{
    public void Render(Mat canvas)
    {
        var half = Size / 2f;
        Cv2.Line(canvas, new Point(Center.X - half, Center.Y), new Point(Center.X + half, Center.Y), Color, Thickness);
        Cv2.Line(canvas, new Point(Center.X, Center.Y - half), new Point(Center.X, Center.Y + half), Color, Thickness);
    }
}
