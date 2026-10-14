using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Geometry;

/// <summary>
/// Scales an image to arbitrary pixel dimensions or proportional ratios.
/// </summary>
[VisionTool("Geometry.Resize", "Resize", "Geometry / ROI", Description = "Scales an image to specified dimensions or ratios")]
public sealed class ResizeTool : VisionToolBase
{
    public const string ParamMode = "Mode";
    public const string ParamWidth = "Width";
    public const string ParamHeight = "Height";
    public const string ParamScale = "Scale";
    public const string ParamInterp = "Interpolation";

    public const string PortInImage = "ImageIn";
    public const string PortOutImage = "ImageOut";

    private readonly ToolParameter _pMode;
    private readonly ToolParameter _pWidth;
    private readonly ToolParameter _pHeight;
    private readonly ToolParameter _pScale;
    private readonly ToolParameter _pInterp;

    public ResizeTool() : base("Geometry.Resize", "Resize", "Geometry / ROI", "Scales image size")
    {
        _pMode = AddParameter(new ToolParameter(ParamMode, "Mode", ParameterType.Enum, "ExplicitSize", enumOptions: new[] { "ExplicitSize", "ScaleFactor" }));
        _pWidth = AddParameter(new ToolParameter(ParamWidth, "Width", ParameterType.Integer, 320, min: 1, max: 16384));
        _pHeight = AddParameter(new ToolParameter(ParamHeight, "Height", ParameterType.Integer, 240, min: 1, max: 16384));
        _pScale = AddParameter(new ToolParameter(ParamScale, "Scale Factor", ParameterType.Double, 0.5, min: 0.01, max: 10.0));
        _pInterp = AddParameter(new ToolParameter(ParamInterp, "Interpolation", ParameterType.Enum, "Linear", enumOptions: new[] { "Nearest", "Linear", "Area", "Cubic" }));

        AddInput(new ToolPort(PortInImage, "Image In", PortType.Image, PortDirection.Input));
        AddOutput(new ToolPort(PortOutImage, "Image Out", PortType.Image, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var inputFrame = ctx.GetImage(PortInImage);
        if (inputFrame == null) return ToolResult.Failed("Missing input image.");

        var src = inputFrame.Mat;
        var mode = _pMode.GetValue<string>() ?? "ExplicitSize";

        int targetW, targetH;
        if (mode.Equals("ScaleFactor", StringComparison.OrdinalIgnoreCase))
        {
            var s = _pScale.GetValue<double>();
            targetW = Math.Max(1, (int)(src.Cols * s));
            targetH = Math.Max(1, (int)(src.Rows * s));
        }
        else
        {
            targetW = Math.Max(1, _pWidth.GetValue<int>());
            targetH = Math.Max(1, _pHeight.GetValue<int>());
        }

        var interp = (_pInterp.GetValue<string>() ?? "Linear").ToLowerInvariant() switch
        {
            "nearest" => InterpolationFlags.Nearest,
            "area" => InterpolationFlags.Area,
            "cubic" => InterpolationFlags.Cubic,
            _ => InterpolationFlags.Linear
        };

        var dst = new Mat();
        Cv2.Resize(src, dst, new Size(targetW, targetH), interpolation: interp);

        var outFrame = new ImageFrame(dst, inputFrame.CameraId, inputFrame.FrameId, inputFrame.Format);
        return ToolResult.Ok(new Dictionary<string, object?> { [PortOutImage] = outFrame }, message: $"Resized to {targetW}x{targetH}");
    }
}
