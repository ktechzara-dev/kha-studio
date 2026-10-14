using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Overlays;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Stitching;

/// <summary>
/// Image Stitching Tool merging two overlapping camera views or line-scan swaths into a panoramic image.
/// </summary>
[VisionTool("Stitching.ImageStitch", "Image Stitching (Panoramic / Tiled)", "Geometry", Description = "Merges overlapping image pairs into a single stitched composite image via ORB feature matching")]
public sealed class ImageStitchingTool : VisionToolBase
{
    public const string PortInImageA = "ImageA";
    public const string PortInImageB = "ImageB";
    public const string PortOutStitched = "StitchedImage";
    public const string PortInlierCount = "InlierCount";
    public const string PortSuccess = "Success";

    public const string ParamMaxFeatures = "MaxFeatures";

    private readonly ToolParameter _pMaxFeatures;

    public ImageStitchingTool() : base("Stitching.ImageStitch", "Image Stitching (Panoramic / Tiled)", "Geometry", "Merges overlapping image pairs into a single stitched composite image via ORB feature matching")
    {
        _pMaxFeatures = AddParameter(new ToolParameter(ParamMaxFeatures, "Max Features", ParameterType.Integer, 500, min: 50, max: 5000));

        AddInput(new ToolPort(PortInImageA, "Primary Reference Image (A)", PortType.Image, PortDirection.Input));
        AddInput(new ToolPort(PortInImageB, "Overlapping Image (B)", PortType.Image, PortDirection.Input));
        AddOutput(new ToolPort(PortOutStitched, "Stitched Composite Image", PortType.Image, PortDirection.Output));
        AddOutput(new ToolPort(PortInlierCount, "Feature Inlier Count", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortSuccess, "Stitch Success (Pass/Fail)", PortType.Boolean, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext context, CancellationToken ct)
    {
        var frameA = context.GetImage(PortInImageA);
        var frameB = context.GetImage(PortInImageB);

        if (frameA == null || frameB == null || frameA.Mat.Empty() || frameB.Mat.Empty())
            return ToolResult.Failed("One or both input images are missing or empty.");

        int maxFeatures = _pMaxFeatures.GetValue<int>();

        var result = ImageStitcher.StitchPair(frameA.Mat, frameB.Mat, maxFeatures);

        var overlays = new List<IOverlayPrimitive>
        {
            new TextOverlay($"Stitched: {(result.Success ? "HOMOGRAPHY ALIGNED" : "CONCAT FALLBACK")} | Inliers: {result.InlierCount}",
                new Point2f(10, 25), result.Success ? new Scalar(0, 255, 100) : new Scalar(0, 170, 255), FontScale: 0.5)
        };

        var outputs = new Dictionary<string, object?>
        {
            [PortOutStitched] = new ImageFrame(result.StitchedMat, frameA.CameraId, frameA.FrameId),
            [PortInlierCount] = (double)result.InlierCount,
            [PortSuccess] = result.Success
        };

        return ToolResult.Ok(outputs, overlays, message: $"Stitch: {(result.Success ? "Aligned" : "Fallback")} (Inliers={result.InlierCount})");
    }
}
