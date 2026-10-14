using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Color;

/// <summary>
/// Segments chromatic regions by filtering pixels within lower and upper HSV/BGR bounds.
/// </summary>
[VisionTool("Color.ColorThreshold", "Color Threshold", "Color / Intensity", Description = "Filters pixels within lower and upper HSV/BGR ranges")]
public sealed class ColorThresholdTool : VisionToolBase
{
    public const string ParamSpace = "ColorSpace";
    public const string ParamMinCh1 = "MinCh1";
    public const string ParamMaxCh1 = "MaxCh1";
    public const string ParamMinCh2 = "MinCh2";
    public const string ParamMaxCh2 = "MaxCh2";
    public const string ParamMinCh3 = "MinCh3";
    public const string ParamMaxCh3 = "MaxCh3";

    public const string PortInImage = "ImageIn";
    public const string PortMask = "Mask";
    public const string PortPixelCount = "PixelCount";
    public const string PortAreaFraction = "AreaFraction";

    private readonly ToolParameter _pSpace;
    private readonly ToolParameter _pMin1;
    private readonly ToolParameter _pMax1;
    private readonly ToolParameter _pMin2;
    private readonly ToolParameter _pMax2;
    private readonly ToolParameter _pMin3;
    private readonly ToolParameter _pMax3;

    public ColorThresholdTool() : base("Color.ColorThreshold", "Color Threshold", "Color / Intensity", "Color range filter")
    {
        _pSpace = AddParameter(new ToolParameter(ParamSpace, "Color Space", ParameterType.Enum, "HSV", enumOptions: new[] { "HSV", "BGR" }));

        // Default: Green in HSV (H: 35-85, S: 50-255, V: 50-255)
        _pMin1 = AddParameter(new ToolParameter(ParamMinCh1, "Min Ch1 (H / B)", ParameterType.Integer, 35, min: 0, max: 255));
        _pMax1 = AddParameter(new ToolParameter(ParamMaxCh1, "Max Ch1 (H / B)", ParameterType.Integer, 85, min: 0, max: 255));
        _pMin2 = AddParameter(new ToolParameter(ParamMinCh2, "Min Ch2 (S / G)", ParameterType.Integer, 50, min: 0, max: 255));
        _pMax2 = AddParameter(new ToolParameter(ParamMaxCh2, "Max Ch2 (S / G)", ParameterType.Integer, 255, min: 0, max: 255));
        _pMin3 = AddParameter(new ToolParameter(ParamMinCh3, "Min Ch3 (V / R)", ParameterType.Integer, 50, min: 0, max: 255));
        _pMax3 = AddParameter(new ToolParameter(ParamMaxCh3, "Max Ch3 (V / R)", ParameterType.Integer, 255, min: 0, max: 255));

        AddInput(new ToolPort(PortInImage, "Image In", PortType.Image, PortDirection.Input));
        AddOutput(new ToolPort(PortMask, "Mask", PortType.Image, PortDirection.Output));
        AddOutput(new ToolPort(PortPixelCount, "Pixel Count", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortAreaFraction, "Area Fraction (%)", PortType.Number, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var inputFrame = ctx.GetImage(PortInImage);
        if (inputFrame == null) return ToolResult.Failed("Missing input image.");

        var src = inputFrame.Mat;
        if (src.Channels() < 3) return ToolResult.Failed("Color threshold requires 3 color channels.");

        var space = _pSpace.GetValue<string>() ?? "HSV";
        using var targetMat = new Mat();

        if (space.Equals("HSV", StringComparison.OrdinalIgnoreCase))
        {
            Cv2.CvtColor(src, targetMat, ColorConversionCodes.BGR2HSV);
        }
        else
        {
            src.CopyTo(targetMat);
        }

        var lower = new Scalar(_pMin1.GetValue<int>(), _pMin2.GetValue<int>(), _pMin3.GetValue<int>());
        var upper = new Scalar(_pMax1.GetValue<int>(), _pMax2.GetValue<int>(), _pMax3.GetValue<int>());

        var mask = new Mat();
        Cv2.InRange(targetMat, lower, upper, mask);

        var matchCount = Cv2.CountNonZero(mask);
        var totalPixels = mask.Rows * mask.Cols;
        var fraction = totalPixels > 0 ? (matchCount * 100.0) / totalPixels : 0.0;

        var maskFrame = new ImageFrame(mask, inputFrame.CameraId, inputFrame.FrameId, PixelFormat.Mono8);

        var outputs = new Dictionary<string, object?>
        {
            [PortMask] = maskFrame,
            [PortPixelCount] = matchCount,
            [PortAreaFraction] = fraction
        };

        return ToolResult.Ok(outputs, message: $"Matched {matchCount} pixels ({fraction:F2}%) in {space}");
    }
}
