using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Color;

/// <summary>
/// Converts color images between standard color spaces (HSV, Lab, YCrCb).
/// </summary>
[VisionTool("Color.ColorSpaceConvert", "Color Space Convert", "Color / Intensity", Description = "Transforms image between color spaces (HSV, CIE Lab, YCrCb)")]
public sealed class ColorSpaceConvertTool : VisionToolBase
{
    public const string ParamSpace = "TargetSpace";
    public const string PortInImage = "ImageIn";
    public const string PortOutImage = "ImageOut";
    public const string PortChannel1 = "Ch1";
    public const string PortChannel2 = "Ch2";
    public const string PortChannel3 = "Ch3";

    private readonly ToolParameter _pSpace;

    public ColorSpaceConvertTool() : base("Color.ColorSpaceConvert", "Color Space Convert", "Color / Intensity", "Color space converter")
    {
        _pSpace = AddParameter(new ToolParameter(
            ParamSpace, "Target Color Space", ParameterType.Enum, "HSV",
            enumOptions: new[] { "HSV", "Lab", "YCrCb" }));

        AddInput(new ToolPort(PortInImage, "Image In", PortType.Image, PortDirection.Input));
        AddOutput(new ToolPort(PortOutImage, "Image Out", PortType.Image, PortDirection.Output));
        AddOutput(new ToolPort(PortChannel1, "Ch1 (H / L / Y)", PortType.Image, PortDirection.Output));
        AddOutput(new ToolPort(PortChannel2, "Ch2 (S / a / Cr)", PortType.Image, PortDirection.Output));
        AddOutput(new ToolPort(PortChannel3, "Ch3 (V / b / Cb)", PortType.Image, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var inputFrame = ctx.GetImage(PortInImage);
        if (inputFrame == null) return ToolResult.Failed("Missing input image.");

        var src = inputFrame.Mat;
        if (src.Channels() < 3) return ToolResult.Failed("Input must have 3 color channels.");

        var space = _pSpace.GetValue<string>() ?? "HSV";
        var code = space.ToLowerInvariant() switch
        {
            "lab" => ColorConversionCodes.BGR2Lab,
            "ycrcb" => ColorConversionCodes.BGR2YCrCb,
            _ => ColorConversionCodes.BGR2HSV
        };

        var dst = new Mat();
        Cv2.CvtColor(src, dst, code);

        var split = Cv2.Split(dst);
        var f1 = new ImageFrame(split[0], inputFrame.CameraId, inputFrame.FrameId, PixelFormat.Mono8);
        var f2 = new ImageFrame(split[1], inputFrame.CameraId, inputFrame.FrameId, PixelFormat.Mono8);
        var f3 = new ImageFrame(split[2], inputFrame.CameraId, inputFrame.FrameId, PixelFormat.Mono8);

        var outFrame = new ImageFrame(dst, inputFrame.CameraId, inputFrame.FrameId, inputFrame.Format);
        var outputs = new Dictionary<string, object?>
        {
            [PortOutImage] = outFrame,
            [PortChannel1] = f1,
            [PortChannel2] = f2,
            [PortChannel3] = f3
        };

        return ToolResult.Ok(outputs, message: $"Converted BGR to {space}");
    }
}
