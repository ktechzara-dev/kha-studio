using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Overlays;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Fitting;

/// <summary>
/// Detects straight line segments via Progressive Probabilistic Hough Transform (HoughLinesP).
/// </summary>
[VisionTool("Fitting.HoughLines", "Hough Lines", "Geometry Fitting", Description = "Detects straight line segments using probabilistic Hough transform")]
public sealed class HoughLinesTool : VisionToolBase
{
    public const string ParamThreshold = "Threshold";
    public const string ParamMinLength = "MinLineLength";
    public const string ParamMaxGap = "MaxLineGap";

    public const string PortInImage = "ImageIn";
    public const string PortLineCount = "LineCount";
    public const string PortPrimaryAngle = "PrimaryAngle";
    public const string PortAnnotatedImage = "AnnotatedImage";

    private readonly ToolParameter _pThresh;
    private readonly ToolParameter _pMinLen;
    private readonly ToolParameter _pMaxGap;

    public HoughLinesTool() : base("Fitting.HoughLines", "Hough Lines", "Geometry Fitting", "Hough line transform")
    {
        _pThresh = AddParameter(new ToolParameter(ParamThreshold, "Accumulator Threshold", ParameterType.Integer, 50, min: 10, max: 500));
        _pMinLen = AddParameter(new ToolParameter(ParamMinLength, "Min Line Length (px)", ParameterType.Double, 30.0, min: 5.0, max: 2000.0));
        _pMaxGap = AddParameter(new ToolParameter(ParamMaxGap, "Max Line Gap (px)", ParameterType.Double, 10.0, min: 0.0, max: 200.0));

        AddInput(new ToolPort(PortInImage, "Image In", PortType.Image, PortDirection.Input));
        AddOutput(new ToolPort(PortLineCount, "Line Count", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortPrimaryAngle, "Primary Angle (deg)", PortType.Number, PortDirection.Output));
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

        // Run Canny pre-pass if not already binary
        using var edges = new Mat();
        Cv2.Canny(gray, edges, 50, 150);

        var thresh = _pThresh.GetValue<int>();
        var minLen = _pMinLen.GetValue<double>();
        var maxGap = _pMaxGap.GetValue<double>();

        var lines = Cv2.HoughLinesP(edges, 1, Math.PI / 180.0, thresh, minLen, maxGap);

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
        double primaryAngle = 0.0;

        for (int i = 0; i < lines.Length; i++)
        {
            var l = lines[i];
            var p1 = new Point2f(l.P1.X, l.P1.Y);
            var p2 = new Point2f(l.P2.X, l.P2.Y);

            var ov = new LineOverlay(p1, p2, new Scalar(0, 255, 0), Thickness: 2);
            overlays.Add(ov);
            ov.Render(annotated);

            if (i == 0)
            {
                primaryAngle = Math.Atan2(l.P2.Y - l.P1.Y, l.P2.X - l.P1.X) * 180.0 / Math.PI;
            }
        }

        var outFrame = new ImageFrame(annotated, inputFrame.CameraId, inputFrame.FrameId, PixelFormat.Bgr24);
        var outputs = new Dictionary<string, object?>
        {
            [PortLineCount] = lines.Length,
            [PortPrimaryAngle] = primaryAngle,
            [PortAnnotatedImage] = outFrame
        };

        return ToolResult.Ok(outputs, overlays, message: $"Detected {lines.Length} Hough line segments");
    }
}
