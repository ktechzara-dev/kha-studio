using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Overlays;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;
using VisionStudio.Matching.Engine;
using VisionStudio.Matching.Models;

namespace VisionStudio.Matching.Tools;

/// <summary>
/// Industrial geometric pattern matching tool locating parts with sub-pixel and sub-degree accuracy.
/// </summary>
[VisionTool("Matching.GeometricMatcher", "Geometric Pattern Matcher", "Matching and Localization",
    Description = "Learns models from edge geometry with rotation, scale, occlusion, and contrast reversal robustness")]
public sealed class GeometricMatcherTool : VisionToolBase
{
    public const string ParamModelPath = "ModelPath";
    public const string ParamMinScore = "MinScore";
    public const string ParamMaxInstances = "MaxInstances";
    public const string ParamAngleMin = "AngleMin";
    public const string ParamAngleMax = "AngleMax";
    public const string ParamScaleMin = "ScaleMin";
    public const string ParamScaleMax = "ScaleMax";
    public const string ParamAllowContrastReversal = "AllowContrastReversal";
    public const string ParamMinContrast = "MinContrast";
    public const string ParamSubPixel = "SubPixelRefinement";

    public const string PortInImage = "ImageIn";
    public const string PortPoseX = "PoseX";
    public const string PortPoseY = "PoseY";
    public const string PortAngle = "Angle";
    public const string PortScale = "Scale";
    public const string PortScore = "Score";
    public const string PortMatchCount = "MatchCount";
    public const string PortAnnotatedImage = "AnnotatedImage";

    private readonly ToolParameter _pModelPath;
    private readonly ToolParameter _pMinScore;
    private readonly ToolParameter _pMaxInstances;
    private readonly ToolParameter _pAngleMin;
    private readonly ToolParameter _pAngleMax;
    private readonly ToolParameter _pScaleMin;
    private readonly ToolParameter _pScaleMax;
    private readonly ToolParameter _pAllowReversal;
    private readonly ToolParameter _pMinContrast;
    private readonly ToolParameter _pSubPixel;

    private readonly GeometricMatcher _matcher = new();
    private GeometricModel? _cachedModel;
    private string? _cachedModelPath;

    public GeometricMatcherTool()
        : base("Matching.GeometricMatcher", "Geometric Pattern Matcher", "Matching and Localization",
               "Learns models from edge geometry with rotation, scale, occlusion, and contrast reversal robustness")
    {
        _pModelPath = AddParameter(new ToolParameter(ParamModelPath, "Model File (.vsgeom)", ParameterType.FilePath, string.Empty));
        _pMinScore = AddParameter(new ToolParameter(ParamMinScore, "Min Score", ParameterType.Double, 0.70, min: 0.1, max: 1.0, step: 0.05));
        _pMaxInstances = AddParameter(new ToolParameter(ParamMaxInstances, "Max Instances", ParameterType.Integer, 1, min: 1, max: 20));
        _pAngleMin = AddParameter(new ToolParameter(ParamAngleMin, "Angle Min (deg)", ParameterType.Double, -180.0, min: -180.0, max: 180.0, step: 5.0));
        _pAngleMax = AddParameter(new ToolParameter(ParamAngleMax, "Angle Max (deg)", ParameterType.Double, 180.0, min: -180.0, max: 180.0, step: 5.0));
        _pScaleMin = AddParameter(new ToolParameter(ParamScaleMin, "Scale Min", ParameterType.Double, 0.90, min: 0.5, max: 2.0, step: 0.05));
        _pScaleMax = AddParameter(new ToolParameter(ParamScaleMax, "Scale Max", ParameterType.Double, 1.10, min: 0.5, max: 2.0, step: 0.05));
        _pAllowReversal = AddParameter(new ToolParameter(ParamAllowContrastReversal, "Allow Contrast Reversal", ParameterType.Boolean, false));
        _pMinContrast = AddParameter(new ToolParameter(ParamMinContrast, "Min Edge Contrast", ParameterType.Double, 20.0, min: 5.0, max: 100.0, step: 1.0));
        _pSubPixel = AddParameter(new ToolParameter(ParamSubPixel, "Sub-Pixel Refinement", ParameterType.Boolean, true));

        AddInput(new ToolPort(PortInImage, "Image In", PortType.Image, PortDirection.Input));

        AddOutput(new ToolPort(PortPoseX, "Pose X", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortPoseY, "Pose Y", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortAngle, "Angle (deg)", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortScale, "Scale", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortScore, "Score", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortMatchCount, "Match Count", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortAnnotatedImage, "Annotated Image", PortType.Image, PortDirection.Output));
    }

