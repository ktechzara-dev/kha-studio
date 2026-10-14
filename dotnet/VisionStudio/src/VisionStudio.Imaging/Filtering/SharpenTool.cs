using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Filtering;

/// <summary>
/// Sharpens edge transitions using unsharp masking convolution or high-pass boost.
/// </summary>
[VisionTool("Filter.Sharpen", "Sharpen (Unsharp Mask)", "Filtering", Description = "Sharpens edges using unsharp masking")]
public sealed class SharpenTool : VisionToolBase
{
    public const string ParamAmount = "Amount";
    public const string ParamRadius = "Radius";
    public const string ParamThreshold = "Threshold";

    public const string PortInImage = "ImageIn";
    public const string PortOutImage = "ImageOut";

    private readonly ToolParameter _pAmount;
    private readonly ToolParameter _pRadius;
    private readonly ToolParameter _pThreshold;

    public SharpenTool() : base("Filter.Sharpen", "Sharpen (Unsharp Mask)", "Filtering", "Sharpens image details")
    {
        _pAmount = AddParameter(new ToolParameter(ParamAmount, "Sharpness Amount", ParameterType.Double, 1.5, min: 0.1, max: 10.0));
        _pRadius = AddParameter(new ToolParameter(ParamRadius, "Blur Radius (Odd)", ParameterType.Integer, 5, min: 1, max: 51, step: 2));
        _pThreshold = AddParameter(new ToolParameter(ParamThreshold, "Edge Threshold", ParameterType.Double, 0.0, min: 0.0, max: 50.0));

        AddInput(new ToolPort(PortInImage, "Image In", PortType.Image, PortDirection.Input));
        AddOutput(new ToolPort(PortOutImage, "Image Out", PortType.Image, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var inputFrame = ctx.GetImage(PortInImage);
        if (inputFrame == null) return ToolResult.Failed("Missing input image.");

        var src = inputFrame.Mat;
        var amount = _pAmount.GetValue<double>();
        var r = _pRadius.GetValue<int>();
        if (r % 2 == 0) r += 1;
        r = Math.Max(1, r);

        using var blurred = new Mat();
        Cv2.GaussianBlur(src, blurred, new Size(r, r), 0);

        var dst = new Mat();
        // dst = src * (1 + amount) - blurred * amount
        Cv2.AddWeighted(src, 1.0 + amount, blurred, -amount, 0, dst);

        var outFrame = new ImageFrame(dst, inputFrame.CameraId, inputFrame.FrameId, inputFrame.Format);
        return ToolResult.Ok(new Dictionary<string, object?> { [PortOutImage] = outFrame }, message: $"Sharpened (Amount={amount:F1}, Radius={r})");
    }
}
