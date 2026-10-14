using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Overlays;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Matching;

/// <summary>
/// Normalized Cross-Correlation (NCC) template matching for high-speed part localization.
/// </summary>
[VisionTool("Matching.TemplateMatch", "Template Match (NCC)", "Matching and Localization", Description = "Normalized cross-correlation template matching")]
public sealed class TemplateMatchTool : VisionToolBase
{
    public const string ParamMinScore = "MinScore";
    public const string ParamMaxMatches = "MaxMatches";

    public const string PortInImage = "ImageIn";
    public const string PortTemplate = "TemplateIn";
    public const string PortMatchCount = "MatchCount";
    public const string PortBestScore = "BestScore";
    public const string PortBestX = "BestX";
    public const string PortBestY = "BestY";
    public const string PortAnnotatedImage = "AnnotatedImage";

    private readonly ToolParameter _pScore;
    private readonly ToolParameter _pMax;

    public TemplateMatchTool() : base("Matching.TemplateMatch", "Template Match (NCC)", "Matching and Localization", "Normalized cross correlation")
    {
        _pScore = AddParameter(new ToolParameter(ParamMinScore, "Min Match Score (0-1)", ParameterType.Double, 0.75, min: 0.1, max: 1.0, step: 0.05));
        _pMax = AddParameter(new ToolParameter(ParamMaxMatches, "Max Matches", ParameterType.Integer, 5, min: 1, max: 100));

        AddInput(new ToolPort(PortInImage, "Image In", PortType.Image, PortDirection.Input));
        AddInput(new ToolPort(PortTemplate, "Template In", PortType.Image, PortDirection.Input));

        AddOutput(new ToolPort(PortMatchCount, "Match Count", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortBestScore, "Best Score", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortBestX, "Best X", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortBestY, "Best Y", PortType.Number, PortDirection.Output));
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

        if (tpl.Cols > src.Cols || tpl.Rows > src.Rows)
        {
            return ToolResult.Failed("Template dimensions exceed input image size.");
        }

        using var graySrc = new Mat();
        using var grayTpl = new Mat();

        if (src.Channels() > 1) Cv2.CvtColor(src, graySrc, ColorConversionCodes.BGR2GRAY);
        else src.CopyTo(graySrc);

        if (tpl.Channels() > 1) Cv2.CvtColor(tpl, grayTpl, ColorConversionCodes.BGR2GRAY);
        else tpl.CopyTo(grayTpl);

        using var matchResult = new Mat();
        Cv2.MatchTemplate(graySrc, grayTpl, matchResult, TemplateMatchModes.CCoeffNormed);

        Cv2.MinMaxLoc(matchResult, out _, out double maxVal, out _, out Point maxLoc);

        var minScore = _pScore.GetValue<double>();
        var isMatch = maxVal >= minScore;

        var annotated = src.Clone();
        var overlays = new List<IOverlayPrimitive>();

        double bestX = 0, bestY = 0;

        if (isMatch)
        {
            bestX = maxLoc.X + tpl.Cols / 2.0;
            bestY = maxLoc.Y + tpl.Rows / 2.0;

            var r = new Rect2f(maxLoc.X, maxLoc.Y, tpl.Cols, tpl.Rows);
            var rectOv = new RectOverlay(r, new Scalar(0, 255, 0), Thickness: 2);
            var crossOv = new CrossOverlay(new Point2f((float)bestX, (float)bestY), 12f, new Scalar(0, 0, 255), Thickness: 2);
            var textOv = new TextOverlay($"Score: {maxVal:F3}", new Point2f(r.X, r.Y - 6), new Scalar(0, 255, 0), FontScale: 0.5);

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
            [PortMatchCount] = isMatch ? 1 : 0,
            [PortBestScore] = maxVal,
            [PortBestX] = bestX,
            [PortBestY] = bestY,
            [PortAnnotatedImage] = outFrame
        };

        return ToolResult.Ok(outputs, overlays, message: $"NCC Match: Score={maxVal:F3} (Thresh={minScore:F2}, {(isMatch ? "FOUND" : "NOT FOUND")})");
    }
}
