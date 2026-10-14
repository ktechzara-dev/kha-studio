using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Filtering;

/// <summary>
/// Applies 2D Gaussian convolution smoothing to attenuate high-frequency noise.
/// </summary>
[VisionTool("Filter.Gaussian", "Gaussian Filter", "Filtering", Description = "Applies Gaussian low-pass spatial smoothing")]
public sealed class GaussianFilterTool : VisionToolBase
{
    public const string ParamKernelSize = "KernelSize";
    public const string ParamSigmaX = "SigmaX";
    public const string ParamSigmaY = "SigmaY";

    public const string PortInImage = "ImageIn";
    public const string PortOutImage = "ImageOut";

    private readonly ToolParameter _pKernel;
    private readonly ToolParameter _pSigmaX;
    private readonly ToolParameter _pSigmaY;

    public GaussianFilterTool() : base("Filter.Gaussian", "Gaussian Filter", "Filtering", "Smooths image using Gaussian blur")
    {
        _pKernel = AddParameter(new ToolParameter(ParamKernelSize, "Kernel Size (Odd)", ParameterType.Integer, 5, min: 1, max: 99, step: 2));
        _pSigmaX = AddParameter(new ToolParameter(ParamSigmaX, "Sigma X", ParameterType.Double, 1.5, min: 0.1, max: 50.0));
        _pSigmaY = AddParameter(new ToolParameter(ParamSigmaY, "Sigma Y", ParameterType.Double, 0.0, min: 0.0, max: 50.0));

        AddInput(new ToolPort(PortInImage, "Image In", PortType.Image, PortDirection.Input));
        AddOutput(new ToolPort(PortOutImage, "Image Out", PortType.Image, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var inputFrame = ctx.GetImage(PortInImage);
        if (inputFrame == null) return ToolResult.Failed("Missing input image.");

        var src = inputFrame.Mat;
        var k = _pKernel.GetValue<int>();
        if (k % 2 == 0) k += 1; // Ensure odd kernel
        k = Math.Max(1, k);

        var sigmaX = _pSigmaX.GetValue<double>();
        var sigmaY = _pSigmaY.GetValue<double>();

        var dst = new Mat();
        Cv2.GaussianBlur(src, dst, new Size(k, k), sigmaX, sigmaY);

        var outFrame = new ImageFrame(dst, inputFrame.CameraId, inputFrame.FrameId, inputFrame.Format);
        return ToolResult.Ok(new Dictionary<string, object?> { [PortOutImage] = outFrame }, message: $"Gaussian filtered (Kernel={k}x{k}, σ={sigmaX:F1})");
    }
}
