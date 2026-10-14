using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Color;

/// <summary>
/// Adjusts linear image brightness offset and contrast gain factor.
/// </summary>
[VisionTool("Color.BrightnessContrast", "Brightness / Contrast", "Color / Intensity", Description = "Adjusts linear contrast scale and brightness offset")]
public sealed class BrightnessContrastTool : VisionToolBase
{
    public const string ParamContrast = "ContrastGain";
    public const string ParamBrightness = "BrightnessOffset";

    public const string PortInImage = "ImageIn";
    public const string PortOutImage = "ImageOut";

    private readonly ToolParameter _pContrast;
    private readonly ToolParameter _pBrightness;

    public BrightnessContrastTool() : base("Color.BrightnessContrast", "Brightness / Contrast", "Color / Intensity", "Adjusts brightness and contrast")
    {
        _pContrast = AddParameter(new ToolParameter(ParamContrast, "Contrast Gain (Alpha)", ParameterType.Double, 1.0, min: 0.1, max: 5.0, step: 0.1));
        _pBrightness = AddParameter(new ToolParameter(ParamBrightness, "Brightness Offset (Beta)", ParameterType.Double, 0.0, min: -150.0, max: 150.0, step: 1.0));

        AddInput(new ToolPort(PortInImage, "Image In", PortType.Image, PortDirection.Input));
        AddOutput(new ToolPort(PortOutImage, "Image Out", PortType.Image, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var inputFrame = ctx.GetImage(PortInImage);
        if (inputFrame == null) return ToolResult.Failed("Missing input image.");

        var src = inputFrame.Mat;
        var alpha = _pContrast.GetValue<double>();
        var beta = _pBrightness.GetValue<double>();

        var dst = new Mat();
        src.ConvertTo(dst, -1, alpha, beta);

        var outFrame = new ImageFrame(dst, inputFrame.CameraId, inputFrame.FrameId, inputFrame.Format);
        return ToolResult.Ok(new Dictionary<string, object?> { [PortOutImage] = outFrame }, message: $"Adjusted (Alpha={alpha:F2}, Beta={beta:F0})");
    }
}
