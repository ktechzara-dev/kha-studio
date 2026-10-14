using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Edges;

/// <summary>
/// Computes first-order spatial image gradients using Sobel or Scharr operators.
/// </summary>
[VisionTool("Edges.SobelScharr", "Sobel / Scharr Gradient", "Edges and Contours", Description = "Computes directional gradient derivative maps")]
public sealed class SobelScharrTool : VisionToolBase
{
    public const string ParamOperator = "Operator";
    public const string ParamKernelSize = "SobelKernelSize";
    public const string ParamDirection = "Direction";

    public const string PortInImage = "ImageIn";
    public const string PortMagnitude = "GradientMagnitude";

    private readonly ToolParameter _pOp;
    private readonly ToolParameter _pKernel;
    private readonly ToolParameter _pDir;

    public SobelScharrTool() : base("Edges.SobelScharr", "Sobel / Scharr Gradient", "Edges and Contours", "Gradient magnitude derivative")
    {
        _pOp = AddParameter(new ToolParameter(ParamOperator, "Operator", ParameterType.Enum, "Sobel", enumOptions: new[] { "Sobel", "Scharr" }));
        _pKernel = AddParameter(new ToolParameter(ParamKernelSize, "Sobel Kernel Size (Odd)", ParameterType.Integer, 3, min: 1, max: 7, step: 2));
        _pDir = AddParameter(new ToolParameter(ParamDirection, "Direction", ParameterType.Enum, "Magnitude", enumOptions: new[] { "Magnitude", "HorizontalX", "VerticalY" }));

        AddInput(new ToolPort(PortInImage, "Image In", PortType.Image, PortDirection.Input));
        AddOutput(new ToolPort(PortMagnitude, "Gradient Magnitude", PortType.Image, PortDirection.Output));
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

        var isScharr = (_pOp.GetValue<string>() ?? "Sobel").Equals("Scharr", StringComparison.OrdinalIgnoreCase);
        var dir = _pDir.GetValue<string>() ?? "Magnitude";
        var k = _pKernel.GetValue<int>();
        if (k % 2 == 0) k += 1;
        k = Math.Clamp(k, 1, 7);

        using var gradX = new Mat();
        using var gradY = new Mat();

        if (isScharr)
        {
            Cv2.Scharr(gray, gradX, MatType.CV_16S, 1, 0);
            Cv2.Scharr(gray, gradY, MatType.CV_16S, 0, 1);
        }
        else
        {
            Cv2.Sobel(gray, gradX, MatType.CV_16S, 1, 0, k);
            Cv2.Sobel(gray, gradY, MatType.CV_16S, 0, 1, k);
        }

        using var absX = new Mat();
        using var absY = new Mat();
        Cv2.ConvertScaleAbs(gradX, absX);
        Cv2.ConvertScaleAbs(gradY, absY);

        var dst = new Mat();
        switch (dir.ToLowerInvariant())
        {
            case "horizontalx":
                absX.CopyTo(dst);
                break;
            case "verticaly":
                absY.CopyTo(dst);
                break;
            default: // Magnitude
                Cv2.AddWeighted(absX, 0.5, absY, 0.5, 0, dst);
                break;
        }

        var outFrame = new ImageFrame(dst, inputFrame.CameraId, inputFrame.FrameId, PixelFormat.Mono8);
        return ToolResult.Ok(new Dictionary<string, object?> { [PortMagnitude] = outFrame }, message: $"Computed {(isScharr ? "Scharr" : "Sobel")} {dir}");
    }
}
