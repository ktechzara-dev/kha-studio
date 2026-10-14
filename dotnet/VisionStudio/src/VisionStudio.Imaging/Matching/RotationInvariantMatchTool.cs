using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Overlays;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Matching;

/// <summary>
/// Rotation-invariant template localization searching across angular intervals.
/// Outputs full 2D pose: X, Y, Angle, and Score.
/// </summary>
[VisionTool("Matching.RotationInvariant", "Rotation-Invariant Match", "Matching and Localization", Description = "Multi-angle template search yielding 2D pose (X, Y, Angle, Score)")]
public sealed class RotationInvariantMatchTool : VisionToolBase
{
    public const string ParamMinAngle = "MinAngle";
    public const string ParamMaxAngle = "MaxAngle";
    public const string ParamStepAngle = "AngleStep";
    public const string ParamMinScore = "MinScore";

    public const string PortInImage = "ImageIn";
    public const string PortTemplate = "TemplateIn";
    public const string PortBestScore = "BestScore";
    public const string PortPoseX = "PoseX";
    public const string PortPoseY = "PoseY";
    public const string PortPoseAngle = "PoseAngle";
    public const string PortAnnotatedImage = "AnnotatedImage";

    private readonly ToolParameter _pMinAngle;
    private readonly ToolParameter _pMaxAngle;
    private readonly ToolParameter _pStepAngle;
    private readonly ToolParameter _pMinScore;

    public RotationInvariantMatchTool() : base("Matching.RotationInvariant", "Rotation-Invariant Match", "Matching and Localization", "Rotation-invariant NCC")
    {
        _pMinAngle = AddParameter(new ToolParameter(ParamMinAngle, "Min Angle (deg)", ParameterType.Double, -45.0, min: -180.0, max: 180.0, unit: "deg"));
        _pMaxAngle = AddParameter(new ToolParameter(ParamMaxAngle, "Max Angle (deg)", ParameterType.Double, 45.0, min: -180.0, max: 180.0, unit: "deg"));
        _pStepAngle = AddParameter(new ToolParameter(ParamStepAngle, "Angle Step (deg)", ParameterType.Double, 5.0, min: 0.5, max: 45.0, unit: "deg"));
        _pMinScore = AddParameter(new ToolParameter(ParamMinScore, "Min Score", ParameterType.Double, 0.70, min: 0.1, max: 1.0));

        AddInput(new ToolPort(PortInImage, "Image In", PortType.Image, PortDirection.Input));
        AddInput(new ToolPort(PortTemplate, "Template In", PortType.Image, PortDirection.Input));

        AddOutput(new ToolPort(PortBestScore, "Best Score", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortPoseX, "Pose X", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortPoseY, "Pose Y", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortPoseAngle, "Pose Angle (deg)", PortType.Number, PortDirection.Output));
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

        using var graySrc = new Mat();
        using var grayTpl = new Mat();

        if (src.Channels() > 1) Cv2.CvtColor(src, graySrc, ColorConversionCodes.BGR2GRAY);
        else src.CopyTo(graySrc);

        if (tpl.Channels() > 1) Cv2.CvtColor(tpl, grayTpl, ColorConversionCodes.BGR2GRAY);
        else tpl.CopyTo(grayTpl);

        var minA = _pMinAngle.GetValue<double>();
        var maxA = _pMaxAngle.GetValue<double>();
        var stepA = Math.Max(0.5, _pStepAngle.GetValue<double>());
        var minScore = _pMinScore.GetValue<double>();

        double bestScore = -1.0;
        double bestX = 0, bestY = 0, bestAngle = 0;
        Size bestTplSize = tpl.Size();

        var tplCenter = new Point2f(grayTpl.Cols / 2.0f, grayTpl.Rows / 2.0f);

        for (double angle = minA; angle <= maxA; angle += stepA)
        {
            if (ct.IsCancellationRequested) break;

            using var rotMat = Cv2.GetRotationMatrix2D(tplCenter, angle, 1.0);
            using var rotatedTpl = new Mat();
            Cv2.WarpAffine(grayTpl, rotatedTpl, rotMat, grayTpl.Size(), InterpolationFlags.Linear, BorderTypes.Constant, Scalar.All(0));

            if (rotatedTpl.Cols > graySrc.Cols || rotatedTpl.Rows > graySrc.Rows) continue;

            using var matchRes = new Mat();
            Cv2.MatchTemplate(graySrc, rotatedTpl, matchRes, TemplateMatchModes.CCoeffNormed);

            Cv2.MinMaxLoc(matchRes, out _, out double maxVal, out _, out Point maxLoc);

            if (maxVal > bestScore)
            {
                bestScore = maxVal;
                bestAngle = angle;
                bestX = maxLoc.X + rotatedTpl.Cols / 2.0;
                bestY = maxLoc.Y + rotatedTpl.Rows / 2.0;
                bestTplSize = rotatedTpl.Size();
            }
        }

        var isFound = bestScore >= minScore;
        var annotated = src.Clone();
        var overlays = new List<IOverlayPrimitive>();

        if (isFound)
        {
            var rr = new RotatedRect(new Point2f((float)bestX, (float)bestY), new Size2f(bestTplSize.Width, bestTplSize.Height), (float)bestAngle);
            var rectOv = new RotatedRectOverlay(rr, new Scalar(0, 255, 0), Thickness: 2);
            var crossOv = new CrossOverlay(rr.Center, 12f, new Scalar(0, 0, 255), Thickness: 2);
            var textOv = new TextOverlay($"Pose ({bestX:F1}, {bestY:F1}, {bestAngle:F1}°) Score={bestScore:F2}", new Point2f(rr.Center.X - 40, rr.Center.Y - bestTplSize.Height / 2 - 8), new Scalar(0, 255, 0), FontScale: 0.5);

            overlays.Add(rectOv);
            overlays.Add(crossOv);
            overlays.Add(textOv);

            rectOv.Render(annotated);
            crossOv.Render(annotated);
            textOv.Render(annotated);
        }

        var outFrame = new ImageFrame(annotated, inputFrame.CameraId, inputFrame.FrameId, inputFrame.Format);
        var outputs = new Dictionary<string, object?>
        {
            [PortBestScore] = bestScore,
            [PortPoseX] = bestX,
            [PortPoseY] = bestY,
            [PortPoseAngle] = bestAngle,
            [PortAnnotatedImage] = outFrame
        };

        return ToolResult.Ok(outputs, overlays, message: $"Rot-Match: Score={bestScore:F3}, Angle={bestAngle:F1}° at ({bestX:F1}, {bestY:F1})");
    }
}
