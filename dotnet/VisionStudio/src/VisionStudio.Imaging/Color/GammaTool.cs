using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Color;

/// <summary>
/// Applies non-linear gamma correction via precomputed 8-bit lookup table.
/// </summary>
[VisionTool("Color.Gamma", "Gamma Correction", "Color / Intensity", Description = "Non-linear power-law gamma luminance adjustment")]
public sealed class GammaTool : VisionToolBase
{
    public const string ParamGamma = "Gamma";
    public const string PortInImage = "ImageIn";
    public const string PortOutImage = "ImageOut";

    private readonly ToolParameter _pGamma;

    public GammaTool() : base("Color.Gamma", "Gamma Correction", "Color / Intensity", "Applies power-law gamma")
    {
        _pGamma = AddParameter(new ToolParameter(ParamGamma, "Gamma Value", ParameterType.Double, 1.0, min: 0.1, max: 5.0, step: 0.1));

        AddInput(new ToolPort(PortInImage, "Image In", PortType.Image, PortDirection.Input));
        AddOutput(new ToolPort(PortOutImage, "Image Out", PortType.Image, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var inputFrame = ctx.GetImage(PortInImage);
        if (inputFrame == null) return ToolResult.Failed("Missing input image.");

        var src = inputFrame.Mat;
        var gamma = _pGamma.GetValue<double>();
        if (gamma <= 0) gamma = 1.0;

        // Build 256-entry lookup table
        using var lut = new Mat(1, 256, MatType.CV_8U);
        var invGamma = 1.0 / gamma;
        for (int i = 0; i < 256; i++)
        {
            var val = Math.Pow(i / 255.0, invGamma) * 255.0;
            lut.Set(0, i, (byte)Math.Clamp((int)Math.Round(val), 0, 255));
        }

        var dst = new Mat();
        Cv2.LUT(src, lut, dst);

        var outFrame = new ImageFrame(dst, inputFrame.CameraId, inputFrame.FrameId, inputFrame.Format);
        return ToolResult.Ok(new Dictionary<string, object?> { [PortOutImage] = outFrame }, message: $"Gamma corrected (γ={gamma:F2})");
    }
}
