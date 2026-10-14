using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Morphology;

/// <summary>
/// Executes fundamental mathematical morphology operations (Erode, Dilate, Open, Close, Top-Hat, Black-Hat).
/// </summary>
[VisionTool("Morphology.Basic", "Morphology", "Morphology", Description = "Applies morphological Erode, Dilate, Open, Close, Top-Hat, Black-Hat")]
public sealed class MorphologyTool : VisionToolBase
{
    public const string ParamOperation = "Operation";
    public const string ParamKernelSize = "KernelSize";
    public const string ParamShape = "Shape";
    public const string ParamIterations = "Iterations";

    public const string PortInImage = "ImageIn";
    public const string PortOutImage = "ImageOut";

    private readonly ToolParameter _pOp;
    private readonly ToolParameter _pKernel;
    private readonly ToolParameter _pShape;
    private readonly ToolParameter _pIter;

    public MorphologyTool() : base("Morphology.Basic", "Morphology", "Morphology", "Mathematical morphology operator")
    {
        _pOp = AddParameter(new ToolParameter(
            ParamOperation, "Operation", ParameterType.Enum, "Open",
            enumOptions: new[] { "Erode", "Dilate", "Open", "Close", "TopHat", "BlackHat" }));

        _pKernel = AddParameter(new ToolParameter(ParamKernelSize, "Kernel Size (Odd)", ParameterType.Integer, 3, min: 1, max: 99, step: 2));
        _pShape = AddParameter(new ToolParameter(ParamShape, "Kernel Shape", ParameterType.Enum, "Rect", enumOptions: new[] { "Rect", "Cross", "Ellipse" }));
        _pIter = AddParameter(new ToolParameter(ParamIterations, "Iterations", ParameterType.Integer, 1, min: 1, max: 50));

        AddInput(new ToolPort(PortInImage, "Image In", PortType.Image, PortDirection.Input));
        AddOutput(new ToolPort(PortOutImage, "Image Out", PortType.Image, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var inputFrame = ctx.GetImage(PortInImage);
        if (inputFrame == null) return ToolResult.Failed("Missing input image.");

        var src = inputFrame.Mat;
        var opStr = _pOp.GetValue<string>() ?? "Open";
        var k = _pKernel.GetValue<int>();
        if (k % 2 == 0) k += 1;
        k = Math.Clamp(k, 1, 99);

        var shapeStr = _pShape.GetValue<string>() ?? "Rect";
        var shape = shapeStr.ToLowerInvariant() switch
        {
            "cross" => MorphShapes.Cross,
            "ellipse" => MorphShapes.Ellipse,
            _ => MorphShapes.Rect
        };

        var iter = Math.Clamp(_pIter.GetValue<int>(), 1, 50);

        using var element = Cv2.GetStructuringElement(shape, new Size(k, k));
        var dst = new Mat();

        switch (opStr.ToLowerInvariant())
        {
            case "erode":
                Cv2.Erode(src, dst, element, iterations: iter);
                break;
            case "dilate":
                Cv2.Dilate(src, dst, element, iterations: iter);
                break;
            case "close":
                Cv2.MorphologyEx(src, dst, MorphTypes.Close, element, iterations: iter);
                break;
            case "tophat":
                Cv2.MorphologyEx(src, dst, MorphTypes.TopHat, element, iterations: iter);
                break;
            case "blackhat":
                Cv2.MorphologyEx(src, dst, MorphTypes.BlackHat, element, iterations: iter);
                break;
            default: // Open
                Cv2.MorphologyEx(src, dst, MorphTypes.Open, element, iterations: iter);
                break;
        }

        var outFrame = new ImageFrame(dst, inputFrame.CameraId, inputFrame.FrameId, inputFrame.Format);
        return ToolResult.Ok(new Dictionary<string, object?> { [PortOutImage] = outFrame }, message: $"Applied {opStr} (k={k}, iter={iter})");
    }
}
