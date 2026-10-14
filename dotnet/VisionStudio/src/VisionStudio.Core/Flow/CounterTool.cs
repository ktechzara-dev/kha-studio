using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Core.Flow;

/// <summary>
/// Stateful cycle and execution counter with target threshold detection.
/// </summary>
[VisionTool("Flow.Counter", "Counter", "Flow Control", Description = "Counts pipeline executions and checks target limits")]
public sealed class CounterTool : VisionToolBase
{
    public const string ParamInitial = "InitialValue";
    public const string ParamStep = "Step";
    public const string ParamTarget = "Target";

    public const string PortCount = "Count";
    public const string PortTargetReached = "TargetReached";

    private int _currentCount;
    private readonly ToolParameter _pInitial;
    private readonly ToolParameter _pStep;
    private readonly ToolParameter _pTarget;

    public CounterTool() : base("Flow.Counter", "Counter", "Flow Control", "Counts pipeline executions")
    {
        _pInitial = AddParameter(new ToolParameter(ParamInitial, "Initial Value", ParameterType.Integer, 0));
        _pStep = AddParameter(new ToolParameter(ParamStep, "Step Increment", ParameterType.Integer, 1));
        _pTarget = AddParameter(new ToolParameter(ParamTarget, "Target Value", ParameterType.Integer, 100));

        AddOutput(new ToolPort(PortCount, "Count", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortTargetReached, "Target Reached", PortType.Boolean, PortDirection.Output));

        _currentCount = _pInitial.GetValue<int>();
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var step = _pStep.GetValue<int>();
        var target = _pTarget.GetValue<int>();

        _currentCount += step;
        var reached = _currentCount >= target;

        var outputs = new Dictionary<string, object?>
        {
            [PortCount] = _currentCount,
            [PortTargetReached] = reached
        };

        return ToolResult.Ok(outputs, message: $"Current count: {_currentCount} (Target: {target})");
    }

    public override void Reset()
    {
        _currentCount = _pInitial.GetValue<int>();
    }
}
