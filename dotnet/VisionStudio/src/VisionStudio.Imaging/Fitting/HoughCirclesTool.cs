using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Overlays;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Fitting;

/// <summary>
/// Detects circular features in noisy images via gradient Hough circle transform.
/// </summary>
[VisionTool("Fitting.HoughCircles", "Hough Circles", "Geometry Fitting", Description = "Detects circular features via Hough circle transform")]
public sealed class HoughCirclesTool : VisionToolBase
{
    public const string ParamMinDist = "MinDist";
    public const string ParamParam1 = "CannyHighThreshold";
    public const string ParamParam2 = "AccumulatorThreshold";
    public const string ParamMinR = "MinRadius";
    public const string ParamMaxR = "MaxRadius";

    public const string PortInImage = "ImageIn";
    public const string PortCircleCount = "CircleCount";
    public const string PortPrimaryRadius = "PrimaryRadius";
    public const string PortPrimaryCenterX = "PrimaryCenterX";
    public const string PortPrimaryCenterY = "PrimaryCenterY";
    public const string PortAnnotatedImage = "AnnotatedImage";

    private readonly ToolParameter _pMinDist;
    private readonly ToolParameter _pParam1;
    private readonly ToolParameter _pParam2;
    private readonly ToolParameter _pMinR;
    private readonly ToolParameter _pMaxR;

    public HoughCirclesTool() : base("Fitting.HoughCircles", "Hough Circles", "Geometry Fitting", "Hough circle transform")
    {
        _pMinDist = AddParameter(new ToolParameter(ParamMinDist, "Min Distance Between Centers", ParameterType.Double, 30.0, min: 5.0, max: 2000.0));
        _pParam1 = AddParameter(new ToolParameter(ParamParam1, "Canny High Threshold", ParameterType.Double, 100.0, min: 10.0, max: 500.0));
        _pParam2 = AddParameter(new ToolParameter(ParamParam2, "Accumulator Center Threshold", ParameterType.Double, 30.0, min: 5.0, max: 200.0));
        _pMinR = AddParameter(new ToolParameter(ParamMinR, "Min Radius (px)", ParameterType.Integer, 10, min: 0, max: 2000));
        _pMaxR = AddParameter(new ToolParameter(ParamMaxR, "Max Radius (px)", ParameterType.Integer, 150, min: 0, max: 4000));

        AddInput(new ToolPort(PortInImage, "Image In", PortType.Image, PortDirection.Input));
        AddOutput(new ToolPort(PortCircleCount, "Circle Count", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortPrimaryRadius, "Primary Radius", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortPrimaryCenterX, "Primary Center X", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortPrimaryCenterY, "Primary Center Y", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortAnnotatedImage, "Annotated Image", PortType.Image, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var inputFrame = ctx.GetImage(PortInImage);
        if (inputFrame == null) return ToolResult.Failed("Missing input image.");

        var src = inputFrame.Mat;
        using var gray = new Mat();
        if (src.Channels() > 1)
        {
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
        }
        else
        {
            src.CopyTo(gray);
        }

        // Slight Gaussian blur to reduce false circles
        using var blurred = new Mat();
        Cv2.GaussianBlur(gray, blurred, new Size(9, 9), 2);

        var minDist = _pMinDist.GetValue<double>();
        var p1 = _pParam1.GetValue<double>();
        var p2 = _pParam2.GetValue<double>();
        var minR = _pMinR.GetValue<int>();
        var maxR = _pMaxR.GetValue<int>();

        var circles = Cv2.HoughCircles(blurred, HoughModes.Gradient, 1.2, minDist, p1, p2, minR, maxR);

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
        double primaryR = 0, primaryX = 0, primaryY = 0;

        if (circles != null && circles.Length > 0)
        {
            var dominant = circles.OrderByDescending(c => c.Radius).First();
            primaryR = dominant.Radius;
            primaryX = dominant.Center.X;
            primaryY = dominant.Center.Y;

            foreach (var c in circles)
            {
                var circleOv = new CircleOverlay(new Point2f(c.Center.X, c.Center.Y), c.Radius, new Scalar(0, 255, 0), Thickness: 2);
                var crossOv = new CrossOverlay(new Point2f(c.Center.X, c.Center.Y), 10f, new Scalar(0, 0, 255), Thickness: 2);

                overlays.Add(circleOv);
                overlays.Add(crossOv);

                circleOv.Render(annotated);
                crossOv.Render(annotated);
            }
        }

        var count = circles?.Length ?? 0;
        var outFrame = new ImageFrame(annotated, inputFrame.CameraId, inputFrame.FrameId, PixelFormat.Bgr24);

        var outputs = new Dictionary<string, object?>
        {
            [PortCircleCount] = count,
            [PortPrimaryRadius] = primaryR,
            [PortPrimaryCenterX] = primaryX,
            [PortPrimaryCenterY] = primaryY,
            [PortAnnotatedImage] = outFrame
        };

        return ToolResult.Ok(outputs, overlays, message: $"Detected {count} Hough circles (Dominant R={primaryR:F1})");
    }
}
