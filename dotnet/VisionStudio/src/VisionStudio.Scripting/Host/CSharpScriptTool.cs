using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Scripting.Host;

/// <summary>
/// Pipeline Vision Tool that executes an embedded sandboxed Roslyn C# script.
/// Allows engineers to perform custom filtering, complex math, or proprietary algorithms directly in pipeline.
/// </summary>
[VisionTool("Scripting.CSharp", "C# Script Block", "Scripting & Flow", Description = "Executes custom sandboxed C# script code to process pipeline data")]
public sealed class CSharpScriptTool : VisionToolBase
{
    public const string ParamScript = "ScriptCode";
    public const string ParamTimeout = "TimeoutMs";

    public const string PortInA = "InputA";
    public const string PortInB = "InputB";
    public const string PortInImage = "ImageIn";

    public const string PortOutA = "OutputA";
    public const string PortOutB = "OutputB";
    public const string PortOutPassed = "Passed";

    private readonly ToolParameter _pScript;
    private readonly ToolParameter _pTimeout;
    private readonly ScriptHost _scriptHost = new();

    public CSharpScriptTool() : base("Scripting.CSharp", "C# Script Block", "Scripting & Flow", "Executes custom C# script")
    {
        _pScript = AddParameter(new ToolParameter(
            ParamScript,
            "Script Code",
            ParameterType.String,
            "// C# Script Sandbox\nvar a = GetNumber(\"InputA\");\nvar b = GetNumber(\"InputB\");\nSet(\"OutputA\", a + b);\nSet(\"Passed\", a + b > 0);\nLog($\"Computed sum: {a + b}\");"));

        _pTimeout = AddParameter(new ToolParameter(ParamTimeout, "Timeout (ms)", ParameterType.Integer, 2000, min: 100, max: 30000));

        AddInput(new ToolPort(PortInA, "Input A", PortType.Any, PortDirection.Input, IsRequired: false));
        AddInput(new ToolPort(PortInB, "Input B", PortType.Any, PortDirection.Input, IsRequired: false));
        AddInput(new ToolPort(PortInImage, "Image In", PortType.Image, PortDirection.Input, IsRequired: false));

        AddOutput(new ToolPort(PortOutA, "Output A", PortType.Any, PortDirection.Output));
        AddOutput(new ToolPort(PortOutB, "Output B", PortType.Any, PortDirection.Output));
        AddOutput(new ToolPort(PortOutPassed, "Passed", PortType.Boolean, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        string code = _pScript.GetValue<string>() ?? string.Empty;
        int timeout = _pTimeout.GetValue<int>();

        var scriptCtx = new ScriptContext(ctx.Inputs, logger: ctx.Logger);

        var runTask = _scriptHost.ExecuteAsync(code, scriptCtx, timeout, ct);
        var execResult = runTask.GetAwaiter().GetResult();

        if (!execResult.Success)
        {
            var err = string.Join("; ", execResult.Errors);
            return ToolResult.Failed($"Script error: {err}");
        }

        var outputs = new Dictionary<string, object?>(scriptCtx.Outputs, StringComparer.OrdinalIgnoreCase);

        // Ensure default port outputs are mapped if script set them
        if (!outputs.ContainsKey(PortOutPassed) && outputs.TryGetValue("Passed", out var passVal))
        {
            outputs[PortOutPassed] = passVal;
        }

        bool passed = true;
        if (outputs.TryGetValue(PortOutPassed, out var pObj) && pObj is bool b)
        {
            passed = b;
        }

        return ToolResult.Ok(outputs, message: $"Script executed successfully in {execResult.ExecutionTime.TotalMilliseconds:F1} ms.");
    }
}
