using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Overlays;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;
using VisionStudio.Matching.Locator;
using VisionStudio.Matching.Models;

namespace VisionStudio.Matching.Tools;

/// <summary>
/// Tool that locates multiple components and verifies relative geometric assembly rules.
/// </summary>
[VisionTool("Matching.MultiModelLocator", "Multi-Model Locator & Assembly Verifier", "Matching and Localization",
    Description = "Locates several geometric models and validates relative position tolerances (assembly verification)")]
public sealed class MultiModelLocatorTool : VisionToolBase
{
    public const string PortInImage = "ImageIn";
    public const string PortIsAssemblyValid = "IsAssemblyValid";
    public const string PortDiscrepancies = "Discrepancies";
    public const string PortLocatedCount = "LocatedCount";
    public const string PortAnnotatedImage = "AnnotatedImage";

    public const string ParamNominalDistance = "NominalDistancePx";
    public const string ParamDistanceTolerance = "DistanceTolerancePx";
    public const string ParamNominalAngle = "NominalAngleDeg";
    public const string ParamAngleTolerance = "AngleToleranceDeg";

    private readonly ToolParameter _pNominalDist;
    private readonly ToolParameter _pDistTol;
    private readonly ToolParameter _pNominalAng;
    private readonly ToolParameter _pAngTol;

    private readonly MultiModelLocator _locator = new();

    public MultiModelLocatorTool()
        : base("Matching.MultiModelLocator", "Multi-Model Locator & Assembly Verifier", "Matching and Localization",
               "Locates multiple models and verifies assembly integrity")
    {
        _pNominalDist = AddParameter(new ToolParameter(ParamNominalDistance, "Nominal Part Distance (px)", ParameterType.Double, 100.0, min: 0.0, max: 2000.0));
        _pDistTol = AddParameter(new ToolParameter(ParamDistanceTolerance, "Distance Tolerance (px)", ParameterType.Double, 15.0, min: 0.5, max: 200.0));
        _pNominalAng = AddParameter(new ToolParameter(ParamNominalAngle, "Nominal Relative Angle (deg)", ParameterType.Double, 0.0, min: -180.0, max: 180.0));
        _pAngTol = AddParameter(new ToolParameter(ParamAngleTolerance, "Angle Tolerance (deg)", ParameterType.Double, 5.0, min: 0.5, max: 45.0));

        AddInput(new ToolPort(PortInImage, "Image In", PortType.Image, PortDirection.Input));

        AddOutput(new ToolPort(PortIsAssemblyValid, "Assembly Valid", PortType.Boolean, PortDirection.Output));
        AddOutput(new ToolPort(PortDiscrepancies, "Discrepancies", PortType.String, PortDirection.Output));
        AddOutput(new ToolPort(PortLocatedCount, "Located Count", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortAnnotatedImage, "Annotated Image", PortType.Image, PortDirection.Output));
    }

    public void AddModel(GeometricModel model)
    {
        _locator.RegisterModel(model);
    }

    public void AddAssemblyRule(AssemblyVerificationRule rule)
    {
        _locator.AddRule(rule);
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var inputFrame = ctx.GetImage(PortInImage);
        if (inputFrame == null) return ToolResult.Failed("Missing input image.");

        var (matches, verification) = _locator.LocateAndVerify(inputFrame.Mat, MultiModelSearchMode.Best);

        var annotatedMat = inputFrame.Mat.Clone();
        foreach (var (modelId, report) in matches)
        {
            if (report.Success && report.BestMatch != null)
            {
                var inst = report.BestMatch;
                var corners = inst.BoundingCorners;
                var color = verification.IsValid ? new Scalar(0, 255, 0) : new Scalar(0, 0, 255);

                for (int i = 0; i < 4; i++)
                {
                    Cv2.Line(annotatedMat, new Point((int)corners[i].X, (int)corners[i].Y),
                        new Point((int)corners[(i + 1) % 4].X, (int)corners[(i + 1) % 4].Y), color, 2);
                }

                Cv2.PutText(annotatedMat, $"{modelId}: {inst.Score:P0}",
                    new Point((int)corners[0].X, (int)corners[0].Y - 5),
                    HersheyFonts.HersheySimplex, 0.45, color, 1);
            }
        }

        string discText = verification.Discrepancies.Count > 0
            ? string.Join("; ", verification.Discrepancies)
            : "OK - All assembly tolerances satisfied";

        var outputs = new Dictionary<string, object?>
        {
            [PortIsAssemblyValid] = verification.IsValid,
            [PortDiscrepancies] = discText,
            [PortLocatedCount] = verification.LocatedParts.Count,
            [PortAnnotatedImage] = new ImageFrame(annotatedMat)
        };

        return ToolResult.Ok(outputs);
    }
}
