using OpenCvSharp;

namespace VisionStudio.Imaging.Blob;

/// <summary>
/// Geometric and morphological measurements computed for an isolated connected region (blob).
/// </summary>
public sealed record BlobFeature(
    int Id,
    double Area,
    double Perimeter,
    Point2f Centroid,
    Rect2f BoundingBox,
    RotatedRect RotatedBox,
    double Circularity,
    double AspectRatio,
    double OrientationDegrees);
