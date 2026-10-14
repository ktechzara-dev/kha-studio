using OpenCvSharp;

namespace VisionStudio.Matching.Engine;

/// <summary>
/// Sub-pixel and sub-degree 2D affine pose of a matched object.
/// </summary>
public readonly record struct MatchPose(
    float X,
    float Y,
    float AngleDeg,
    float Scale)
{
    public float AngleRad => AngleDeg * (MathF.PI / 180.0f);
}

/// <summary>
/// A single located instance of a geometric model in the target scene.
/// </summary>
public sealed class MatchInstance
{
    public MatchPose Pose { get; }
    public float Score { get; }
    public bool ContrastReversed { get; }
    public IReadOnlyList<Point2f> TransformedContour { get; }
    public Point2f[] BoundingCorners { get; }

    public float X => Pose.X;
    public float Y => Pose.Y;
    public float AngleDeg => Pose.AngleDeg;
    public float Scale => Pose.Scale;

    public MatchInstance(
        MatchPose pose,
        float score,
        bool contrastReversed,
        IReadOnlyList<Point2f> transformedContour,
        Point2f[] boundingCorners)
    {
        Pose = pose;
        Score = score;
        ContrastReversed = contrastReversed;
        TransformedContour = transformedContour;
        BoundingCorners = boundingCorners;
    }
}

/// <summary>
/// Complete inspection match report returned by the GeometricMatcher.
/// </summary>
public sealed class MatchReport
{
    public IReadOnlyList<MatchInstance> Instances { get; }
    public int MatchCount => Instances.Count;
    public MatchInstance? BestMatch => Instances.Count > 0 ? Instances[0] : null;
    public double ExecutionTimeMs { get; }
    public bool Success => Instances.Count > 0;

    public MatchReport(IEnumerable<MatchInstance> instances, double executionTimeMs)
    {
        Instances = instances.OrderByDescending(i => i.Score).ToList();
        ExecutionTimeMs = executionTimeMs;
    }

    public static MatchReport Empty(double executionTimeMs = 0.0) =>
        new(Array.Empty<MatchInstance>(), executionTimeMs);
}
