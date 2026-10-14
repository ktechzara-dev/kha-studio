using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Segmentation;

/// <summary>
/// Separates touching or clustered objects (particles, pills, overlapping parts) using distance-transform watershed segmentation.
/// </summary>
[VisionTool("Segmentation.Watershed", "Watershed Segmentation", "Segmentation", Description = "Separates touching objects using marker-controlled watershed")]
public sealed class WatershedTool : VisionToolBase
{
    public const string ParamDistRatio = "DistanceThresholdRatio";
    public const string ParamInvert = "InvertThreshold";

    public const string PortInImage = "ImageIn";
    public const string PortSegmented = "SegmentedImage";
    public const string PortObjectCount = "ObjectCount";

    private readonly ToolParameter _pDistRatio;
    private readonly ToolParameter _pInvert;

    public WatershedTool() : base("Segmentation.Watershed", "Watershed Segmentation", "Segmentation", "Separates touching objects")
    {
        _pDistRatio = AddParameter(new ToolParameter(ParamDistRatio, "Distance Peak Ratio", ParameterType.Double, 0.4, min: 0.05, max: 0.95));
        _pInvert = AddParameter(new ToolParameter(ParamInvert, "Dark Objects on Light Background", ParameterType.Boolean, false));

        AddInput(new ToolPort(PortInImage, "Image In", PortType.Image, PortDirection.Input));
        AddOutput(new ToolPort(PortSegmented, "Segmented Image", PortType.Image, PortDirection.Output));
        AddOutput(new ToolPort(PortObjectCount, "Object Count", PortType.Number, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var inputFrame = ctx.GetImage(PortInImage);
        if (inputFrame == null) return ToolResult.Failed("Missing input image.");

        var src = inputFrame.Mat;
        using var colorSrc = new Mat();
        using var gray = new Mat();

        if (src.Channels() == 1)
        {
            src.CopyTo(gray);
            Cv2.CvtColor(src, colorSrc, ColorConversionCodes.GRAY2BGR);
        }
        else
        {
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
            src.CopyTo(colorSrc);
        }

        // 1. Otsu threshold
        var invert = _pInvert.GetValue<bool>();
        var threshType = (invert ? ThresholdTypes.BinaryInv : ThresholdTypes.Binary) | ThresholdTypes.Otsu;
        using var thresh = new Mat();
        Cv2.Threshold(gray, thresh, 0, 255, threshType);

        // 2. Remove noise via opening
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
        using var opening = new Mat();
        Cv2.MorphologyEx(thresh, opening, MorphTypes.Open, kernel, iterations: 2);

        // 3. Sure background via dilation
        using var sureBg = new Mat();
        Cv2.Dilate(opening, sureBg, kernel, iterations: 3);

        // 4. Distance transform for sure foreground
        using var dist = new Mat();
        Cv2.DistanceTransform(opening, dist, DistanceTypes.L2, DistanceTransformMasks.Mask5);
        Cv2.MinMaxLoc(dist, out _, out double maxVal);

        var ratio = _pDistRatio.GetValue<double>();
        using var sureFg = new Mat();
        Cv2.Threshold(dist, sureFg, maxVal * ratio, 255, ThresholdTypes.Binary);
        using var sureFg8U = new Mat();
        sureFg.ConvertTo(sureFg8U, MatType.CV_8U);

        // 5. Unknown region
        using var unknown = new Mat();
        Cv2.Subtract(sureBg, sureFg8U, unknown);

        // 6. Connected components markers
        using var markers = new Mat();
        var numComponents = Cv2.ConnectedComponents(sureFg8U, markers);

        // Add 1 to all labels so background is 1 instead of 0
        markers.ConvertTo(markers, MatType.CV_32S);
        Cv2.Add(markers, Scalar.All(1), markers);

        // Mark unknown region with 0
        for (int r = 0; r < markers.Rows; r++)
        {
            for (int c = 0; c < markers.Cols; c++)
            {
                if (unknown.Get<byte>(r, c) == 255)
                {
                    markers.Set(r, c, 0);
                }
            }
        }

        // 7. Apply watershed
        Cv2.Watershed(colorSrc, markers);

        // 8. Colorize result
        var dst = colorSrc.Clone();
        // Draw watershed boundaries (-1) in red
        for (int r = 0; r < markers.Rows; r++)
        {
            for (int c = 0; c < markers.Cols; c++)
            {
                if (markers.Get<int>(r, c) == -1)
                {
                    dst.Set(r, c, new Vec3b(0, 0, 255));
                }
            }
        }

        var objectCount = Math.Max(0, numComponents - 1);
        var outFrame = new ImageFrame(dst, inputFrame.CameraId, inputFrame.FrameId, PixelFormat.Bgr24);

        var outputs = new Dictionary<string, object?>
        {
            [PortSegmented] = outFrame,
            [PortObjectCount] = objectCount
        };

        return ToolResult.Ok(outputs, message: $"Watershed detected {objectCount} isolated objects");
    }
}
