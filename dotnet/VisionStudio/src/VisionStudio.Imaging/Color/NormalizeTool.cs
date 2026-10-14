using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Color;

/// <summary>
/// Normalizes image intensity distribution across a target min-max dynamic range.
/// </summary>
[VisionTool("Color.Normalize", "Normalize", "Color / Intensity", Description = "Stretches histogram intensity to target min/max boundaries")]
public sealed class NormalizeTool : VisionToolBase
{
    public const string ParamMin = "MinVal";
    public const string ParamMax = "MaxVal";

    public const string PortInImage = "ImageIn";
    public const string PortOutImage = "ImageOut";

    private readonly ToolParameter _pMin;
    private readonly ToolParameter _pMax;

    public NormalizeTool() : base("Color.Normalize", "Normalize", "Color / Intensity", "Normalizes intensity range")
    {
        _pMin = AddParameter(new ToolParameter(ParamMin, "Min Target Intensity", ParameterType.Double, 0.0, min: 0.0, max: 255.0));
        _pMax = AddParameter(new ToolParameter(ParamMax, "Max Target Intensity", ParameterType.Double, 255.0, min: 0.0, max: 255.0));

        AddInput(new ToolPort(PortInImage, "Image In", PortType.Image, PortDirection.Input));
        AddOutput(new ToolPort(PortOutImage, "Image Out", PortType.Image, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var inputFrame = ctx.GetImage(PortInImage);
        if (inputFrame == null) return ToolResult.Failed("Missing input image.");

        var src = inputFrame.Mat;
        var minVal = _pMin.GetValue<double>();
        var maxVal = _pMax.GetValue<double>();

        var dst = new Mat();
        Cv2.Normalize(src, dst, alpha: minVal, beta: maxVal, normType: NormTypes.MinMax);

        var outFrame = new ImageFrame(dst, inputFrame.CameraId, inputFrame.FrameId, inputFrame.Format);
        return ToolResult.Ok(new Dictionary<string, object?> { [PortOutImage] = outFrame }, message: $"Normalized to [{minVal:F0}, {maxVal:F0}]");
    }
}
