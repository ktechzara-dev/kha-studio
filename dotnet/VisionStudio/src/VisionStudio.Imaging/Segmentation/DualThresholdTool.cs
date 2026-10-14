using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Segmentation;

/// <summary>
/// Dual-threshold segmenter filtering intensity bands within lower and upper bounds.
/// </summary>
[VisionTool("Segmentation.DualThreshold", "Dual Threshold", "Segmentation", Description = "Isolates intensity bands between lower and upper threshold levels")]
public sealed class DualThresholdTool : VisionToolBase
{
    public const string ParamLow = "LowThreshold";
    public const string ParamHigh = "HighThreshold";

    public const string PortInImage = "ImageIn";
    public const string PortMask = "Mask";
    public const string PortCount = "PixelCount";

    private readonly ToolParameter _pLow;
    private readonly ToolParameter _pHigh;

    public DualThresholdTool() : base("Segmentation.DualThreshold", "Dual Threshold", "Segmentation", "Band-pass intensity thresholding")
    {
        _pLow = AddParameter(new ToolParameter(ParamLow, "Low Threshold", ParameterType.Double, 50.0, min: 0.0, max: 255.0));
        _pHigh = AddParameter(new ToolParameter(ParamHigh, "High Threshold", ParameterType.Double, 200.0, min: 0.0, max: 255.0));

        AddInput(new ToolPort(PortInImage, "Image In", PortType.Image, PortDirection.Input));
        AddOutput(new ToolPort(PortMask, "Mask", PortType.Image, PortDirection.Output));
        AddOutput(new ToolPort(PortCount, "Pixel Count", PortType.Number, PortDirection.Output));
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

        var low = _pLow.GetValue<double>();
        var high = _pHigh.GetValue<double>();

        var dst = new Mat();
        Cv2.InRange(gray, new Scalar(low), new Scalar(high), dst);

        var count = Cv2.CountNonZero(dst);
        var outFrame = new ImageFrame(dst, inputFrame.CameraId, inputFrame.FrameId, PixelFormat.Mono8);

        var outputs = new Dictionary<string, object?>
        {
            [PortMask] = outFrame,
            [PortCount] = count
        };

        return ToolResult.Ok(outputs, message: $"Dual thresholded [{low:F0}, {high:F0}]: {count} px");
    }
}
