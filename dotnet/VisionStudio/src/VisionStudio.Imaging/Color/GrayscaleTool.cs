using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Color;

/// <summary>
/// Converts color images to single-channel 8-bit monochrome grayscale.
/// </summary>
[VisionTool("Color.Grayscale", "Grayscale", "Color / Intensity", Description = "Converts color images to 8-bit monochrome grayscale")]
public sealed class GrayscaleTool : VisionToolBase
{
    public const string PortInImage = "ImageIn";
    public const string PortOutImage = "ImageOut";

    public GrayscaleTool() : base("Color.Grayscale", "Grayscale", "Color / Intensity", "Converts to monochrome")
    {
        AddInput(new ToolPort(PortInImage, "Image In", PortType.Image, PortDirection.Input));
        AddOutput(new ToolPort(PortOutImage, "Image Out", PortType.Image, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var inputFrame = ctx.GetImage(PortInImage);
        if (inputFrame == null) return ToolResult.Failed("Missing input image.");

        var src = inputFrame.Mat;
        if (src.Channels() == 1)
        {
            var passthrough = new ImageFrame(src.Clone(), inputFrame.CameraId, inputFrame.FrameId, PixelFormat.Mono8);
            return ToolResult.Ok(new Dictionary<string, object?> { [PortOutImage] = passthrough }, message: "Image already 1-channel grayscale.");
        }

        var dst = new Mat();
        if (src.Channels() == 4)
        {
            Cv2.CvtColor(src, dst, ColorConversionCodes.BGRA2GRAY);
        }
        else
        {
            Cv2.CvtColor(src, dst, ColorConversionCodes.BGR2GRAY);
        }

        var outFrame = new ImageFrame(dst, inputFrame.CameraId, inputFrame.FrameId, PixelFormat.Mono8);
        return ToolResult.Ok(new Dictionary<string, object?> { [PortOutImage] = outFrame }, message: $"Converted to grayscale ({dst.Cols}x{dst.Rows})");
    }
}
