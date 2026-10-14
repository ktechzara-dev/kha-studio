using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Edges;

/// <summary>
/// Computes second-order spatial Laplacian derivative for isotropic edge detection.
/// </summary>
[VisionTool("Edges.Laplacian", "Laplacian Operator", "Edges and Contours", Description = "Second-order isotropic derivative filter")]
public sealed class LaplacianTool : VisionToolBase
{
    public const string ParamKernelSize = "KernelSize";
    public const string PortInImage = "ImageIn";
    public const string PortOutImage = "ImageOut";

    private readonly ToolParameter _pKernel;

    public LaplacianTool() : base("Edges.Laplacian", "Laplacian Operator", "Edges and Contours", "Second-order Laplacian derivative")
    {
        _pKernel = AddParameter(new ToolParameter(ParamKernelSize, "Aperture Size (Odd)", ParameterType.Integer, 3, min: 1, max: 7, step: 2));

        AddInput(new ToolPort(PortInImage, "Image In", PortType.Image, PortDirection.Input));
        AddOutput(new ToolPort(PortOutImage, "Image Out", PortType.Image, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var inputFrame = ctx.GetImage(PortInImage);
        if (inputFrame == null) return ToolResult.Failed("Missing input image.");

        var src = inputFrame.Mat;
        using var gray = new Mat();
        if (src.Channels() > 1)
        {
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
        }
        else
        {
            src.CopyTo(gray);
        }

        var k = _pKernel.GetValue<int>();
        if (k % 2 == 0) k += 1;
        k = Math.Clamp(k, 1, 7);

        using var lap = new Mat();
        Cv2.Laplacian(gray, lap, MatType.CV_16S, k);

        var dst = new Mat();
        Cv2.ConvertScaleAbs(lap, dst);

        var outFrame = new ImageFrame(dst, inputFrame.CameraId, inputFrame.FrameId, PixelFormat.Mono8);
        return ToolResult.Ok(new Dictionary<string, object?> { [PortOutImage] = outFrame }, message: $"Laplacian derivative (k={k})");
    }
}
