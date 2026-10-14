using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Filtering;

/// <summary>
/// Eliminates salt-and-pepper impulse noise while preserving sharp edge transitions.
/// </summary>
[VisionTool("Filter.Median", "Median Filter", "Filtering", Description = "Non-linear filter removing salt-and-pepper impulse noise")]
public sealed class MedianFilterTool : VisionToolBase
{
    public const string ParamKernelSize = "KernelSize";
    public const string PortInImage = "ImageIn";
    public const string PortOutImage = "ImageOut";

    private readonly ToolParameter _pKernel;

    public MedianFilterTool() : base("Filter.Median", "Median Filter", "Filtering", "Removes salt-and-pepper noise")
    {
        _pKernel = AddParameter(new ToolParameter(ParamKernelSize, "Kernel Size (Odd)", ParameterType.Integer, 3, min: 1, max: 31, step: 2));

        AddInput(new ToolPort(PortInImage, "Image In", PortType.Image, PortDirection.Input));
        AddOutput(new ToolPort(PortOutImage, "Image Out", PortType.Image, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var inputFrame = ctx.GetImage(PortInImage);
        if (inputFrame == null) return ToolResult.Failed("Missing input image.");

        var src = inputFrame.Mat;
        var k = _pKernel.GetValue<int>();
        if (k % 2 == 0) k += 1;
        k = Math.Clamp(k, 1, 31);

        var dst = new Mat();
        Cv2.MedianBlur(src, dst, k);

        var outFrame = new ImageFrame(dst, inputFrame.CameraId, inputFrame.FrameId, inputFrame.Format);
        return ToolResult.Ok(new Dictionary<string, object?> { [PortOutImage] = outFrame }, message: $"Median filtered (k={k})");
    }
}
