using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Overlays;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Matching;

/// <summary>
/// Industrial pattern alignment tool computing compensation offset (Delta X, Delta Y, Delta Angle) relative to nominal target.
/// Feeds robot guidance and XY-Theta alignment stages.
/// </summary>
[VisionTool("Matching.PatternAlign", "Pattern Align", "Matching and Localization", Description = "Computes 2D offset deviations (Delta X, Delta Y, Delta Angle) for robot guidance")]
public sealed class PatternAlignTool : VisionToolBase
{
    public const string ParamNominalX = "NominalX";
    public const string ParamNominalY = "NominalY";
    public const string ParamNominalAngle = "NominalAngle";

    public const string PortCurrentX = "CurrentX";
    public const string PortCurrentY = "CurrentY";
    public const string PortCurrentAngle = "CurrentAngle";

    public const string PortDeltaX = "DeltaX";
    public const string PortDeltaY = "DeltaY";
    public const string PortDeltaAngle = "DeltaAngle";

    private readonly ToolParameter _pNomX;
    private readonly ToolParameter _pNomY;
    private readonly ToolParameter _pNomAngle;

    public PatternAlignTool() : base("Matching.PatternAlign", "Pattern Align", "Matching and Localization", "Calculates robot alignment deltas")
    {
        _pNomX = AddParameter(new ToolParameter(ParamNominalX, "Nominal Reference X", ParameterType.Double, 160.0));
        _pNomY = AddParameter(new ToolParameter(ParamNominalY, "Nominal Reference Y", ParameterType.Double, 120.0));
        _pNomAngle = AddParameter(new ToolParameter(ParamNominalAngle, "Nominal Reference Angle (deg)", ParameterType.Double, 0.0, unit: "deg"));

        AddInput(new ToolPort(PortCurrentX, "Current Pose X", PortType.Number, PortDirection.Input, IsRequired: true, DefaultValue: 160.0));
        AddInput(new ToolPort(PortCurrentY, "Current Pose Y", PortType.Number, PortDirection.Input, IsRequired: true, DefaultValue: 120.0));
        AddInput(new ToolPort(PortCurrentAngle, "Current Pose Angle", PortType.Number, PortDirection.Input, IsRequired: false, DefaultValue: 0.0));

        AddOutput(new ToolPort(PortDeltaX, "Delta X", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortDeltaY, "Delta Y", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortDeltaAngle, "Delta Angle (deg)", PortType.Number, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var curX = Convert.ToDouble(ctx.GetInput<object>(PortCurrentX) ?? 0.0);
        var curY = Convert.ToDouble(ctx.GetInput<object>(PortCurrentY) ?? 0.0);
        var curAngle = Convert.ToDouble(ctx.GetInput<object>(PortCurrentAngle) ?? 0.0);

        var nomX = _pNomX.GetValue<double>();
        var nomY = _pNomY.GetValue<double>();
        var nomAngle = _pNomAngle.GetValue<double>();

        var deltaX = curX - nomX;
        var deltaY = curY - nomY;
        var deltaAngle = curAngle - nomAngle;

        var overlays = new List<IOverlayPrimitive>
        {
            new CrossOverlay(new Point2f((float)nomX, (float)nomY), 14f, new Scalar(0, 255, 0), Thickness: 1), // Nominal in green
            new CrossOverlay(new Point2f((float)curX, (float)curY), 14f, new Scalar(0, 0, 255), Thickness: 2), // Current in red
            new LineOverlay(new Point2f((float)nomX, (float)nomY), new Point2f((float)curX, (float)curY), new Scalar(0, 255, 255), Thickness: 1)
        };

        var outputs = new Dictionary<string, object?>
        {
            [PortDeltaX] = deltaX,
            [PortDeltaY] = deltaY,
            [PortDeltaAngle] = deltaAngle
        };

        return ToolResult.Ok(outputs, overlays, message: $"Alignment Deltas: ΔX={deltaX:+0.00;-0.00;0.00}, ΔY={deltaY:+0.00;-0.00;0.00}, Δθ={deltaAngle:+0.00;-0.00;0.00}°");
    }
}
