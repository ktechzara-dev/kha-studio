using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Color;

/// <summary>
/// Inverts pixel intensities producing a photographic negative (Bitwise NOT).
/// </summary>
[VisionTool("Color.Invert", "Invert", "Color / Intensity", Description = "Produces negative image by inverting bit values")]
public sealed class InvertTool : VisionToolBase
{
    public const string PortInImage = "ImageIn";
    public const string PortOutImage = "ImageOut";

    public InvertTool() : base("Color.Invert", "Invert", "Color / Intensity", "Inverts image colors")
    {
        AddInput(new ToolPort(PortInImage, "Image In", PortType.Image, PortDirection.Input));
        AddOutput(new ToolPort(PortOutImage, "Image Out", PortType.Image, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var inputFrame = ctx.GetImage(PortInImage);
        if (inputFrame == null) return ToolResult.Failed("Missing input image.");

        var src = inputFrame.Mat;
        var dst = new Mat();
        Cv2.BitwiseNot(src, dst);

        var outFrame = new ImageFrame(dst, inputFrame.CameraId, inputFrame.FrameId, inputFrame.Format);
        return ToolResult.Ok(new Dictionary<string, object?> { [PortOutImage] = outFrame }, message: "Inverted intensities");
    }
}
