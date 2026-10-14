using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Core.Flow;

/// <summary>
/// Source tool emitting constant numerical and text values for simulation and testing.
/// </summary>
[VisionTool("Flow.ConstantSource", "Constant Source", "Flow Control", Description = "Emits configured constant values")]
public sealed class ConstantSourceTool : VisionToolBase
{
    public const string ParamNumber = "NumberValue";
    public const string ParamText = "TextValue";
    public const string PortNumber = "OutNumber";
    public const string PortText = "OutText";

    private readonly ToolParameter _pNum;
    private readonly ToolParameter _pText;

    public ConstantSourceTool() : base("Flow.ConstantSource", "Constant Source", "Flow Control", "Emits configured constants")
    {
        _pNum = AddParameter(new ToolParameter(ParamNumber, "Number Value", ParameterType.Double, 42.0));
        _pText = AddParameter(new ToolParameter(ParamText, "Text Value", ParameterType.String, "Part-A100"));

        AddOutput(new ToolPort(PortNumber, "Number Out", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortText, "Text Out", PortType.String, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var num = _pNum.GetValue<double>();
        var text = _pText.GetValue<string>() ?? string.Empty;

        var outputs = new Dictionary<string, object?>
        {
            [PortNumber] = num,
            [PortText] = text
        };

        return ToolResult.Ok(outputs, message: $"Constant: {num}, '{text}'");
    }
}
