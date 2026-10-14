using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Geometry;

/// <summary>
/// Mirrors an image horizontally, vertically, or along both axes.
/// </summary>
[VisionTool("Geometry.Flip", "Flip", "Geometry / ROI", Description = "Mirrors an image horizontally or vertically")]
public sealed class FlipTool : VisionToolBase
{
    public const string ParamMode = "FlipMode";
    public const string PortInImage = "ImageIn";
    public const string PortOutImage = "ImageOut";

    private readonly ToolParameter _pMode;

    public FlipTool() : base("Geometry.Flip", "Flip", "Geometry / ROI", "Flips an image")
    {
        _pMode = AddParameter(new ToolParameter(
            ParamMode, "Flip Mode", ParameterType.Enum, "Horizontal",
            enumOptions: new[] { "Horizontal", "Vertical", "Both" }));

        AddInput(new ToolPort(PortInImage, "Image In", PortType.Image, PortDirection.Input));
        AddOutput(new ToolPort(PortOutImage, "Image Out", PortType.Image, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var inputFrame = ctx.GetImage(PortInImage);
        if (inputFrame == null) return ToolResult.Failed("Missing input image.");

        var src = inputFrame.Mat;
        var modeStr = _pMode.GetValue<string>() ?? "Horizontal";

        var flipCode = modeStr.ToLowerInvariant() switch
        {
            "vertical" => FlipMode.X,
            "both" => FlipMode.XY,
            _ => FlipMode.Y // Horizontal
        };

        var dst = new Mat();
        Cv2.Flip(src, dst, flipCode);

        var outFrame = new ImageFrame(dst, inputFrame.CameraId, inputFrame.FrameId, inputFrame.Format);
        return ToolResult.Ok(new Dictionary<string, object?> { [PortOutImage] = outFrame }, message: $"Flipped {modeStr}");
    }
}
