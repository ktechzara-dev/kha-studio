using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Overlays;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Edges;

/// <summary>
/// Extracts vector boundaries from binary images with hierarchy topology, polygon approximation, and convex hull generation.
/// </summary>
[VisionTool("Edges.FindContours", "Find Contours", "Edges and Contours", Description = "Extracts vector contours, convex hull, and polygonal approximations")]
public sealed class FindContoursTool : VisionToolBase
{
    public const string ParamRetrieval = "RetrievalMode";
    public const string ParamPolygonEpsilon = "ApproxEpsilon";
    public const string ParamConvexHull = "ComputeConvexHull";

    public const string PortInImage = "ImageIn";
    public const string PortContourCount = "ContourCount";
    public const string PortAnnotatedImage = "AnnotatedImage";

    private readonly ToolParameter _pRetrieval;
    private readonly ToolParameter _pEps;
    private readonly ToolParameter _pHull;

    public FindContoursTool() : base("Edges.FindContours", "Find Contours", "Edges and Contours", "Extracts vector contours")
    {
        _pRetrieval = AddParameter(new ToolParameter(
            ParamRetrieval, "Retrieval Mode", ParameterType.Enum, "External",
            enumOptions: new[] { "External", "List", "Tree" }));

        _pEps = AddParameter(new ToolParameter(ParamPolygonEpsilon, "Polygon Approx Epsilon (px)", ParameterType.Double, 2.0, min: 0.0, max: 100.0));
        _pHull = AddParameter(new ToolParameter(ParamConvexHull, "Compute Convex Hull", ParameterType.Boolean, true));

        AddInput(new ToolPort(PortInImage, "Image In", PortType.Image, PortDirection.Input));
        AddOutput(new ToolPort(PortContourCount, "Contour Count", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortAnnotatedImage, "Annotated Image", PortType.Image, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var inputFrame = ctx.GetImage(PortInImage);
        if (inputFrame == null) return ToolResult.Failed("Missing input image.");

        var src = inputFrame.Mat;
        using var binary = new Mat();
        if (src.Channels() > 1)
        {
            Cv2.CvtColor(src, binary, ColorConversionCodes.BGR2GRAY);
            Cv2.Threshold(binary, binary, 127, 255, ThresholdTypes.Binary);
        }
        else
        {
            Cv2.Threshold(src, binary, 127, 255, ThresholdTypes.Binary);
        }

        var rMode = (_pRetrieval.GetValue<string>() ?? "External").ToLowerInvariant() switch
        {
            "tree" => RetrievalModes.Tree,
            "list" => RetrievalModes.List,
            _ => RetrievalModes.External
        };

        Cv2.FindContours(binary, out var contours, out _, rMode, ContourApproximationModes.ApproxNone);

        var eps = _pEps.GetValue<double>();
        var doHull = _pHull.GetValue<bool>();

        var annotated = new Mat();
        if (src.Channels() == 1)
        {
            Cv2.CvtColor(src, annotated, ColorConversionCodes.GRAY2BGR);
        }
        else
        {
            src.CopyTo(annotated);
        }

        var overlays = new List<IOverlayPrimitive>();

        for (int i = 0; i < contours.Length; i++)
        {
            var c = contours[i];
            if (c.Length < 3) continue;

            // Approximate polygon
            var approx = (eps > 0) ? Cv2.ApproxPolyDP(c, eps, closed: true) : c;
            var polyPoints = approx.Select(p => new Point2f(p.X, p.Y)).ToList();
            overlays.Add(new PolygonOverlay(polyPoints, new Scalar(0, 255, 0), Thickness: 2));

            if (doHull)
            {
                var hull = Cv2.ConvexHull(c);
                var hullPoints = hull.Select(p => new Point2f(p.X, p.Y)).ToList();
                overlays.Add(new PolygonOverlay(hullPoints, new Scalar(255, 0, 255), Thickness: 1));
            }
        }

        foreach (var ov in overlays) ov.Render(annotated);

        var outFrame = new ImageFrame(annotated, inputFrame.CameraId, inputFrame.FrameId, PixelFormat.Bgr24);
        var outputs = new Dictionary<string, object?>
        {
            [PortContourCount] = contours.Length,
            [PortAnnotatedImage] = outFrame
        };

        return ToolResult.Ok(outputs, overlays, message: $"Extracted {contours.Length} contours");
    }
}
