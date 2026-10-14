using System.Text.Json.Serialization;
using OpenCvSharp;

namespace VisionStudio.Core.Types;

/// <summary>
/// A horizontal pixel run for Run-Length Encoded (RLE) region storage.
/// </summary>
public readonly record struct RunLengthSpan(int Y, int StartX, int EndX)
{
    public int Length => Math.Max(0, EndX - StartX + 1);
}

/// <summary>
/// First-class 2D industrial inspection Region supporting RLE, polygon contours, 
/// boolean set operations, morphology, and geometric measurements.
/// </summary>
public sealed class Region
{
    private List<RunLengthSpan> _runs;
    private Rect? _cachedBounds;
    private double? _cachedArea;
    private (double X, double Y)? _cachedCentroid;

    public IReadOnlyList<RunLengthSpan> Runs => _runs;

    public Region()
    {
        _runs = new List<RunLengthSpan>();
    }

    public Region(IEnumerable<RunLengthSpan> spans)
    {
        _runs = NormalizeSpans(spans);
    }

    /// <summary>
    /// Total pixel count (area) of the region.
    /// </summary>
    public double Area
    {
        get
        {
            if (!_cachedArea.HasValue)
            {
                double sum = 0;
                foreach (var r in _runs)
                {
                    sum += r.Length;
                }
                _cachedArea = sum;
            }
            return _cachedArea.Value;
        }
    }

    /// <summary>
    /// Minimal axis-aligned bounding box enclosing the region.
    /// </summary>
    public Rect BoundingBox
    {
        get
        {
            if (!_cachedBounds.HasValue)
            {
                if (_runs.Count == 0)
                {
                    _cachedBounds = new Rect(0, 0, 0, 0);
                }
                else
                {
                    int minX = int.MaxValue;
                    int maxX = int.MinValue;
                    int minY = _runs[0].Y;
                    int maxY = _runs[^1].Y;

                    foreach (var r in _runs)
                    {
                        if (r.StartX < minX) minX = r.StartX;
                        if (r.EndX > maxX) maxX = r.EndX;
                    }

                    _cachedBounds = new Rect(minX, minY, Math.Max(0, maxX - minX + 1), Math.Max(0, maxY - minY + 1));
                }
            }
            return _cachedBounds.Value;
        }
    }

    /// <summary>
    /// Center of mass (centroid) of the region.
    /// </summary>
    public (double X, double Y) Centroid
    {
        get
        {
            if (!_cachedCentroid.HasValue)
            {
                if (_runs.Count == 0 || Area <= 0)
                {
                    _cachedCentroid = (0, 0);
                }
                else
                {
                    double sumX = 0;
                    double sumY = 0;
                    double totalPts = 0;

                    foreach (var r in _runs)
                    {
                        int len = r.Length;
                        if (len > 0)
                        {
                            double avgX = (r.StartX + r.EndX) / 2.0;
                            sumX += avgX * len;
                            sumY += r.Y * len;
                            totalPts += len;
                        }
                    }

                    _cachedCentroid = totalPts > 0 ? (sumX / totalPts, sumY / totalPts) : (0, 0);
                }
            }
            return _cachedCentroid.Value;
        }
    }

    public bool IsEmpty => _runs.Count == 0;

    #region Factory Methods

    public static Region Empty => new();

    public static Region FromRect(Rect rect)
    {
        if (rect.Width <= 0 || rect.Height <= 0) return Empty;

        var spans = new List<RunLengthSpan>(rect.Height);
        int endX = rect.X + rect.Width - 1;
        for (int y = rect.Y; y < rect.Y + rect.Height; y++)
        {
            spans.Add(new RunLengthSpan(y, rect.X, endX));
        }
        return new Region(spans);
    }

    public static Region FromPolygon(IEnumerable<Point> points, int width = 4096, int height = 4096)
    {
        var pts = points.ToArray();
        if (pts.Length < 3) return Empty;

        using var mask = new Mat(height, width, MatType.CV_8UC1, Scalar.All(0));
        var contours = new[] { pts };
        Cv2.FillPoly(mask, contours, Scalar.All(255));
        return FromMask(mask);
    }

