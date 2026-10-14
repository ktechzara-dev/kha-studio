using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Filtering;

/// <summary>
/// Corrects lens vignetting and non-uniform lighting via flat-field reference normalization:
/// Corrected = ((Raw - Dark) / (Bright - Dark)) * Mean(Bright - Dark).
/// </summary>
[VisionTool("Filter.FlatFieldCorrection", "Flat-Field Correction", "Filtering", Description = "Buries non-uniform shading and lens vignetting using bright/dark frames")]
public sealed class FlatFieldCorrectionTool : VisionToolBase
{
    public const string PortRaw = "RawImage";
    public const string PortBright = "BrightField";
    public const string PortDark = "DarkField";
    public const string PortCorrected = "ImageOut";

    public FlatFieldCorrectionTool() : base("Filter.FlatFieldCorrection", "Flat-Field Correction", "Filtering", "Illumination shading correction")
    {
        AddInput(new ToolPort(PortRaw, "Raw Image", PortType.Image, PortDirection.Input));
        AddInput(new ToolPort(PortBright, "Bright Field Ref", PortType.Image, PortDirection.Input));
        AddInput(new ToolPort(PortDark, "Dark Field Ref", PortType.Image, PortDirection.Input, IsRequired: false));

        AddOutput(new ToolPort(PortCorrected, "Image Out", PortType.Image, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var rawFrame = ctx.GetImage(PortRaw);
        var brightFrame = ctx.GetImage(PortBright);
        var darkFrame = ctx.GetImage(PortDark);

        if (rawFrame == null) return ToolResult.Failed("Missing raw input image.");
        if (brightFrame == null) return ToolResult.Failed("Missing bright field reference image.");

        using var rawF = new Mat();
        using var brightF = new Mat();
        rawFrame.Mat.ConvertTo(rawF, MatType.CV_32F);
        brightFrame.Mat.ConvertTo(brightF, MatType.CV_32F);

        using var darkF = new Mat();
        if (darkFrame != null)
        {
            darkFrame.Mat.ConvertTo(darkF, MatType.CV_32F);
            Cv2.Subtract(rawF, darkF, rawF);
            Cv2.Subtract(brightF, darkF, brightF);
        }

        // Avoid division by zero
        Cv2.Max(brightF, new Scalar(1.0), brightF);

        var meanBright = Cv2.Mean(brightF).Val0;
        using var gain = new Mat();
        Cv2.Divide(rawF, brightF, gain);

        using var correctedF = new Mat();
        Cv2.Multiply(gain, new Scalar(meanBright), correctedF);

        var dst = new Mat();
        correctedF.ConvertTo(dst, rawFrame.Mat.Type());

        var outFrame = new ImageFrame(dst, rawFrame.CameraId, rawFrame.FrameId, rawFrame.Format);
        return ToolResult.Ok(new Dictionary<string, object?> { [PortCorrected] = outFrame }, message: "Applied flat-field shading correction");
    }
}
