using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Overlays;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Blob;

/// <summary>
/// Extracts connected binary components, computes geometric features (area, perimeter, centroid, circularity, orientation),
/// filters by thresholds, and sorts candidates.
/// </summary>
[VisionTool("Blob.Analysis", "Blob Analysis", "Blob Analysis", Description = "Extracts and measures connected binary components with morphology metrics")]
public sealed class BlobAnalysisTool : VisionToolBase
{
    public const string ParamMinArea = "MinArea";
    public const string ParamMaxArea = "MaxArea";
    public const string ParamMinCircularity = "MinCircularity";
    public const string ParamMaxCircularity = "MaxCircularity";
    public const string ParamSortBy = "SortBy";
    public const string ParamMaxBlobs = "MaxBlobs";

    public const string PortInImage = "ImageIn";
    public const string PortBlobCount = "BlobCount";
    public const string PortPrimaryArea = "PrimaryArea";
    public const string PortPrimaryCenterX = "PrimaryCenterX";
    public const string PortPrimaryCenterY = "PrimaryCenterY";
    public const string PortLabeledImage = "LabeledImage";

    private readonly ToolParameter _pMinArea;
    private readonly ToolParameter _pMaxArea;
    private readonly ToolParameter _pMinCirc;
    private readonly ToolParameter _pMaxCirc;
    private readonly ToolParameter _pSortBy;
    private readonly ToolParameter _pMaxBlobs;

