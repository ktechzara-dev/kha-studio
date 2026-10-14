using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Color;

/// <summary>
/// Splits a multi-channel color image into individual Blue, Green, and Red channel frames.
/// </summary>
[VisionTool("Color.ChannelSplit", "Channel Split", "Color / Intensity", Description = "Separates BGR image into Blue, Green, and Red channels")]
public sealed class ChannelSplitTool : VisionToolBase
{
    public const string PortInImage = "ImageIn";
    public const string PortBlue = "Blue";
    public const string PortGreen = "Green";
    public const string PortRed = "Red";

    public ChannelSplitTool() : base("Color.ChannelSplit", "Channel Split", "Color / Intensity", "Splits color channels")
    {
        AddInput(new ToolPort(PortInImage, "Image In", PortType.Image, PortDirection.Input));

        AddOutput(new ToolPort(PortBlue, "Blue Channel", PortType.Image, PortDirection.Output));
        AddOutput(new ToolPort(PortGreen, "Green Channel", PortType.Image, PortDirection.Output));
        AddOutput(new ToolPort(PortRed, "Red Channel", PortType.Image, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var inputFrame = ctx.GetImage(PortInImage);
        if (inputFrame == null) return ToolResult.Failed("Missing input image.");

        var src = inputFrame.Mat;
        if (src.Channels() < 3)
        {
            return ToolResult.Failed("Channel split requires at least 3 color channels.");
        }

        var channels = Cv2.Split(src);

        var fBlue = new ImageFrame(channels[0], inputFrame.CameraId, inputFrame.FrameId, PixelFormat.Mono8);
        var fGreen = new ImageFrame(channels[1], inputFrame.CameraId, inputFrame.FrameId, PixelFormat.Mono8);
        var fRed = new ImageFrame(channels[2], inputFrame.CameraId, inputFrame.FrameId, PixelFormat.Mono8);

        // If 4 channels (BGRA), dispose alpha channel
        if (channels.Length > 3)
        {
            for (int i = 3; i < channels.Length; i++) channels[i].Dispose();
        }

        var outputs = new Dictionary<string, object?>
        {
            [PortBlue] = fBlue,
            [PortGreen] = fGreen,
            [PortRed] = fRed
        };

        return ToolResult.Ok(outputs, message: "Split image into B, G, R channels");
    }
}
