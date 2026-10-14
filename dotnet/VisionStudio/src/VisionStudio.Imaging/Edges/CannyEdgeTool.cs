using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Edges;

/// <summary>
/// Detects wide range of strong and weak edge transitions using multi-stage Canny algorithm with hysteresis.
/// </summary>
[VisionTool("Edges.Canny", "Canny Edge Detector", "Edges and Contours", Description = "Multi-stage optimal edge detector with hysteresis")]
public sealed class CannyEdgeTool : VisionToolBase
{
    public const string ParamThresh1 = "ThresholdLow";
    public const string ParamThresh2 = "ThresholdHigh";
    public const string ParamAperture = "ApertureSize";
    public const string ParamL2 = "L2Gradient";

    public const string PortInImage = "ImageIn";
    public const string PortEdgeImage = "EdgeImage";
    public const string PortEdgeCount = "EdgePixelCount";

    private readonly ToolParameter _pT1;
    private readonly ToolParameter _pT2;
    private readonly ToolParameter _pAperture;
    private readonly ToolParameter _pL2;

    public CannyEdgeTool() : base("Edges.Canny", "Canny Edge Detector", "Edges and Contours", "Detects edges via Canny algorithm")
    {
        _pT1 = AddParameter(new ToolParameter(ParamThresh1, "Low Threshold", ParameterType.Double, 50.0, min: 0.0, max: 500.0));
        _pT2 = AddParameter(new ToolParameter(ParamThresh2, "High Threshold", ParameterType.Double, 150.0, min: 0.0, max: 500.0));
        _pAperture = AddParameter(new ToolParameter(ParamAperture, "Sobel Aperture Size (Odd)", ParameterType.Integer, 3, min: 3, max: 7, step: 2));
        _pL2 = AddParameter(new ToolParameter(ParamL2, "Use L2 Gradient Norm", ParameterType.Boolean, true));

        AddInput(new ToolPort(PortInImage, "Image In", PortType.Image, PortDirection.Input));
        AddOutput(new ToolPort(PortEdgeImage, "Edge Image", PortType.Image, PortDirection.Output));
        AddOutput(new ToolPort(PortEdgeCount, "Edge Pixel Count", PortType.Number, PortDirection.Output));
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

        var t1 = _pT1.GetValue<double>();
        var t2 = _pT2.GetValue<double>();
        var aperture = _pAperture.GetValue<int>();
        if (aperture % 2 == 0) aperture += 1;
        aperture = Math.Clamp(aperture, 3, 7);
        var l2 = _pL2.GetValue<bool>();

        var dst = new Mat();
        Cv2.Canny(gray, dst, t1, t2, aperture, l2);

        var edgeCount = Cv2.CountNonZero(dst);
        var outFrame = new ImageFrame(dst, inputFrame.CameraId, inputFrame.FrameId, PixelFormat.Mono8);

        var outputs = new Dictionary<string, object?>
        {
            [PortEdgeImage] = outFrame,
            [PortEdgeCount] = edgeCount
        };

        return ToolResult.Ok(outputs, message: $"Canny detected {edgeCount} edge pixels (T=[{t1:F0}, {t2:F0}])");
    }
}
