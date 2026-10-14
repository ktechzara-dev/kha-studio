using OpenCvSharp;

namespace VisionStudio.Core.Types;

/// <summary>
/// A 2D point with double precision.
/// </summary>
public readonly record struct PathPoint(double X, double Y)
{
    public double DistanceTo(PathPoint other)
    {
        double dx = X - other.X;
        double dy = Y - other.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}

/// <summary>
/// First-class 2D Path type representing inspection trajectories, measurement contours,
/// and geometric profiles with length, resampling, and distance query capabilities.
/// </summary>
public sealed class Path2D
{
    private readonly List<PathPoint> _points;
    private double? _cachedLength;
    private Rect? _cachedBounds;

    public IReadOnlyList<PathPoint> Points => _points;
    public bool IsClosed { get; }

    public Path2D(IEnumerable<PathPoint> points, bool isClosed = false)
    {
        _points = points.ToList();
        IsClosed = isClosed;
    }

    public int PointCount => _points.Count;

    public double Length
    {
        get
        {
            if (!_cachedLength.HasValue)
            {
                if (_points.Count < 2)
                {
                    _cachedLength = 0;
                }
                else
                {
                    double sum = 0;
                    for (int i = 0; i < _points.Count - 1; i++)
                    {
                        sum += _points[i].DistanceTo(_points[i + 1]);
                    }
                    if (IsClosed && _points.Count > 2)
                    {
                        sum += _points[^1].DistanceTo(_points[0]);
                    }
                    _cachedLength = sum;
                }
            }
            return _cachedLength.Value;
        }
    }

    public Rect BoundingBox
    {
        get
        {
            if (!_cachedBounds.HasValue)
            {
                if (_points.Count == 0)
                {
                    _cachedBounds = new Rect(0, 0, 0, 0);
                }
                else
                {
                    double minX = _points.Min(p => p.X);
                    double maxX = _points.Max(p => p.X);
                    double minY = _points.Min(p => p.Y);
                    double maxY = _points.Max(p => p.Y);

                    _cachedBounds = new Rect(
                        (int)Math.Floor(minX),
                        (int)Math.Floor(minY),
                        Math.Max(0, (int)Math.Ceiling(maxX - minX)),
                        Math.Max(0, (int)Math.Ceiling(maxY - minY)));
                }
            }
            return _cachedBounds.Value;
        }
    }

    #region Factory Methods

    public static Path2D FromLine(double x1, double y1, double x2, double y2)
    {
        return new Path2D(new[] { new PathPoint(x1, y1), new PathPoint(x2, y2) }, isClosed: false);
    }

    public static Path2D FromContour(IEnumerable<Point> contour, bool isClosed = true)
    {
        var pts = contour.Select(p => new PathPoint(p.X, p.Y));
        return new Path2D(pts, isClosed);
    }

    public static Path2D FromPoints(IEnumerable<(double X, double Y)> points, bool isClosed = false)
    {
        return new Path2D(points.Select(p => new PathPoint(p.X, p.Y)), isClosed);
    }

    #endregion

    #region Geometry Analysis

    /// <summary>
    /// Computes minimum Euclidean distance from query point to this path.
    /// </summary>
    public double DistanceTo(PathPoint query)
    {
        if (_points.Count == 0) return double.PositiveInfinity;
        if (_points.Count == 1) return _points[0].DistanceTo(query);

        double minDistance = double.MaxValue;
        int count = IsClosed ? _points.Count : _points.Count - 1;

        for (int i = 0; i < count; i++)
        {
            var p1 = _points[i];
            var p2 = _points[(i + 1) % _points.Count];

            double dist = DistancePointToSegment(query, p1, p2);
            if (dist < minDistance) minDistance = dist;
        }

        return minDistance;
    }

    /// <summary>
    /// Uniformly resamples the path with a given arc-length step.
    /// </summary>
    public Path2D SamplePoints(double step)
    {
        if (step <= 0 || _points.Count < 2 || Length <= 0) return this;

        var res = new List<PathPoint> { _points[0] };
        double accumulated = 0;
        double targetDist = step;

        int segCount = IsClosed ? _points.Count : _points.Count - 1;

        for (int i = 0; i < segCount; i++)
        {
            var p1 = _points[i];
            var p2 = _points[(i + 1) % _points.Count];
            double segLen = p1.DistanceTo(p2);

            while (accumulated + segLen >= targetDist)
            {
                double remain = targetDist - accumulated;
                double t = segLen > 0 ? remain / segLen : 0;
                double nx = p1.X + t * (p2.X - p1.X);
                double ny = p1.Y + t * (p2.Y - p1.Y);

                res.Add(new PathPoint(nx, ny));
                targetDist += step;
            }

            accumulated += segLen;
        }

        if (IsClosed)
        {
            return new Path2D(res, isClosed: true);
        }
        else
        {
            if (res.Last().DistanceTo(_points.Last()) > step * 0.2)
            {
                res.Add(_points.Last());
            }
            return new Path2D(res, isClosed: false);
        }
    }

    private static double DistancePointToSegment(PathPoint p, PathPoint a, PathPoint b)
    {
        double abx = b.X - a.X;
        double aby = b.Y - a.Y;
        double apx = p.X - a.X;
        double apy = p.Y - a.Y;

        double abLenSq = abx * abx + aby * aby;
        if (abLenSq <= 1e-9)
        {
            return p.DistanceTo(a);
        }

        double t = Math.Clamp((apx * abx + apy * aby) / abLenSq, 0.0, 1.0);
        double nearestX = a.X + t * abx;
        double nearestY = a.Y + t * aby;

        return p.DistanceTo(new PathPoint(nearestX, nearestY));
    }

    #endregion
}