    public static Region FromCircle(Point center, double radius, int width = 4096, int height = 4096)
    {
        if (radius <= 0) return Empty;

        int r = (int)Math.Ceiling(radius);
        int minY = Math.Max(0, center.Y - r);
        int maxY = Math.Min(height - 1, center.Y + r);
        var spans = new List<RunLengthSpan>();

        for (int y = minY; y <= maxY; y++)
        {
            double dy = y - center.Y;
            double dxMaxSq = radius * radius - dy * dy;
            if (dxMaxSq >= 0)
            {
                double dxMax = Math.Sqrt(dxMaxSq);
                int startX = Math.Max(0, (int)Math.Round(center.X - dxMax));
                int endX = Math.Min(width - 1, (int)Math.Round(center.X + dxMax));
                if (startX <= endX)
                {
                    spans.Add(new RunLengthSpan(y, startX, endX));
                }
            }
        }
        return new Region(spans);
    }

    public static Region FromMask(Mat mask)
    {
        if (mask.Empty()) return Empty;

        using var gray = mask.Channels() > 1 ? mask.CvtColor(ColorConversionCodes.BGR2GRAY) : mask.Clone();
        var spans = new List<RunLengthSpan>();

        int w = gray.Width;
        int h = gray.Height;

        for (int y = 0; y < h; y++)
        {
            unsafe
            {
                byte* row = (byte*)gray.Ptr(y);
                int inSpanStart = -1;

                for (int x = 0; x < w; x++)
                {
                    if (row[x] > 127)
                    {
                        if (inSpanStart < 0) inSpanStart = x;
                    }
                    else
                    {
                        if (inSpanStart >= 0)
                        {
                            spans.Add(new RunLengthSpan(y, inSpanStart, x - 1));
                            inSpanStart = -1;
                        }
                    }
                }

                if (inSpanStart >= 0)
                {
                    spans.Add(new RunLengthSpan(y, inSpanStart, w - 1));
                }
            }
        }

        return new Region(spans);
    }

    #endregion

    #region Conversions

    public Mat ToMask(int width, int height)
    {
        var mask = new Mat(height, width, MatType.CV_8UC1, Scalar.All(0));
        foreach (var r in _runs)
        {
            if (r.Y >= 0 && r.Y < height)
            {
                int sx = Math.Clamp(r.StartX, 0, width - 1);
                int ex = Math.Clamp(r.EndX, 0, width - 1);
                if (sx <= ex)
                {
                    unsafe
                    {
                        byte* row = (byte*)mask.Ptr(r.Y);
                        for (int x = sx; x <= ex; x++)
                        {
                            row[x] = 255;
                        }
                    }
                }
            }
        }
        return mask;
    }

    #endregion

    #region Boolean Operations

    public Region Union(Region other)
    {
        if (IsEmpty) return other;
        if (other.IsEmpty) return this;

        // Group runs by Y coordinate and merge overlapping spans
        var combined = new List<RunLengthSpan>(_runs.Count + other._runs.Count);
        combined.AddRange(_runs);
        combined.AddRange(other._runs);
        return new Region(combined);
    }

    public Region Intersect(Region other)
    {
        if (IsEmpty || other.IsEmpty) return Empty;

        // Fast bounding box rejection check
        var b1 = BoundingBox;
        var b2 = other.BoundingBox;
        if (b1.X + b1.Width < b2.X || b2.X + b2.Width < b1.X ||
            b1.Y + b1.Height < b2.Y || b2.Y + b2.Height < b1.Y)
        {
            return Empty;
        }

        var result = new List<RunLengthSpan>();
        int i = 0, j = 0;

        while (i < _runs.Count && j < other._runs.Count)
        {
            var r1 = _runs[i];
            var r2 = other._runs[j];

            if (r1.Y < r2.Y)
            {
                i++;
            }
            else if (r1.Y > r2.Y)
            {
                j++;
            }
            else
            {
                // Same line, compute interval overlap
                int start = Math.Max(r1.StartX, r2.StartX);
                int end = Math.Min(r1.EndX, r2.EndX);
                if (start <= end)
                {
                    result.Add(new RunLengthSpan(r1.Y, start, end));
                }

                if (r1.EndX < r2.EndX)
                {
                    i++;
                }
                else
                {
                    j++;
                }
            }
        }

        return new Region(result);
    }