    public void SetModelDirectly(GeometricModel model)
    {
        _cachedModel = model;
        _cachedModelPath = null;
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var inputFrame = ctx.GetImage(PortInImage);
        if (inputFrame == null) return ToolResult.Failed("Missing input image.");

        // Load model if path specified and changed
        var path = _pModelPath.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(path) && !string.Equals(path, _cachedModelPath, StringComparison.OrdinalIgnoreCase))
        {
            if (File.Exists(path))
            {
                _cachedModel = GeometricModel.LoadFromFile(path);
                _cachedModelPath = path;
            }
            else
            {
                return ToolResult.Failed($"Model file does not exist: {path}");
            }
        }

        if (_cachedModel == null)
        {
            return ToolResult.Warning("No Geometric Model assigned. Train a model in the Model Editor or load a .vsgeom file.");
        }

        var options = new GeometricMatcherOptions
        {
            MinScore = (float)_pMinScore.GetValue<double>(),
            MaxInstances = _pMaxInstances.GetValue<int>(),
            AngleMinDeg = (float)_pAngleMin.GetValue<double>(),
            AngleMaxDeg = (float)_pAngleMax.GetValue<double>(),
            ScaleMin = (float)_pScaleMin.GetValue<double>(),
            ScaleMax = (float)_pScaleMax.GetValue<double>(),
            AllowContrastReversal = _pAllowReversal.GetValue<bool>(),
            MinContrast = (float)_pMinContrast.GetValue<double>(),
            SubPixelRefinement = _pSubPixel.GetValue<bool>()
        };

        var report = _matcher.Match(inputFrame.Mat, _cachedModel, options);

        var outputs = new Dictionary<string, object?>
        {
            [PortMatchCount] = report.MatchCount
        };

        var annotatedMat = inputFrame.Mat.Clone();

        if (report.Success && report.BestMatch != null)
        {
            var best = report.BestMatch;
            outputs[PortPoseX] = (double)best.X;
            outputs[PortPoseY] = (double)best.Y;
            outputs[PortAngle] = (double)best.AngleDeg;
            outputs[PortScale] = (double)best.Scale;
            outputs[PortScore] = (double)best.Score;

            // Render overlays for each instance
            foreach (var inst in report.Instances)
            {
                var corners = inst.BoundingCorners;
                for (int i = 0; i < 4; i++)
                {
                    var p1 = new Point((int)corners[i].X, (int)corners[i].Y);
                    var p2 = new Point((int)corners[(i + 1) % 4].X, (int)corners[(i + 1) % 4].Y);
                    Cv2.Line(annotatedMat, p1, p2, new Scalar(0, 255, 0), 2);
                }

                // Origin cross
                Cv2.DrawMarker(annotatedMat, new Point((int)inst.X, (int)inst.Y), new Scalar(255, 0, 255), MarkerTypes.Cross, 16, 2);

                // Label
                string label = $"{inst.Score:P0} ({inst.AngleDeg:F1}deg)";
                Cv2.PutText(annotatedMat, label, new Point((int)corners[0].X, (int)corners[0].Y - 6),
                    HersheyFonts.HersheySimplex, 0.45, new Scalar(0, 255, 255), 1);
            }
        }
        else
        {
            outputs[PortPoseX] = 0.0;
            outputs[PortPoseY] = 0.0;
            outputs[PortAngle] = 0.0;
            outputs[PortScale] = 1.0;
            outputs[PortScore] = 0.0;
        }

        var outputFrame = new ImageFrame(annotatedMat);
        outputs[PortAnnotatedImage] = outputFrame;

        return ToolResult.Ok(outputs);
    }
}
