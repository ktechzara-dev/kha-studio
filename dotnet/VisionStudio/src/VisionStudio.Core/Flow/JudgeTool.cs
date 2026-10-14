using OpenCvSharp;
using VisionStudio.Core.Overlays;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Core.Flow;

/// <summary>
/// Industrial pass/fail judgment tool comparing numerical measurements against tolerance limits.
/// </summary>
[VisionTool("Output.Judge", "Judge", "Output", Description = "Evaluates tolerance thresholds to produce OK/NG verdict")]
public sealed class JudgeTool : VisionToolBase
{
    public const string PortValue = "Value";
    public const string PortIsOk = "IsOk";
    public const string PortVerdict = "Verdict";

    public const string ParamLower = "LowerLimit";
    public const string ParamUpper = "UpperLimit";

    private readonly ToolParameter _pLower;
    private readonly ToolParameter _pUpper;

    public JudgeTool() : base("Output.Judge", "Judge", "Output", "Tolerance judgment")
    {
        _pLower = AddParameter(new ToolParameter(ParamLower, "Lower Limit", ParameterType.Double, 10.0));
        _pUpper = AddParameter(new ToolParameter(ParamUpper, "Upper Limit", ParameterType.Double, 50.0));

        AddInput(new ToolPort(PortValue, "Value", PortType.Number, PortDirection.Input, IsRequired: true, DefaultValue: 0.0));

        AddOutput(new ToolPort(PortIsOk, "Is OK", PortType.Boolean, PortDirection.Output));
        AddOutput(new ToolPort(PortVerdict, "Verdict", PortType.String, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var val = Convert.ToDouble(ctx.GetInput<object>(PortValue) ?? 0.0);
        var lower = _pLower.GetValue<double>();
        var upper = _pUpper.GetValue<double>();

        var isOk = val >= lower && val <= upper;
        var verdict = isOk ? "OK" : "NG";

        var overlays = new List<IOverlayPrimitive>
        {
            new TextOverlay(
                verdict,
                new Point2f(20, 40),
                isOk ? new Scalar(0, 255, 0) : new Scalar(0, 0, 255),
                FontScale: 1.2,
                Thickness: 2)
        };

        var outputs = new Dictionary<string, object?>
        {
            [PortIsOk] = isOk,
            [PortVerdict] = verdict
        };

        return ToolResult.Ok(outputs, overlays, message: $"Judge: {verdict} (Value={val:F2}, Range=[{lower:F2}, {upper:F2}])");
    }
}
