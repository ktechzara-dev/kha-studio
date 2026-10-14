using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Overlays;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Defect;

/// <summary>
/// Golden template comparison subtracting live part against golden reference to isolate surface anomalies.
/// </summary>
[VisionTool("Defect.GoldenDiff", "Golden Template Difference", "Defect Detection", Description = "Isolates defects by subtracting live image from golden reference")]
public sealed class GoldenTemplateDiffTool : VisionToolBase
{
    public const string ParamTolerance = "Tolerance";
    public const string ParamMinDefectArea = "MinDefectArea";

    public const string PortLive = "LiveImage";
    public const string PortGolden = "GoldenImage";
    public const string PortDefectMask = "DefectMask";
    public const string PortDefectArea = "DefectArea";
    public const string PortAnnotatedImage = "AnnotatedImage";

    private readonly ToolParameter _pTol;
    private readonly ToolParameter _pMinArea;

    public GoldenTemplateDiffTool() : base("Defect.GoldenDiff", "Golden Template Difference", "Defect Detection", "Golden template inspection")
    {
        _pTol = AddParameter(new ToolParameter(ParamTolerance, "Intensity Tolerance", ParameterType.Double, 25.0, min: 1.0, max: 255.0));
        _pMinArea = AddParameter(new ToolParameter(ParamMinDefectArea, "Min Defect Area (px)", ParameterType.Integer, 20, min: 1, max: 50000));

        AddInput(new ToolPort(PortLive, "Live Image", PortType.Image, PortDirection.Input));
        AddInput(new ToolPort(PortGolden, "Golden Reference", PortType.Image, PortDirection.Input));

        AddOutput(new ToolPort(PortDefectMask, "Defect Mask", PortType.Image, PortDirection.Output));
        AddOutput(new ToolPort(PortDefectArea, "Defect Area (px)", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortAnnotatedImage, "Annotated Image", PortType.Image, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var liveFrame = ctx.GetImage(PortLive);
        var goldenFrame = ctx.GetImage(PortGolden);

        if (liveFrame == null) return ToolResult.Failed("Missing live inspection image.");
        if (goldenFrame == null) return ToolResult.Failed("Missing golden template image.");

        var live = liveFrame.Mat;
        var golden = goldenFrame.Mat;

        using var liveGray = new Mat();
        using var goldenGray = new Mat();

        if (live.Channels() > 1) Cv2.CvtColor(live, liveGray, ColorConversionCodes.BGR2GRAY);
        else live.CopyTo(liveGray);

        if (golden.Channels() > 1) Cv2.CvtColor(golden, goldenGray, ColorConversionCodes.BGR2GRAY);
        else golden.CopyTo(goldenGray);

        if (liveGray.Size() != goldenGray.Size())
        {
            return ToolResult.Failed("Dimension mismatch between live and golden reference images.");
        }

        // Absolute difference: |Live - Golden|
        using var diff = new Mat();
        Cv2.Absdiff(liveGray, goldenGray, diff);

        var tol = _pTol.GetValue<double>();
        using var rawMask = new Mat();
        Cv2.Threshold(diff, rawMask, tol, 255, ThresholdTypes.Binary);

        // Remove single-pixel salt noise via opening
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
        var defectMask = new Mat();
        Cv2.MorphologyEx(rawMask, defectMask, MorphTypes.Open, kernel);

        var defectArea = Cv2.CountNonZero(defectMask);
        var minDefectArea = _pMinArea.GetValue<int>();
        var hasDefect = defectArea >= minDefectArea;

        var annotated = live.Clone();
        var overlays = new List<IOverlayPrimitive>();

        if (hasDefect)
        {
            Cv2.FindContours(defectMask, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
            foreach (var c in contours)
            {
                if (Cv2.ContourArea(c) >= minDefectArea)
                {
                    var rect = Cv2.BoundingRect(c);
                    var rOv = new RectOverlay(new Rect2f(rect.X, rect.Y, rect.Width, rect.Height), new Scalar(0, 0, 255), Thickness: 2);
                    overlays.Add(rOv);
                    rOv.Render(annotated);
                }
            }

            var textOv = new TextOverlay($"DEFECT ({defectArea} px)", new Point2f(10, 30), new Scalar(0, 0, 255), FontScale: 0.8, Thickness: 2);
            overlays.Add(textOv);
            textOv.Render(annotated);
        }
        else
        {
            var textOv = new TextOverlay("OK (No Defects)", new Point2f(10, 30), new Scalar(0, 255, 0), FontScale: 0.8, Thickness: 2);
            overlays.Add(textOv);
            textOv.Render(annotated);
        }

        var maskFrame = new ImageFrame(defectMask, liveFrame.CameraId, liveFrame.FrameId, PixelFormat.Mono8);
        var outFrame = new ImageFrame(annotated, liveFrame.CameraId, liveFrame.FrameId, liveFrame.Format);

        var outputs = new Dictionary<string, object?>
        {
            [PortDefectMask] = maskFrame,
            [PortDefectArea] = defectArea,
            [PortAnnotatedImage] = outFrame
        };

        return ToolResult.Ok(outputs, overlays, message: $"Golden diff: {defectArea} defect pixels ({(hasDefect ? "DEFECTIVE" : "CLEAN")})");
    }
}
