using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Overlays;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Matching;

/// <summary>
/// Feature-based keypoint matching using ORB descriptors and RANSAC homography estimation.
/// Tolerates partial occlusion, scale variations, and affine perspective distortion.
/// </summary>
[VisionTool("Matching.FeatureMatch", "Feature Match (ORB + RANSAC)", "Matching and Localization", Description = "ORB keypoint matching with RANSAC homography perspective projection")]
public sealed class FeatureMatchTool : VisionToolBase
{
    public const string ParamMaxFeatures = "MaxFeatures";
    public const string ParamMinMatches = "MinInlierMatches";

    public const string PortInImage = "ImageIn";
    public const string PortTemplate = "TemplateIn";
    public const string PortInlierCount = "InlierCount";
    public const string PortPoseCenterX = "PoseCenterX";
    public const string PortPoseCenterY = "PoseCenterY";
    public const string PortAnnotatedImage = "AnnotatedImage";

    private readonly ToolParameter _pFeatures;
    private readonly ToolParameter _pMinMatches;

    public FeatureMatchTool() : base("Matching.FeatureMatch", "Feature Match (ORB + RANSAC)", "Matching and Localization", "ORB feature descriptor matching")
    {
        _pFeatures = AddParameter(new ToolParameter(ParamMaxFeatures, "Max Features", ParameterType.Integer, 500, min: 50, max: 5000));
        _pMinMatches = AddParameter(new ToolParameter(ParamMinMatches, "Min Inliers for Success", ParameterType.Integer, 8, min: 4, max: 200));

        AddInput(new ToolPort(PortInImage, "Image In", PortType.Image, PortDirection.Input));
        AddInput(new ToolPort(PortTemplate, "Template In", PortType.Image, PortDirection.Input));

        AddOutput(new ToolPort(PortInlierCount, "Inlier Count", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortPoseCenterX, "Pose Center X", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortPoseCenterY, "Pose Center Y", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortAnnotatedImage, "Annotated Image", PortType.Image, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var inputFrame = ctx.GetImage(PortInImage);
        var tplFrame = ctx.GetImage(PortTemplate);

        if (inputFrame == null) return ToolResult.Failed("Missing input image.");
        if (tplFrame == null) return ToolResult.Failed("Missing template image.");

        var src = inputFrame.Mat;
        var tpl = tplFrame.Mat;

        using var orb = ORB.Create(_pFeatures.GetValue<int>());

        using var descTpl = new Mat();
        using var descSrc = new Mat();

        orb.DetectAndCompute(tpl, null, out var kpTpl, descTpl);
        orb.DetectAndCompute(src, null, out var kpSrc, descSrc);

        if (kpTpl.Length < 4 || kpSrc.Length < 4 || descTpl.Empty() || descSrc.Empty())
        {
            return ToolResult.Warning("Insufficient keypoints extracted from template or scene.");
        }

        using var matcher = new BFMatcher(NormTypes.Hamming, crossCheck: true);
        var matches = matcher.Match(descTpl, descSrc);

        var sortedMatches = matches.OrderBy(m => m.Distance).Take(50).ToArray();
        var minMatches = _pMinMatches.GetValue<int>();

        var overlays = new List<IOverlayPrimitive>();
        var annotated = src.Clone();
        double poseX = 0, poseY = 0;
        int inlierCount = 0;

        if (sortedMatches.Length >= 4)
        {
            var ptsTpl = sortedMatches.Select(m => new Point2d(kpTpl[m.QueryIdx].Pt.X, kpTpl[m.QueryIdx].Pt.Y)).ToArray();
            var ptsSrc = sortedMatches.Select(m => new Point2d(kpSrc[m.TrainIdx].Pt.X, kpSrc[m.TrainIdx].Pt.Y)).ToArray();

            using var inlierMask = new Mat();
            using var h = Cv2.FindHomography(ptsTpl, ptsSrc, HomographyMethods.Ransac, 3.0, inlierMask);

            if (!h.Empty())
            {
                inlierCount = Cv2.CountNonZero(inlierMask);

                // Project template 4 corners onto scene
                var tplCorners = new[]
                {
                    new Point2f(0, 0),
                    new Point2f(tpl.Cols, 0),
                    new Point2f(tpl.Cols, tpl.Rows),
                    new Point2f(0, tpl.Rows)
                };

                var projectedCorners = Cv2.PerspectiveTransform(tplCorners, h);
                poseX = projectedCorners.Average(p => p.X);
                poseY = projectedCorners.Average(p => p.Y);

                var polyOv = new PolygonOverlay(projectedCorners, new Scalar(0, 255, 0), Thickness: 2);
                var centerOv = new CrossOverlay(new Point2f((float)poseX, (float)poseY), 14f, new Scalar(0, 0, 255), Thickness: 2);
                var textOv = new TextOverlay($"ORB Inliers: {inlierCount}", new Point2f(projectedCorners[0].X, projectedCorners[0].Y - 8), new Scalar(0, 255, 0), FontScale: 0.5);

                overlays.Add(polyOv);
                overlays.Add(centerOv);
                overlays.Add(textOv);

                polyOv.Render(annotated);
                centerOv.Render(annotated);
                textOv.Render(annotated);
            }
        }

        var isFound = inlierCount >= minMatches;
        var outFrame = new ImageFrame(annotated, inputFrame.CameraId, inputFrame.FrameId, inputFrame.Format);
        var outputs = new Dictionary<string, object?>
        {
            [PortInlierCount] = inlierCount,
            [PortPoseCenterX] = poseX,
            [PortPoseCenterY] = poseY,
            [PortAnnotatedImage] = outFrame
        };

        return ToolResult.Ok(outputs, overlays, message: $"ORB Feature Match: {inlierCount} inliers (Target ≥ {minMatches})");
    }
}
