using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Segmentation;

/// <summary>
/// Binarizes grayscale images into black and white regions using Fixed, Otsu, or Adaptive thresholding.
/// </summary>
[VisionTool("Segmentation.Threshold", "Threshold", "Segmentation", Description = "Binarizes image using Fixed, Otsu, or Adaptive algorithms")]
public sealed class ThresholdTool : VisionToolBase
{
    public const string ParamMethod = "Method";
    public const string ParamThreshold = "ThresholdValue";
    public const string ParamInvert = "Invert";
    public const string ParamBlockSize = "AdaptiveBlockSize";
    public const string ParamC = "AdaptiveC";

    public const string PortInImage = "ImageIn";
    public const string PortMask = "Mask";
    public const string PortCalculatedThreshold = "CalculatedThreshold";

    private readonly ToolParameter _pMethod;
    private readonly ToolParameter _pThresh;
    private readonly ToolParameter _pInvert;
    private readonly ToolParameter _pBlock;
    private readonly ToolParameter _pC;

    public ThresholdTool() : base("Segmentation.Threshold", "Threshold", "Segmentation", "Binarizes grayscale image")
    {
        _pMethod = AddParameter(new ToolParameter(ParamMethod, "Method", ParameterType.Enum, "Otsu", enumOptions: new[] { "Fixed", "Otsu", "AdaptiveMean", "AdaptiveGaussian" }));
        _pThresh = AddParameter(new ToolParameter(ParamThreshold, "Threshold Value", ParameterType.Double, 128.0, min: 0.0, max: 255.0));
        _pInvert = AddParameter(new ToolParameter(ParamInvert, "Invert Output", ParameterType.Boolean, false));
        _pBlock = AddParameter(new ToolParameter(ParamBlockSize, "Adaptive Block Size", ParameterType.Integer, 11, min: 3, max: 99, step: 2));
        _pC = AddParameter(new ToolParameter(ParamC, "Adaptive Constant (C)", ParameterType.Double, 2.0, min: -50.0, max: 50.0));

        AddInput(new ToolPort(PortInImage, "Image In", PortType.Image, PortDirection.Input));
        AddOutput(new ToolPort(PortMask, "Mask", PortType.Image, PortDirection.Output));
        AddOutput(new ToolPort(PortCalculatedThreshold, "Calculated Threshold", PortType.Number, PortDirection.Output));
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

        var method = _pMethod.GetValue<string>() ?? "Otsu";
        var invert = _pInvert.GetValue<bool>();
        var threshVal = _pThresh.GetValue<double>();

        var dst = new Mat();
        double calculatedThresh = threshVal;

        switch (method.ToLowerInvariant())
        {
            case "fixed":
                var fType = invert ? ThresholdTypes.BinaryInv : ThresholdTypes.Binary;
                Cv2.Threshold(gray, dst, threshVal, 255, fType);
                break;

            case "adaptivemean":
            case "adaptivegaussian":
                var adaptiveType = method.ToLowerInvariant().Contains("mean")
                    ? AdaptiveThresholdTypes.MeanC
                    : AdaptiveThresholdTypes.GaussianC;
                var aType = invert ? ThresholdTypes.BinaryInv : ThresholdTypes.Binary;
                var blockSize = _pBlock.GetValue<int>();
                if (blockSize % 2 == 0) blockSize += 1;
                blockSize = Math.Max(3, blockSize);
                var c = _pC.GetValue<double>();

                Cv2.AdaptiveThreshold(gray, dst, 255, adaptiveType, aType, blockSize, c);
                calculatedThresh = 0;
                break;

            default: // Otsu
                var oType = (invert ? ThresholdTypes.BinaryInv : ThresholdTypes.Binary) | ThresholdTypes.Otsu;
                calculatedThresh = Cv2.Threshold(gray, dst, 0, 255, oType);
                break;
        }

        var outFrame = new ImageFrame(dst, inputFrame.CameraId, inputFrame.FrameId, PixelFormat.Mono8);
        var outputs = new Dictionary<string, object?>
        {
            [PortMask] = outFrame,
            [PortCalculatedThreshold] = calculatedThresh
        };

        return ToolResult.Ok(outputs, message: $"Binarized using {method} (Thresh={calculatedThresh:F1})");
    }
}
