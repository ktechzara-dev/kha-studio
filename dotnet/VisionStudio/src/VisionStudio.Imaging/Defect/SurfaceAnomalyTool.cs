using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Overlays;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Defect;

/// <summary>
/// Detects local surface stains, pits, and particulate anomalies via background estimation and spatial deviation.
/// Does not require a golden reference image.
/// </summary>
[VisionTool("Defect.SurfaceAnomaly", "Surface Anomaly Detection", "Defect Detection", Description = "Detects local surface stains, pits, and particles without reference")]
public sealed class SurfaceAnomalyTool : VisionToolBase
{
    public const string ParamBgRadius = "BackgroundBlurRadius";
    public const string ParamSensitivity = "SensitivityThreshold";
    public const string ParamMinArea = "MinAnomalyArea";

    public const string PortInImage = "ImageIn";
    public const string PortAnomalyCount = "AnomalyCount";
    public const string PortAnomalyArea = "TotalAnomalyArea";
    public const string PortAnnotatedImage = "AnnotatedImage";

    private readonly ToolParameter _pRadius;
    private readonly ToolParameter _pSens;
    private readonly ToolParameter _pMinArea;

    public SurfaceAnomalyTool() : base("Defect.SurfaceAnomaly", "Surface Anomaly Detection", "Defect Detection", "Autonomous surface inspection")
    {
        _pRadius = AddParameter(new ToolParameter(ParamBgRadius, "Background Blur Radius", ParameterType.Integer, 25, min: 5, max: 151, step: 2));
        _pSens = AddParameter(new ToolParameter(ParamSensitivity, "Deviation Threshold", ParameterType.Double, 20.0, min: 2.0, max: 150.0));
        _pMinArea = AddParameter(new ToolParameter(ParamMinArea, "Min Anomaly Area (px)", ParameterType.Integer, 15, min: 1, max: 10000));

        AddInput(new ToolPort(PortInImage, "Image In", PortType.Image, PortDirection.Input));

        AddOutput(new ToolPort(PortAnomalyCount, "Anomaly Count", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortAnomalyArea, "Total Anomaly Area", PortType.Number, PortDirection.Output));
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

        var r = _pRadius.GetValue<int>();
        if (r % 2 == 0) r += 1;
        r = Math.Clamp(r, 5, 151);

        // Estimate background illumination
        using var bg = new Mat();
        Cv2.Blur(gray, bg, new Size(r, r));

        using var diff = new Mat();
        Cv2.Absdiff(gray, bg, diff);

        var thresh = _pSens.GetValue<double>();
        using var mask = new Mat();
        Cv2.Threshold(diff, mask, thresh, 255, ThresholdTypes.Binary);

        var minArea = _pMinArea.GetValue<int>();
        Cv2.FindContours(mask, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        var validAnomalies = contours.Where(c => Cv2.ContourArea(c) >= minArea).ToList();
        var totalArea = validAnomalies.Sum(c => Cv2.ContourArea(c));

        var annotated = src.Clone();
        var overlays = new List<IOverlayPrimitive>();

        foreach (var c in validAnomalies)
        {
            var rect = Cv2.BoundingRect(c);
            var rOv = new RectOverlay(new Rect2f(rect.X, rect.Y, rect.Width, rect.Height), new Scalar(0, 0, 255), Thickness: 2);
            overlays.Add(rOv);
            rOv.Render(annotated);
        }

        var label = new TextOverlay($"Anomalies: {validAnomalies.Count} (Area: {totalArea:F0}px)", new Point2f(10, 25), validAnomalies.Count > 0 ? new Scalar(0, 0, 255) : new Scalar(0, 255, 0), FontScale: 0.6, Thickness: 2);
        overlays.Add(label);
        label.Render(annotated);

        var outFrame = new ImageFrame(annotated, inputFrame.CameraId, inputFrame.FrameId, inputFrame.Format);
        var outputs = new Dictionary<string, object?>
        {
            [PortAnomalyCount] = validAnomalies.Count,
            [PortAnomalyArea] = totalArea,
            [PortAnnotatedImage] = outFrame
        };

        return ToolResult.Ok(outputs, overlays, message: $"Detected {validAnomalies.Count} surface anomalies (Total Area: {totalArea:F0} px)");
    }
}