    public BlobAnalysisTool() : base("Blob.Analysis", "Blob Analysis", "Blob Analysis", "Measures connected blobs")
    {
        _pMinArea = AddParameter(new ToolParameter(ParamMinArea, "Min Area (px)", ParameterType.Double, 50.0, min: 0.0, max: 10000000.0));
        _pMaxArea = AddParameter(new ToolParameter(ParamMaxArea, "Max Area (px)", ParameterType.Double, 1000000.0, min: 1.0, max: 10000000.0));
        _pMinCirc = AddParameter(new ToolParameter(ParamMinCircularity, "Min Circularity", ParameterType.Double, 0.0, min: 0.0, max: 1.0, step: 0.05));
        _pMaxCirc = AddParameter(new ToolParameter(ParamMaxCircularity, "Max Circularity", ParameterType.Double, 1.0, min: 0.0, max: 1.0, step: 0.05));
        _pSortBy = AddParameter(new ToolParameter(
            ParamSortBy, "Sort By", ParameterType.Enum, "AreaDescending",
            enumOptions: new[] { "AreaDescending", "AreaAscending", "XAscending", "YAscending", "CircularityDescending" }));
        _pMaxBlobs = AddParameter(new ToolParameter(ParamMaxBlobs, "Max Blob Results", ParameterType.Integer, 50, min: 1, max: 1000));

        AddInput(new ToolPort(PortInImage, "Image In", PortType.Image, PortDirection.Input));

        AddOutput(new ToolPort(PortBlobCount, "Blob Count", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortPrimaryArea, "Primary Area", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortPrimaryCenterX, "Primary Center X", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortPrimaryCenterY, "Primary Center Y", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortLabeledImage, "Labeled Image", PortType.Image, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var inputFrame = ctx.GetImage(PortInImage);
        if (inputFrame == null) return ToolResult.Failed("Missing input image.");

        var src = inputFrame.Mat;
        using var binary = new Mat();

        if (src.Channels() > 1)
        {
            Cv2.CvtColor(src, binary, ColorConversionCodes.BGR2GRAY);
            Cv2.Threshold(binary, binary, 127, 255, ThresholdTypes.Binary);
        }
        else
        {
            Cv2.Threshold(src, binary, 127, 255, ThresholdTypes.Binary);
        }

        // Find contours
        Cv2.FindContours(binary, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        var minArea = _pMinArea.GetValue<double>();
        var maxArea = _pMaxArea.GetValue<double>();
        var minCirc = _pMinCirc.GetValue<double>();
        var maxCirc = _pMaxCirc.GetValue<double>();
        var sortBy = _pSortBy.GetValue<string>() ?? "AreaDescending";
        var maxBlobs = _pMaxBlobs.GetValue<int>();

        var rawList = new List<BlobFeature>();
        int idCounter = 1;

        foreach (var contour in contours)
        {
            var area = Cv2.ContourArea(contour);
            if (area < minArea || area > maxArea) continue;

            var perimeter = Cv2.ArcLength(contour, closed: true);
            var circularity = (perimeter > 0) ? (4.0 * Math.PI * area) / (perimeter * perimeter) : 0.0;
            circularity = Math.Clamp(circularity, 0.0, 1.0);

            if (circularity < minCirc || circularity > maxCirc) continue;

            var moments = Cv2.Moments(contour);
            var cx = moments.M00 > 0 ? (float)(moments.M10 / moments.M00) : 0f;
            var cy = moments.M00 > 0 ? (float)(moments.M01 / moments.M00) : 0f;

            var bRect = Cv2.BoundingRect(contour);
            var rRect = Cv2.MinAreaRect(contour);

            var aspectRatio = (bRect.Height > 0) ? (double)bRect.Width / bRect.Height : 1.0;
            var orientation = rRect.Angle;

            rawList.Add(new BlobFeature(
                Id: idCounter++,
                Area: area,
                Perimeter: perimeter,
                Centroid: new Point2f(cx, cy),
                BoundingBox: new Rect2f(bRect.X, bRect.Y, bRect.Width, bRect.Height),
                RotatedBox: rRect,
                Circularity: circularity,
                AspectRatio: aspectRatio,
                OrientationDegrees: orientation));
        }

        // Sorting
        var sorted = sortBy.ToLowerInvariant() switch
        {
            "areaascending" => rawList.OrderBy(b => b.Area).ToList(),
            "xascending" => rawList.OrderBy(b => b.Centroid.X).ToList(),
            "yascending" => rawList.OrderBy(b => b.Centroid.Y).ToList(),
            "circularitydescending" => rawList.OrderByDescending(b => b.Circularity).ToList(),
            _ => rawList.OrderByDescending(b => b.Area).ToList()
        };

        var selectedBlobs = sorted.Take(maxBlobs).ToList();

        // Create color labeled canvas
        var labeledMat = new Mat();
        if (src.Channels() == 1)
        {
            Cv2.CvtColor(src, labeledMat, ColorConversionCodes.GRAY2BGR);
        }
        else
        {
            src.CopyTo(labeledMat);
        }

        var overlays = new List<IOverlayPrimitive>();
        foreach (var b in selectedBlobs)
        {
            // Visual overlays
            overlays.Add(new RotatedRectOverlay(b.RotatedBox, new Scalar(0, 255, 0), Thickness: 2));
            overlays.Add(new CrossOverlay(b.Centroid, 12f, new Scalar(0, 0, 255), Thickness: 2));
            overlays.Add(new TextOverlay($"#{b.Id} A:{b.Area:F0}", new Point2f(b.BoundingBox.X, b.BoundingBox.Y - 4), new Scalar(255, 255, 0), FontScale: 0.5));

            // Also render onto labeled image canvas
            overlays[^3].Render(labeledMat);
            overlays[^2].Render(labeledMat);
            overlays[^1].Render(labeledMat);
        }

        var primaryBlob = selectedBlobs.FirstOrDefault();
        var outFrame = new ImageFrame(labeledMat, inputFrame.CameraId, inputFrame.FrameId, PixelFormat.Bgr24);

        var outputs = new Dictionary<string, object?>
        {
            [PortBlobCount] = selectedBlobs.Count,
            [PortPrimaryArea] = primaryBlob?.Area ?? 0.0,
            [PortPrimaryCenterX] = (double)(primaryBlob?.Centroid.X ?? 0f),
            [PortPrimaryCenterY] = (double)(primaryBlob?.Centroid.Y ?? 0f),
            [PortLabeledImage] = outFrame
        };

        return ToolResult.Ok(outputs, overlays, message: $"Detected {selectedBlobs.Count} qualified blobs");
    }
}
