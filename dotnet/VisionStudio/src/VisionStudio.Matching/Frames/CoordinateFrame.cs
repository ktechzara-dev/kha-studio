using OpenCvSharp;
using VisionStudio.Matching.Engine;

namespace VisionStudio.Matching.Frames;

/// <summary>
/// A 2D affine coordinate frame ("Space") anchored to a physical part or parent space.
/// </summary>
public sealed class CoordinateFrame
{
    public string Name { get; set; } = "Root";
    public string? ParentName { get; set; }

    public float OriginX { get; set; }
    public float OriginY { get; set; }
    public float AngleDeg { get; set; }
    public float Scale { get; set; } = 1.0f;

    public float AngleRad => AngleDeg * (MathF.PI / 180.0f);

    public CoordinateFrame() { }

    public CoordinateFrame(string name, string? parentName = null, float originX = 0f, float originY = 0f, float angleDeg = 0f, float scale = 1f)
    {
        Name = name;
        ParentName = parentName;
        OriginX = originX;
        OriginY = originY;
        AngleDeg = angleDeg;
        Scale = scale;
    }

    /// <summary>
    /// Transforms a point in this local frame to its immediate parent frame.
    /// </summary>
    public Point2f PointToParent(Point2f p)
    {
        float rad = AngleRad;
        float cosA = MathF.Cos(rad);
        float sinA = MathF.Sin(rad);

        float rx = (p.X * cosA - p.Y * sinA) * Scale + OriginX;
        float ry = (p.X * sinA + p.Y * cosA) * Scale + OriginY;
        return new Point2f(rx, ry);
    }

    /// <summary>
    /// Transforms a point from immediate parent frame into this local frame.
    /// </summary>
    public Point2f PointFromParent(Point2f p)
    {
        float dx = p.X - OriginX;
        float dy = p.Y - OriginY;
        float invScale = Scale != 0.0f ? 1.0f / Scale : 1.0f;

        float rad = -AngleRad;
        float cosA = MathF.Cos(rad);
        float sinA = MathF.Sin(rad);

        float lx = (dx * cosA - dy * sinA) * invScale;
        float ly = (dx * sinA + dy * cosA) * invScale;
        return new Point2f(lx, ly);
    }
}

/// <summary>
/// Node structure for visualizing nested coordinate frames in UI trees.
/// </summary>
public sealed class FrameTreeNode
{
    public CoordinateFrame Frame { get; }
    public string Name => Frame.Name;
    public string Description => $"{Frame.Name} [({Frame.OriginX:F1}, {Frame.OriginY:F1}), {Frame.AngleDeg:F1}°, {Frame.Scale:F2}x]";
    public List<FrameTreeNode> Children { get; } = new();

    public FrameTreeNode(CoordinateFrame frame)
    {
        Frame = frame;
    }
}

/// <summary>
/// Central manager for coordinate frames, spaces, and automated tool ROI fixturing.
/// </summary>
public sealed class CoordinateFrameManager
{
    private readonly Dictionary<string, CoordinateFrame> _frames = new(StringComparer.OrdinalIgnoreCase);

    public const string RootFrameName = "Root";

    public CoordinateFrameManager()
    {
        _frames[RootFrameName] = new CoordinateFrame(RootFrameName);
    }

    public CoordinateFrame GetFrame(string name)
    {
        if (_frames.TryGetValue(name, out var frame)) return frame;
        throw new KeyNotFoundException($"Coordinate frame '{name}' does not exist.");
    }

    public bool HasFrame(string name) => _frames.ContainsKey(name);

    public void SetFrame(CoordinateFrame frame)
    {
        _frames[frame.Name] = frame;
    }

    /// <summary>
    /// Anchors a new coordinate frame from a matched object's pose (Fixturing).
    /// </summary>
    public CoordinateFrame AnchorFromMatch(string frameName, MatchPose pose, string parentFrame = RootFrameName)
    {
        var frame = new CoordinateFrame(frameName, parentFrame, pose.X, pose.Y, pose.AngleDeg, pose.Scale);
        _frames[frameName] = frame;
        return frame;
    }

    /// <summary>
    /// Transforms a 2D point from one coordinate frame to another.
    /// </summary>
    public Point2f TransformPoint(Point2f point, string fromFrameName, string toFrameName)
    {
        if (string.Equals(fromFrameName, toFrameName, StringComparison.OrdinalIgnoreCase))
            return point;

        // Convert point to root
        var current = point;
        var currFrameName = fromFrameName;

        while (!string.Equals(currFrameName, RootFrameName, StringComparison.OrdinalIgnoreCase))
        {
            var f = GetFrame(currFrameName);
            current = f.PointToParent(current);
            currFrameName = f.ParentName ?? RootFrameName;
        }

        // Convert point from root down to destination
        var pathToTarget = new List<CoordinateFrame>();
        var targetCurr = toFrameName;
        while (!string.Equals(targetCurr, RootFrameName, StringComparison.OrdinalIgnoreCase))
        {
            var f = GetFrame(targetCurr);
            pathToTarget.Add(f);
            targetCurr = f.ParentName ?? RootFrameName;
        }

        pathToTarget.Reverse();
        foreach (var f in pathToTarget)
        {
            current = f.PointFromParent(current);
        }

        return current;
    }

    /// <summary>
    /// Transforms a line segment from one frame to another.
    /// </summary>
    public (Point2f P1, Point2f P2) TransformLine(Point2f p1, Point2f p2, string fromFrame, string toFrame)
    {
        return (TransformPoint(p1, fromFrame, toFrame), TransformPoint(p2, fromFrame, toFrame));
    }

    /// <summary>
    /// Transforms a polygon from one frame to another.
    /// </summary>
    public IReadOnlyList<Point2f> TransformPolygon(IEnumerable<Point2f> points, string fromFrame, string toFrame)
    {
        return points.Select(p => TransformPoint(p, fromFrame, toFrame)).ToList();
    }

    /// <summary>
    /// Transforms an ROI box (specified in nominal/fixture space) to actual image coordinates.
    /// </summary>
    public RotatedRect TransformRoiToImage(float localX, float localY, float width, float height, string fixtureFrame)
    {
        var localCenter = new Point2f(localX + width / 2.0f, localY + height / 2.0f);
        var imgCenter = TransformPoint(localCenter, fixtureFrame, RootFrameName);

        var frame = GetFrame(fixtureFrame);
        return new RotatedRect(imgCenter, new Size2f(width * frame.Scale, height * frame.Scale), frame.AngleDeg);
    }

    /// <summary>
    /// Builds a hierarchical frame tree for UI visualization.
    /// </summary>
    public FrameTreeNode GetFrameTree()
    {
        var root = new FrameTreeNode(GetFrame(RootFrameName));
        var nodeMap = new Dictionary<string, FrameTreeNode>(StringComparer.OrdinalIgnoreCase)
        {
            [RootFrameName] = root
        };

        foreach (var (name, frame) in _frames)
        {
            if (!string.Equals(name, RootFrameName, StringComparison.OrdinalIgnoreCase))
            {
                nodeMap[name] = new FrameTreeNode(frame);
            }
        }

        foreach (var (name, node) in nodeMap)
        {
            if (string.Equals(name, RootFrameName, StringComparison.OrdinalIgnoreCase)) continue;

            var parentName = node.Frame.ParentName ?? RootFrameName;
            if (nodeMap.TryGetValue(parentName, out var parentNode))
            {
                parentNode.Children.Add(node);
            }
            else
            {
                root.Children.Add(node);
            }
        }

        return root;
    }
}
