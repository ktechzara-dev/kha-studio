using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Geometry;

/// <summary>
/// Generates or inverts a binary mask for masking background areas outside an inspection region.
/// </summary>
[VisionTool("Geometry.MaskFromRegion", "Mask From Region", "Geometry / ROI", Description = "Applies or inverts a region mask on an image")]
public sealed class MaskFromRegionTool : VisionToolBase
{
    public const string ParamInvert = "InvertMask";
    public const string PortInImage = "ImageIn";
    public const string PortInMask = "MaskIn";
    public const string PortOutImage = "ImageOut";

    private readonly ToolParameter _pInvert;

    public MaskFromRegionTool() : base("Geometry.MaskFromRegion", "Mask From Region", "Geometry / ROI", "Applies mask to image")
    {
        _pInvert = AddParameter(new ToolParameter(ParamInvert, "Invert Mask", ParameterType.Boolean, false));

        AddInput(new ToolPort(PortInImage, "Image In", PortType.Image, PortDirection.Input));
        AddInput(new ToolPort(PortInMask, "Mask In", PortType.Image, PortDirection.Input));
        AddOutput(new ToolPort(PortOutImage, "Image Out", PortType.Image, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var inputFrame = ctx.GetImage(PortInImage);
        var maskFrame = ctx.GetImage(PortInMask);

        if (inputFrame == null) return ToolResult.Failed("Missing input image.");
        if (maskFrame == null) return ToolResult.Failed("Missing input mask.");

        var src = inputFrame.Mat;
        var mask = maskFrame.Mat;

        using var singleMask = new Mat();
        if (mask.Channels() > 1)
        {
            Cv2.CvtColor(mask, singleMask, ColorConversionCodes.BGR2GRAY);
        }
        else
        {
            mask.CopyTo(singleMask);
        }

        if (_pInvert.GetValue<bool>())
        {
            Cv2.BitwiseNot(singleMask, singleMask);
        }

        var dst = new Mat();
        src.CopyTo(dst, singleMask);

        var outFrame = new ImageFrame(dst, inputFrame.CameraId, inputFrame.FrameId, inputFrame.Format);
        return ToolResult.Ok(new Dictionary<string, object?> { [PortOutImage] = outFrame }, message: "Applied region mask");
    }
}
