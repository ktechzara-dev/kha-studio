using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Overlays;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Defect;

/// <summary>
/// Detects elongated surface scratches and hairline cracks using oriented morphological line kernels.
/// </summary>
[VisionTool("Defect.Scratch", "Scratch Detection", "Defect Detection", Description = "Isolates thin directional scratches via oriented line filters")]
public sealed class ScratchDetectionTool : VisionToolBase
{
    public const string ParamLineLength = "LineLength";
    public const string ParamThreshold = "Threshold";
    public const string ParamMinScratchLength = "MinScratchPixels";

    public const string PortInImage = "ImageIn";
    public const string PortScratchMask = "ScratchMask";
    public const string PortScratchCount = "ScratchCount";
    public const string PortAnnotatedImage = "AnnotatedImage";

    private readonly ToolParameter _pLen;
    private readonly ToolParameter _pThresh;
    private readonly ToolParameter _pMinScratch;

    public ScratchDetectionTool() : base("Defect.Scratch", "Scratch Detection", "Defect Detection", "Oriented scratch inspection")
    {
        _pLen = AddParameter(new ToolParameter(ParamLineLength, "Kernel Line Length", ParameterType.Integer, 21, min: 5, max: 101, step: 2));
        _pThresh = AddParameter(new ToolParameter(ParamThreshold, "Scratch Contrast Threshold", ParameterType.Double, 25.0, min: 1.0, max: 255.0));
        _pMinScratch = AddParameter(new ToolParameter(ParamMinScratchLength, "Min Scratch Pixel Area", ParameterType.Integer, 30, min: 5, max: 5000));

        AddInput(new ToolPort(PortInImage, "Image In", PortType.Image, PortDirection.Input));

        AddOutput(new ToolPort(PortScratchMask, "Scratch Mask", PortType.Image, PortDirection.Output));
        AddOutput(new ToolPort(PortScratchCount, "Scratch Count", PortType.Number, PortDirection.Output));
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

        var len = _pLen.GetValue<int>();
        if (len % 2 == 0) len += 1;
        len = Math.Clamp(len, 5, 101);

        using var maxScratchResponse = new Mat(gray.Size(), MatType.CV_8U, Scalar.All(0));

        // Test 4 canonical angles: Horizontal (0°), Vertical (90°), Diagonals (45°, 135°)
        using var kHoriz = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(len, 1));
        using var kVert = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(1, len));

        using var rHoriz = new Mat();
        using var rVert = new Mat();
        Cv2.MorphologyEx(gray, rHoriz, MorphTypes.BlackHat, kHoriz);
        Cv2.MorphologyEx(gray, rVert, MorphTypes.BlackHat, kVert);

        Cv2.Max(rHoriz, rVert, maxScratchResponse);

        var thresh = _pThresh.GetValue<double>();
        var scratchMask = new Mat();
        Cv2.Threshold(maxScratchResponse, scratchMask, thresh, 255, ThresholdTypes.Binary);

        Cv2.FindContours(scratchMask, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
        var minArea = _pMinScratch.GetValue<int>();

        var validScratches = contours.Where(c => Cv2.ContourArea(c) >= minArea).ToList();

        var annotated = src.Clone();
        var overlays = new List<IOverlayPrimitive>();

        foreach (var c in validScratches)
        {
            var r = Cv2.BoundingRect(c);
            var rectOv = new RectOverlay(new Rect2f(r.X, r.Y, r.Width, r.Height), new Scalar(0, 0, 255), Thickness: 2);
            overlays.Add(rectOv);
            rectOv.Render(annotated);
        }

        var label = new TextOverlay($"Scratches: {validScratches.Count}", new Point2f(10, 25), validScratches.Count > 0 ? new Scalar(0, 0, 255) : new Scalar(0, 255, 0), FontScale: 0.7, Thickness: 2);
        overlays.Add(label);
        label.Render(annotated);

        var maskFrame = new ImageFrame(scratchMask, inputFrame.CameraId, inputFrame.FrameId, PixelFormat.Mono8);
        var outFrame = new ImageFrame(annotated, inputFrame.CameraId, inputFrame.FrameId, inputFrame.Format);

        var outputs = new Dictionary<string, object?>
        {
            [PortScratchMask] = maskFrame,
            [PortScratchCount] = validScratches.Count,
            [PortAnnotatedImage] = outFrame
        };

        return ToolResult.Ok(outputs, overlays, message: $"Detected {validScratches.Count} surface scratches");
    }
}