    public Region Difference(Region other)
    {
        if (IsEmpty) return Empty;
        if (other.IsEmpty) return this;

        var result = new List<RunLengthSpan>();
        var otherByY = other._runs.GroupBy(r => r.Y).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var r in _runs)
        {
            if (!otherByY.TryGetValue(r.Y, out var cuts))
            {
                result.Add(r);
                continue;
            }

            // Cut segment r with overlapping cuts
            var currentSegments = new List<(int Start, int End)> { (r.StartX, r.EndX) };

            foreach (var cut in cuts)
            {
                var nextSegments = new List<(int Start, int End)>();
                foreach (var seg in currentSegments)
                {
                    if (cut.EndX < seg.Start || cut.StartX > seg.End)
                    {
                        // No overlap
                        nextSegments.Add(seg);
                    }
                    else
                    {
                        // Left piece
                        if (cut.StartX > seg.Start)
                        {
                            nextSegments.Add((seg.Start, cut.StartX - 1));
                        }
                        // Right piece
                        if (cut.EndX < seg.End)
                        {
                            nextSegments.Add((cut.EndX + 1, seg.End));
                        }
                    }
                }
                currentSegments = nextSegments;
            }

            foreach (var seg in currentSegments)
            {
                if (seg.Start <= seg.End)
                {
                    result.Add(new RunLengthSpan(r.Y, seg.Start, seg.End));
                }
            }
        }

        return new Region(result);
    }

    public Region Xor(Region other)
    {
        if (IsEmpty) return other;
        if (other.IsEmpty) return this;

        var union = Union(other);
        var intersect = Intersect(other);
        return union.Difference(intersect);
    }

    #endregion

    #region Transformations and Morphology

    public Region Translate(int dx, int dy)
    {
        if (IsEmpty || (dx == 0 && dy == 0)) return this;

        var spans = new List<RunLengthSpan>(_runs.Count);
        foreach (var r in _runs)
        {
            spans.Add(new RunLengthSpan(r.Y + dy, r.StartX + dx, r.EndX + dx));
        }
        return new Region(spans);
    }

    public Region Dilate(int radius)
    {
        if (IsEmpty || radius <= 0) return this;

        var bounds = BoundingBox;
        int pad = radius + 2;
        int w = bounds.Width + pad * 2;
        int h = bounds.Height + pad * 2;

        var shifted = Translate(-bounds.X + pad, -bounds.Y + pad);
        using var mask = shifted.ToMask(w, h);
        using var element = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(radius * 2 + 1, radius * 2 + 1));
        using var dilated = new Mat();
        Cv2.Dilate(mask, dilated, element);

        var res = FromMask(dilated);
        return res.Translate(bounds.X - pad, bounds.Y - pad);
    }

    public Region Erode(int radius)
    {
        if (IsEmpty || radius <= 0) return this;

        var bounds = BoundingBox;
        int pad = radius + 2;
        int w = bounds.Width + pad * 2;
        int h = bounds.Height + pad * 2;

        var shifted = Translate(-bounds.X + pad, -bounds.Y + pad);
        using var mask = shifted.ToMask(w, h);
        using var element = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(radius * 2 + 1, radius * 2 + 1));
        using var eroded = new Mat();
        Cv2.Erode(mask, eroded, element);

        var res = FromMask(eroded);
        return res.Translate(bounds.X - pad, bounds.Y - pad);
    }

    #endregion

    private static List<RunLengthSpan> NormalizeSpans(IEnumerable<RunLengthSpan> spans)
    {
        var result = new List<RunLengthSpan>();
        var grouped = spans.Where(s => s.StartX <= s.EndX).GroupBy(s => s.Y).OrderBy(g => g.Key);

        foreach (var group in grouped)
        {
            var ordered = group.OrderBy(s => s.StartX).ToList();
            if (ordered.Count == 0) continue;

            int curStart = ordered[0].StartX;
            int curEnd = ordered[0].EndX;

            for (int i = 1; i < ordered.Count; i++)
            {
                var next = ordered[i];
                if (next.StartX <= curEnd + 1)
                {
                    curEnd = Math.Max(curEnd, next.EndX);
                }
                else
                {
                    result.Add(new RunLengthSpan(group.Key, curStart, curEnd));
                    curStart = next.StartX;
                    curEnd = next.EndX;
                }
            }

            result.Add(new RunLengthSpan(group.Key, curStart, curEnd));
        }

        return result;
    }
}
