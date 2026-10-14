using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Overlays;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Matching;

/// <summary>
/// Compares topological contour shape similarity using invariant Hu moments (Cv2.MatchShapes).
/// Score = 0 indicates an identical shape.
/// </summary>
[VisionTool("Matching.ShapeMatch", "Shape-Based Match", "Matching and Localization", Description = "Scale and rotation invariant contour shape matching via Hu moments")]
public sealed class ShapeMatchTool : VisionToolBase
{
    public const string ParamMaxDistance = "MaxShapeDistance";

    public const string PortInImage = "ImageIn";
    public const string PortTemplate = "TemplateIn";
    public const string PortDisimilarity = "ShapeDistance";
    public const string PortIsMatch = "IsMatch";
    public const string PortAnnotatedImage = "AnnotatedImage";

    private readonly ToolParameter _pMaxDist;

    public ShapeMatchTool() : base("Matching.ShapeMatch", "Shape-Based Match", "Matching and Localization", "Hu moment shape similarity")
    {
        _pMaxDist = AddParameter(new ToolParameter(ParamMaxDistance, "Max Disimilarity Distance", ParameterType.Double, 0.15, min: 0.01, max: 2.0));

        AddInput(new ToolPort(PortInImage, "Image In", PortType.Image, PortDirection.Input));
        AddInput(new ToolPort(PortTemplate, "Template In", PortType.Image, PortDirection.Input));

        AddOutput(new ToolPort(PortDisimilarity, "Shape Distance", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortIsMatch, "Is Match", PortType.Boolean, PortDirection.Output));
        AddOutput(new ToolPort(PortAnnotatedImage, "Annotated Image", PortType.Image, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var inputFrame = ctx.GetImage(PortInImage);
        var tplFrame = ctx.GetImage(PortTemplate);

        if (inputFrame == null) return ToolResult.Failed("Missing input image.");
        if (tplFrame == null) return ToolResult.Failed("Missing template image.");

        using var b1 = ToBinary(inputFrame.Mat);
        using var b2 = ToBinary(tplFrame.Mat);

        Cv2.FindContours(b1, out var c1, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
        Cv2.FindContours(b2, out var c2, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        if (c1.Length == 0 || c2.Length == 0)
        {
            return ToolResult.Warning("Failed to extract contours for shape matching.");
        }

        var targetContour = c1.OrderByDescending(c => Cv2.ContourArea(c)).First();
        var templateContour = c2.OrderByDescending(c => Cv2.ContourArea(c)).First();

        var dist = Cv2.MatchShapes(targetContour, templateContour, ShapeMatchModes.I1, 0.0);
        var maxAllowed = _pMaxDist.GetValue<double>();
        var isMatch = dist <= maxAllowed;

        var annotated = inputFrame.Mat.Clone();
        var overlays = new List<IOverlayPrimitive>();

        var polyPts = targetContour.Select(p => new Point2f(p.X, p.Y)).ToList();
        var polyOv = new PolygonOverlay(polyPts, isMatch ? new Scalar(0, 255, 0) : new Scalar(0, 0, 255), Thickness: 2);
        var textOv = new TextOverlay($"Shape Diff: {dist:F3}", new Point2f(polyPts[0].X, polyPts[0].Y - 8), isMatch ? new Scalar(0, 255, 0) : new Scalar(0, 0, 255), FontScale: 0.5);

        overlays.Add(polyOv);
        overlays.Add(textOv);
        polyOv.Render(annotated);
        textOv.Render(annotated);

        var outFrame = new ImageFrame(annotated, inputFrame.CameraId, inputFrame.FrameId, inputFrame.Format);
        var outputs = new Dictionary<string, object?>
        {
            [PortDisimilarity] = dist,
            [PortIsMatch] = isMatch,
            [PortAnnotatedImage] = outFrame
        };

        return ToolResult.Ok(outputs, overlays, message: $"Shape match distance: {dist:F4} (Thresh ≤ {maxAllowed:F3})");
    }

    private static Mat ToBinary(Mat src)
    {
        var dst = new Mat();
        if (src.Channels() > 1) Cv2.CvtColor(src, dst, ColorConversionCodes.BGR2GRAY);
        else src.CopyTo(dst);
        Cv2.Threshold(dst, dst, 127, 255, ThresholdTypes.Binary);
        return dst;
    }
}
