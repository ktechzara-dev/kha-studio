using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Overlays;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Identification;

/// <summary>
/// Presence/Absence inspection detecting component existence via pixel area and intensity threshold counting.
/// </summary>
[VisionTool("Identification.PresenceAbsence", "Presence / Absence Check", "Identification", Description = "Verifies component presence or absence by pixel count")]
public sealed class PresenceAbsenceTool : VisionToolBase
{
    public const string ParamMinPixels = "MinPixels";
    public const string ParamMaxPixels = "MaxPixels";
    public const string ParamInvert = "DetectDarkPixels";

    public const string PortInImage = "ImageIn";
    public const string PortIsPresent = "IsPresent";
    public const string PortPixelCount = "Count";
    public const string PortAnnotatedImage = "AnnotatedImage";

    private readonly ToolParameter _pMin;
    private readonly ToolParameter _pMax;
    private readonly ToolParameter _pInvert;

    public PresenceAbsenceTool() : base("Identification.PresenceAbsence", "Presence / Absence Check", "Identification", "Verifies presence of features")
    {
        _pMin = AddParameter(new ToolParameter(ParamMinPixels, "Min Pixels for Presence", ParameterType.Integer, 200, min: 1, max: 10000000));
        _pMax = AddParameter(new ToolParameter(ParamMaxPixels, "Max Pixels (Limit)", ParameterType.Integer, 1000000, min: 1, max: 10000000));
        _pInvert = AddParameter(new ToolParameter(ParamInvert, "Count Dark Pixels Instead of Bright", ParameterType.Boolean, false));

        AddInput(new ToolPort(PortInImage, "Image In", PortType.Image, PortDirection.Input));

        AddOutput(new ToolPort(PortIsPresent, "Is Present", PortType.Boolean, PortDirection.Output));
        AddOutput(new ToolPort(PortPixelCount, "Pixel Count", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortAnnotatedImage, "Annotated Image", PortType.Image, PortDirection.Output));
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

        var isDark = _pInvert.GetValue<bool>();
        var threshType = isDark ? ThresholdTypes.BinaryInv : ThresholdTypes.Binary;

        using var binary = new Mat();
        Cv2.Threshold(gray, binary, 127, 255, threshType);

        var count = Cv2.CountNonZero(binary);
        var min = _pMin.GetValue<int>();
        var max = _pMax.GetValue<int>();

        var isPresent = count >= min && count <= max;

        var annotated = src.Clone();
        var color = isPresent ? new Scalar(0, 255, 0) : new Scalar(0, 0, 255);
        var border = new RectOverlay(new Rect2f(4, 4, src.Cols - 8, src.Rows - 8), color, Thickness: 3);
        var label = new TextOverlay(isPresent ? $"PRESENT ({count} px)" : $"ABSENT ({count} px)", new Point2f(12, 30), color, FontScale: 0.8, Thickness: 2);

        var overlays = new List<IOverlayPrimitive> { border, label };
        border.Render(annotated);
        label.Render(annotated);

        var outFrame = new ImageFrame(annotated, inputFrame.CameraId, inputFrame.FrameId, inputFrame.Format);
        var outputs = new Dictionary<string, object?>
        {
            [PortIsPresent] = isPresent,
            [PortPixelCount] = count,
            [PortAnnotatedImage] = outFrame
        };

        return ToolResult.Ok(outputs, overlays, message: $"Presence check: {(isPresent ? "PRESENT" : "ABSENT")} ({count} px in [{min}, {max}])");
    }
}
