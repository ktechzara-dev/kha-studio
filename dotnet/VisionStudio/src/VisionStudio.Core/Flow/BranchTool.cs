using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Core.Flow;

/// <summary>
/// Flow control tool routing input data to True or False output based on a boolean condition.
/// </summary>
[VisionTool("Flow.Branch", "Branch", "Flow Control", Description = "Routes data to True or False branch based on a condition")]
public sealed class BranchTool : VisionToolBase
{
    public const string PortCondition = "Condition";
    public const string PortDataIn = "DataIn";
    public const string PortTrueOut = "TrueOut";
    public const string PortFalseOut = "FalseOut";
    public const string PortConditionOut = "ConditionOut";

    public BranchTool() : base("Flow.Branch", "Branch", "Flow Control", "Routes data based on boolean input")
    {
        AddInput(new ToolPort(PortCondition, "Condition", PortType.Boolean, PortDirection.Input, IsRequired: true, DefaultValue: true));
        AddInput(new ToolPort(PortDataIn, "Data In", PortType.Any, PortDirection.Input, IsRequired: false));

        AddOutput(new ToolPort(PortTrueOut, "True Out", PortType.Any, PortDirection.Output));
        AddOutput(new ToolPort(PortFalseOut, "False Out", PortType.Any, PortDirection.Output));
        AddOutput(new ToolPort(PortConditionOut, "Condition Out", PortType.Boolean, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var condition = ctx.GetInput<bool?>(PortCondition) ?? true;
        var data = ctx.Inputs.TryGetValue(PortDataIn, out var val) ? val : null;

        var outputs = new Dictionary<string, object?>
        {
            [PortConditionOut] = condition,
            [PortTrueOut] = condition ? data : null,
            [PortFalseOut] = !condition ? data : null
        };

        return ToolResult.Ok(outputs, message: condition ? "Branched to True" : "Branched to False");
    }
}
