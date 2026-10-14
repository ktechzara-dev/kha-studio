using VisionStudio.Core.Overlays;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Measurement;

/// <summary>
/// Converts raw pixel measurements to calibrated millimeter dimensions and evaluates engineering tolerances.
/// </summary>
[VisionTool("Measurement.Dimension", "Dimension & Tolerance", "Measurement", Description = "Calibrates pixel dimensions to mm with engineering tolerance checks")]
public sealed class DimensionMeasureTool : VisionToolBase
{
    public const string ParamNominal = "NominalDimensionMm";
    public const string ParamTolerance = "ToleranceMm";
    public const string ParamScaleMm = "ScaleMmPerPixel";

    public const string PortPixelValue = "PixelDimensionIn";
    public const string PortDimensionMm = "DimensionMm";
    public const string PortDeviationMm = "DeviationMm";
    public const string PortPass = "IsPass";

    private readonly ToolParameter _pNominal;
    private readonly ToolParameter _pTolerance;
    private readonly ToolParameter _pScale;

    public DimensionMeasureTool() : base("Measurement.Dimension", "Dimension & Tolerance", "Measurement", "Calibrates mm dimensions")
    {
        _pNominal = AddParameter(new ToolParameter(ParamNominal, "Nominal Dimension (mm)", ParameterType.Double, 10.0, min: 0.0, max: 10000.0, unit: "mm"));
        _pTolerance = AddParameter(new ToolParameter(ParamTolerance, "Tolerance ± (mm)", ParameterType.Double, 0.2, min: 0.001, max: 100.0, unit: "mm"));
        _pScale = AddParameter(new ToolParameter(ParamScaleMm, "Calibration Scale (mm/px)", ParameterType.Double, 0.05, min: 0.0001, max: 10.0, unit: "mm/px"));

        AddInput(new ToolPort(PortPixelValue, "Pixel Dimension In", PortType.Number, PortDirection.Input, IsRequired: true, DefaultValue: 200.0));

        AddOutput(new ToolPort(PortDimensionMm, "Dimension (mm)", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortDeviationMm, "Deviation (mm)", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortPass, "Pass / Fail", PortType.Boolean, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var rawPx = Convert.ToDouble(ctx.GetInput<object>(PortPixelValue) ?? 0.0);
        var scale = _pScale.GetValue<double>();
        var nominal = _pNominal.GetValue<double>();
        var tol = _pTolerance.GetValue<double>();

        var dimensionMm = rawPx * scale;
        var deviation = dimensionMm - nominal;
        var isPass = Math.Abs(deviation) <= tol;

        var outputs = new Dictionary<string, object?>
        {
            [PortDimensionMm] = dimensionMm,
            [PortDeviationMm] = deviation,
            [PortPass] = isPass
        };

        return ToolResult.Ok(outputs, message: $"Dim: {dimensionMm:F3} mm (Dev: {deviation:+0.000;-0.000;0.000} mm, {(isPass ? "PASS" : "FAIL")})");
    }
}
