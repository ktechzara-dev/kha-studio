using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Filtering;

/// <summary>
/// Fast uniform area averaging box filter for rapid background estimation and smoothing.
/// </summary>
[VisionTool("Filter.Box", "Box Filter", "Filtering", Description = "Uniform area averaging smoothing filter")]
public sealed class BoxFilterTool : VisionToolBase
{
    public const string ParamWidth = "KernelWidth";
    public const string ParamHeight = "KernelHeight";

    public const string PortInImage = "ImageIn";
    public const string PortOutImage = "ImageOut";

    private readonly ToolParameter _pKw;
    private readonly ToolParameter _pKh;

    public BoxFilterTool() : base("Filter.Box", "Box Filter", "Filtering", "Uniform box blur")
    {
        _pKw = AddParameter(new ToolParameter(ParamWidth, "Kernel Width", ParameterType.Integer, 5, min: 1, max: 101));
        _pKh = AddParameter(new ToolParameter(ParamHeight, "Kernel Height", ParameterType.Integer, 5, min: 1, max: 101));

        AddInput(new ToolPort(PortInImage, "Image In", PortType.Image, PortDirection.Input));
        AddOutput(new ToolPort(PortOutImage, "Image Out", PortType.Image, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var inputFrame = ctx.GetImage(PortInImage);
        if (inputFrame == null) return ToolResult.Failed("Missing input image.");

        var src = inputFrame.Mat;
        var kw = Math.Max(1, _pKw.GetValue<int>());
        var kh = Math.Max(1, _pKh.GetValue<int>());

        var dst = new Mat();
        Cv2.Blur(src, dst, new Size(kw, kh));

        var outFrame = new ImageFrame(dst, inputFrame.CameraId, inputFrame.FrameId, inputFrame.Format);
        return ToolResult.Ok(new Dictionary<string, object?> { [PortOutImage] = outFrame }, message: $"Box blurred ({kw}x{kh})");
    }
}
