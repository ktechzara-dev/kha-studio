using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Overlays;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Geometry;

/// <summary>
/// Defines a freeform multi-point polygon Region of Interest and generates a binary mask and masked image.
/// </summary>
[VisionTool("Geometry.PolygonRoi", "Polygon ROI", "Geometry / ROI", Description = "Defines a freeform multi-point polygon ROI and generates a binary mask")]
public sealed class PolygonRoiTool : VisionToolBase
{
    public const string ParamPoints = "Points";
    public const string ParamInvert = "InvertMask";

    public const string PortInImage = "ImageIn";
    public const string PortMask = "Mask";
    public const string PortMaskedImage = "MaskedImage";
    public const string PortArea = "Area";
    public const string PortPerimeter = "Perimeter";

    private readonly ToolParameter _pPoints;
    private readonly ToolParameter _pInvert;

    public PolygonRoiTool() : base("Geometry.PolygonRoi", "Polygon ROI", "Geometry / ROI", "Freeform polygon region of interest")
    {
        _pPoints = AddParameter(new ToolParameter(
            ParamPoints, "Polygon Points", ParameterType.String,
            "100,80; 220,80; 260,180; 140,220; 60,150",
            tooltip: "List of x,y vertices separated by semicolons or commas"));

        _pInvert = AddParameter(new ToolParameter(
            ParamInvert, "Invert Mask", ParameterType.Boolean, false));

        AddInput(new ToolPort(PortInImage, "Image In", PortType.Image, PortDirection.Input));
        AddOutput(new ToolPort(PortMask, "Mask", PortType.Image, PortDirection.Output));
        AddOutput(new ToolPort(PortMaskedImage, "Masked Image", PortType.Image, PortDirection.Output));
        AddOutput(new ToolPort(PortArea, "Area (px²)", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortPerimeter, "Perimeter (px)", PortType.Number, PortDirection.Output));
    }

    public static List<Point> ParsePoints(string? raw, int defaultW = 640, int defaultH = 480)
    {
        var pts = new List<Point>();
        if (string.IsNullOrWhiteSpace(raw)) return pts;

        var tokens = raw.Split(new[] { ';', '\n', '\r', '|' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var tok in tokens)
        {
            var parts = tok.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 &&
                double.TryParse(parts[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var px) &&
                double.TryParse(parts[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var py))
            {
                pts.Add(new Point((int)Math.Round(px), (int)Math.Round(py)));
            }
        }

        // If only comma separated without semicolons: e.g. "x1,y1,x2,y2,..."
        if (pts.Count < 3)
        {
            var flat = raw.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries);
            if (flat.Length >= 6 && flat.Length % 2 == 0)
            {
                pts.Clear();
                for (int i = 0; i < flat.Length; i += 2)
                {
                    if (double.TryParse(flat[i], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var fx) &&
                        double.TryParse(flat[i + 1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var fy))
                    {
                        pts.Add(new Point((int)Math.Round(fx), (int)Math.Round(fy)));
                    }
                }
            }
        }

        return pts;
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var inputFrame = ctx.GetImage(PortInImage);
        if (inputFrame == null) return ToolResult.Failed("Missing input image.");

        var src = inputFrame.Mat;
        var rawPts = _pPoints.GetValue<string>();
        var pts = ParsePoints(rawPts, src.Cols, src.Rows);

        if (pts.Count < 3)
        {
            // Default 4-vertex diamond/box if invalid
            int cx = src.Cols / 2, cy = src.Rows / 2;
            pts = new List<Point>
            {
                new(cx - 80, cy - 60),
                new(cx + 80, cy - 60),
                new(cx + 100, cy + 60),
                new(cx - 100, cy + 60)
            };
        }

        var mask = new Mat(src.Rows, src.Cols, MatType.CV_8UC1, new Scalar(0));
        Cv2.FillPoly(mask, new[] { pts.ToArray() }, new Scalar(255));

        if (_pInvert.GetValue<bool>())
        {
            Cv2.BitwiseNot(mask, mask);
        }

        var masked = new Mat();
        src.CopyTo(masked, mask);

        // Compute polygon area and perimeter
        double area = Cv2.ContourArea(pts);
        double perimeter = Cv2.ArcLength(pts, closed: true);

        var overlays = new List<IOverlayPrimitive>
        {
            new PolygonOverlay(pts.Select(p => new Point2f(p.X, p.Y)).ToList(), new Scalar(255, 0, 255), Thickness: 2)
        };

        foreach (var p in pts)
        {
            overlays.Add(new CircleOverlay(new Point2f(p.X, p.Y), 4, new Scalar(0, 255, 255), Filled: true));
        }

        var outMask = new ImageFrame(mask, inputFrame.CameraId, inputFrame.FrameId, PixelFormat.Mono8);
        var outMasked = new ImageFrame(masked, inputFrame.CameraId, inputFrame.FrameId, inputFrame.Format);

        var outputs = new Dictionary<string, object?>
        {
            [PortMask] = outMask,
            [PortMaskedImage] = outMasked,
            [PortArea] = area,
            [PortPerimeter] = perimeter
        };

        return ToolResult.Ok(outputs, overlays, message: $"Polygon ROI: {pts.Count} vertices, Area={area:F0} px²");
    }
}
