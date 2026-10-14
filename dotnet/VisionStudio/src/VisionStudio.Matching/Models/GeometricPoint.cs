namespace VisionStudio.Matching.Models;

/// <summary>
/// Represents an oriented feature edge point extracted from gradient contours.
/// </summary>
public readonly record struct GeometricPoint(
    float X,
    float Y,
    float DirectionX,
    float DirectionY,
    float Magnitude = 1.0f,
    float Weight = 1.0f)
{
    /// <summary>
    /// Gets the orientation angle in radians [-π, π].
    /// </summary>
    public float AngleRad => MathF.Atan2(DirectionY, DirectionX);

    /// <summary>
    /// Gets the orientation angle in degrees [-180°, 180°].
    /// </summary>
    public float AngleDeg => AngleRad * (180.0f / MathF.PI);

    /// <summary>
    /// Transforms this geometric point by a rotation angle in radians, scale, and translation.
    /// </summary>
    public GeometricPoint Transform(float cosAngle, float sinAngle, float scale, float tx, float ty)
    {
        var rx = (X * cosAngle - Y * sinAngle) * scale + tx;
        var ry = (X * sinAngle + Y * cosAngle) * scale + ty;
        var rdx = DirectionX * cosAngle - DirectionY * sinAngle;
        var rdy = DirectionX * sinAngle + DirectionY * cosAngle;
        return new GeometricPoint(rx, ry, rdx, rdy, Magnitude, Weight);
    }
}
