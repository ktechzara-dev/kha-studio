using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Overlays;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Fitting;

/// <summary>
/// Fits analytic geometric primitives (Line, Circle, Ellipse, Rotated Rect) to extracted contours.
/// </summary>
[VisionTool("Fitting.GeometryFit", "Geometry Fitting", "Geometry Fitting", Description = "Fits Line, Circle, Ellipse, or Rotated Rect to feature contours")]
public sealed class GeometryFitTool : VisionToolBase
{
    public const string ParamGeometry = "TargetGeometry";

    public const string PortInImage = "ImageIn";
    public const string PortCenterX = "CenterX";
    public const string PortCenterY = "CenterY";
    public const string PortDimension = "RadiusOrDimension";
    public const string PortAngle = "Angle";
    public const string PortAnnotatedImage = "AnnotatedImage";

    private readonly ToolParameter _pGeom;

    public GeometryFitTool() : base("Fitting.GeometryFit", "Geometry Fitting", "Geometry Fitting", "Fits analytic geometry models")
    {
        _pGeom = AddParameter(new ToolParameter(
            ParamGeometry, "Geometry Type", ParameterType.Enum, "Circle",
            enumOptions: new[] { "Circle", "Line", "Ellipse", "RotatedRect" }));

        AddInput(new ToolPort(PortInImage, "Image In", PortType.Image, PortDirection.Input));

        AddOutput(new ToolPort(PortCenterX, "Center X", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortCenterY, "Center Y", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortDimension, "Dimension / Radius", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortAngle, "Angle (deg)", PortType.Number, PortDirection.Output));
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

        Cv2.FindContours(binary, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxNone);
        if (contours.Length == 0) return ToolResult.Warning("No contours found to fit geometry.");

        // Pick largest contour
        var largest = contours.OrderByDescending(c => Cv2.ContourArea(c)).First();
        var geomType = _pGeom.GetValue<string>() ?? "Circle";

        double cx = 0, cy = 0, dim = 0, angle = 0;
        var overlays = new List<IOverlayPrimitive>();

        var annotated = new Mat();
        if (src.Channels() == 1)
        {
            Cv2.CvtColor(src, annotated, ColorConversionCodes.GRAY2BGR);
        }
        else
        {
            src.CopyTo(annotated);
        }

        switch (geomType.ToLowerInvariant())
        {
            case "line":
                if (largest.Length >= 2)
                {
                    var line = Cv2.FitLine(largest, DistanceTypes.L2, 0, 0.01, 0.01);
                    cx = line.X1;
                    cy = line.Y1;
                    angle = Math.Atan2(line.Vy, line.Vx) * 180.0 / Math.PI;
                    dim = largest.Length;

                    var p1 = new Point2f((float)(cx - line.Vx * 500), (float)(cy - line.Vy * 500));
                    var p2 = new Point2f((float)(cx + line.Vx * 500), (float)(cy + line.Vy * 500));
                    overlays.Add(new LineOverlay(p1, p2, new Scalar(0, 255, 255), Thickness: 2));
                }
                break;

            case "ellipse":
                if (largest.Length >= 5)
                {
                    var ellipse = Cv2.FitEllipse(largest);
                    cx = ellipse.Center.X;
                    cy = ellipse.Center.Y;
                    dim = Math.Max(ellipse.Size.Width, ellipse.Size.Height);
                    angle = ellipse.Angle;
                    overlays.Add(new RotatedRectOverlay(ellipse, new Scalar(255, 0, 255), Thickness: 2));
                }
                break;

            case "rotatedrect":
                var rr = Cv2.MinAreaRect(largest);
                cx = rr.Center.X;
                cy = rr.Center.Y;
                dim = Math.Max(rr.Size.Width, rr.Size.Height);
                angle = rr.Angle;
                overlays.Add(new RotatedRectOverlay(rr, new Scalar(0, 255, 0), Thickness: 2));
                break;

            default: // Circle
                Cv2.MinEnclosingCircle(largest, out var center, out var radius);
                cx = center.X;
                cy = center.Y;
                dim = radius;
                angle = 0;
                overlays.Add(new CircleOverlay(center, radius, new Scalar(0, 255, 0), Thickness: 2));
                break;
        }

        overlays.Add(new CrossOverlay(new Point2f((float)cx, (float)cy), 10f, new Scalar(0, 0, 255), Thickness: 2));
        foreach (var ov in overlays) ov.Render(annotated);

        var outFrame = new ImageFrame(annotated, inputFrame.CameraId, inputFrame.FrameId, PixelFormat.Bgr24);
        var outputs = new Dictionary<string, object?>
        {
            [PortCenterX] = cx,
            [PortCenterY] = cy,
            [PortDimension] = dim,
            [PortAngle] = angle,
            [PortAnnotatedImage] = outFrame
        };

        return ToolResult.Ok(outputs, overlays, message: $"Fit {geomType}: Center=({cx:F1}, {cy:F1}), Dim={dim:F1}");
    }
}
