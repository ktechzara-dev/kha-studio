using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Core.Flow;

/// <summary>
/// Pauses execution for a specified duration to allow sensor settling or pacing.
/// </summary>
[VisionTool("Flow.Delay", "Delay", "Flow Control", Description = "Pauses pipeline execution for a configurable duration")]
public sealed class DelayTool : VisionToolBase
{
    public const string ParamMilliseconds = "Milliseconds";
    public const string PortIn = "DataIn";
    public const string PortOut = "DataOut";

    private readonly ToolParameter _pDelay;

    public DelayTool() : base("Flow.Delay", "Delay", "Flow Control", "Pauses pipeline execution")
    {
        _pDelay = AddParameter(new ToolParameter(ParamMilliseconds, "Delay (ms)", ParameterType.Integer, 50, min: 0, max: 60000, unit: "ms"));

        AddInput(new ToolPort(PortIn, "Data In", PortType.Any, PortDirection.Input, IsRequired: false));
        AddOutput(new ToolPort(PortOut, "Data Out", PortType.Any, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var ms = _pDelay.GetValue<int>();
        if (ms > 0)
        {
            ct.WaitHandle.WaitOne(ms);
        }

        var data = ctx.Inputs.TryGetValue(PortIn, out var val) ? val : null;
        var outputs = new Dictionary<string, object?> { [PortOut] = data };

        return ToolResult.Ok(outputs, message: $"Delayed {ms} ms");
    }
}
