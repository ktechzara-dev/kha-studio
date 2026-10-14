using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Filtering;

/// <summary>
/// Edge-preserving smoothing filter combining spatial distance and radiometric range weights.
/// </summary>
[VisionTool("Filter.Bilateral", "Bilateral Filter", "Filtering", Description = "Edge-preserving smoothing filter")]
public sealed class BilateralFilterTool : VisionToolBase
{
    public const string ParamDiameter = "Diameter";
    public const string ParamSigmaColor = "SigmaColor";
    public const string ParamSigmaSpace = "SigmaSpace";

    public const string PortInImage = "ImageIn";
    public const string PortOutImage = "ImageOut";

    private readonly ToolParameter _pDiameter;
    private readonly ToolParameter _pSigmaColor;
    private readonly ToolParameter _pSigmaSpace;

    public BilateralFilterTool() : base("Filter.Bilateral", "Bilateral Filter", "Filtering", "Edge-preserving smoothing")
    {
        _pDiameter = AddParameter(new ToolParameter(ParamDiameter, "Diameter (d)", ParameterType.Integer, 9, min: 1, max: 25));
        _pSigmaColor = AddParameter(new ToolParameter(ParamSigmaColor, "Sigma Color", ParameterType.Double, 75.0, min: 1.0, max: 250.0));
        _pSigmaSpace = AddParameter(new ToolParameter(ParamSigmaSpace, "Sigma Space", ParameterType.Double, 75.0, min: 1.0, max: 250.0));

        AddInput(new ToolPort(PortInImage, "Image In", PortType.Image, PortDirection.Input));
        AddOutput(new ToolPort(PortOutImage, "Image Out", PortType.Image, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var inputFrame = ctx.GetImage(PortInImage);
        if (inputFrame == null) return ToolResult.Failed("Missing input image.");

        var src = inputFrame.Mat;
        var d = _pDiameter.GetValue<int>();
        var sigmaColor = _pSigmaColor.GetValue<double>();
        var sigmaSpace = _pSigmaSpace.GetValue<double>();

        using var processMat = new Mat();
        if (src.Channels() == 4)
        {
            Cv2.CvtColor(src, processMat, ColorConversionCodes.BGRA2BGR);
        }
        else
        {
            src.CopyTo(processMat);
        }

        var dst = new Mat();
        Cv2.BilateralFilter(processMat, dst, d, sigmaColor, sigmaSpace);

        var outFrame = new ImageFrame(dst, inputFrame.CameraId, inputFrame.FrameId, inputFrame.Format);
        return ToolResult.Ok(new Dictionary<string, object?> { [PortOutImage] = outFrame }, message: $"Bilateral filtered (d={d}, σColor={sigmaColor:F0})");
    }
}
