using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Core.Flow;

/// <summary>
/// Safe algebraic and relational expression evaluator for numbers and tolerances.
/// </summary>
[VisionTool("Flow.Expression", "Expression", "Flow Control", Description = "Evaluates math operations and comparisons between numbers")]
public sealed class ExpressionTool : VisionToolBase
{
    public const string PortA = "A";
    public const string PortB = "B";
    public const string PortResultNumber = "ResultNumber";
    public const string PortResultBool = "ResultBool";

    public const string ParamOperation = "Operation";
    public const string ParamTolerance = "Tolerance";

    private readonly ToolParameter _pOp;
    private readonly ToolParameter _pTolerance;

    public ExpressionTool() : base("Flow.Expression", "Expression", "Flow Control", "Math and comparison evaluator")
    {
        _pOp = AddParameter(new ToolParameter(
            ParamOperation,
            "Operation",
            ParameterType.Enum,
            "Add",
            enumOptions: new[] { "Add", "Subtract", "Multiply", "Divide", "GreaterThan", "LessThan", "Equal", "InRange" }));

        _pTolerance = AddParameter(new ToolParameter(ParamTolerance, "Tolerance", ParameterType.Double, 0.001, min: 0.0, max: 1000.0));

        AddInput(new ToolPort(PortA, "A", PortType.Number, PortDirection.Input, IsRequired: true, DefaultValue: 0.0));
        AddInput(new ToolPort(PortB, "B", PortType.Number, PortDirection.Input, IsRequired: false, DefaultValue: 0.0));

        AddOutput(new ToolPort(PortResultNumber, "Result Number", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortResultBool, "Result Bool", PortType.Boolean, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var a = Convert.ToDouble(ctx.GetInput<object>(PortA) ?? 0.0);
        var b = Convert.ToDouble(ctx.GetInput<object>(PortB) ?? 0.0);
        var op = _pOp.GetValue<string>() ?? "Add";
        var tol = _pTolerance.GetValue<double>();

        double resultNum = 0.0;
        bool resultBool = false;

        switch (op.ToLowerInvariant())
        {
            case "add":
                resultNum = a + b;
                resultBool = resultNum != 0;
                break;
            case "subtract":
                resultNum = a - b;
                resultBool = resultNum != 0;
                break;
            case "multiply":
                resultNum = a * b;
                resultBool = resultNum != 0;
                break;
            case "divide":
                resultNum = Math.Abs(b) > 1e-12 ? a / b : 0.0;
                resultBool = Math.Abs(b) > 1e-12;
                break;
            case "greaterthan":
                resultNum = a - b;
                resultBool = a > b;
                break;
            case "lessthan":
                resultNum = b - a;
                resultBool = a < b;
                break;
            case "equal":
                resultNum = Math.Abs(a - b);
                resultBool = Math.Abs(a - b) <= tol;
                break;
            case "inrange":
                resultNum = Math.Abs(a);
                resultBool = Math.Abs(a - b) <= tol;
                break;
            default:
                resultNum = a + b;
                resultBool = true;
                break;
        }

        var outputs = new Dictionary<string, object?>
        {
            [PortResultNumber] = resultNum,
            [PortResultBool] = resultBool
        };

        return ToolResult.Ok(outputs, message: $"Expr '{op}': Num={resultNum}, Bool={resultBool}");
    }
}
