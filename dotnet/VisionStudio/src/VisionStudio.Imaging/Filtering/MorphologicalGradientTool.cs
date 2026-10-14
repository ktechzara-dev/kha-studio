using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Filtering;

/// <summary>
/// Computes morphological gradient (dilation minus erosion) highlighting structural boundary profiles.
/// </summary>
[VisionTool("Filter.MorphGradient", "Morphological Gradient", "Filtering", Description = "Extracts boundary outlines via morphological gradient")]
public sealed class MorphologicalGradientTool : VisionToolBase
{
    public const string ParamKernelSize = "KernelSize";
    public const string ParamShape = "Shape";

    public const string PortInImage = "ImageIn";
    public const string PortOutImage = "ImageOut";

    private readonly ToolParameter _pKernel;
    private readonly ToolParameter _pShape;

    public MorphologicalGradientTool() : base("Filter.MorphGradient", "Morphological Gradient", "Filtering", "Morphological boundary extraction")
    {
        _pKernel = AddParameter(new ToolParameter(ParamKernelSize, "Kernel Size (Odd)", ParameterType.Integer, 3, min: 1, max: 31, step: 2));
        _pShape = AddParameter(new ToolParameter(ParamShape, "Structuring Element", ParameterType.Enum, "Rect", enumOptions: new[] { "Rect", "Cross", "Ellipse" }));

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

        var shapeStr = _pShape.GetValue<string>() ?? "Rect";
        var shape = shapeStr.ToLowerInvariant() switch
        {
            "cross" => MorphShapes.Cross,
            "ellipse" => MorphShapes.Ellipse,
            _ => MorphShapes.Rect
        };

        using var element = Cv2.GetStructuringElement(shape, new Size(k, k));
        var dst = new Mat();
        Cv2.MorphologyEx(src, dst, MorphTypes.Gradient, element);

        var outFrame = new ImageFrame(dst, inputFrame.CameraId, inputFrame.FrameId, inputFrame.Format);
        return ToolResult.Ok(new Dictionary<string, object?> { [PortOutImage] = outFrame }, message: $"Morph gradient extracted (k={k}, shape={shapeStr})");
    }
}
