using System.Diagnostics;
using VisionStudio.Core.Overlays;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Pipeline;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Core.Macro;

/// <summary>
/// Execution wrapper for a MacroDefinition sub-pipeline.
/// Implements IVisionTool, allowing macro blocks to be placed directly into a parent pipeline
/// and nested to any depth.
/// </summary>
public sealed class MacroTool : VisionToolBase
{
    private readonly ToolRegistry _registry;
    private readonly PipelineSerializer _serializer = new();
    private readonly PipelineExecutor _executor = new();

    public MacroDefinition Definition { get; }

    public MacroTool(MacroDefinition definition, ToolRegistry? registry = null)
        : base(
            $"Macro.{definition.Id}",
            definition.Name,
            definition.Category ?? "Macros",
            definition.Description ?? $"Macro sub-pipeline v{definition.Version}")
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _registry = registry ?? ToolRegistry.Shared;

        // Register exposed inputs
        foreach (var inp in definition.ExposedInputs)
        {
            AddInput(new ToolPort(
                inp.ExposedPortId,
                string.IsNullOrEmpty(inp.DisplayName) ? inp.ExposedPortId : inp.DisplayName,
                inp.Type,
                PortDirection.Input,
                IsRequired: inp.IsRequired,
                DefaultValue: inp.DefaultValue,
                Description: inp.Description));
        }

        // Register exposed outputs
        foreach (var outp in definition.ExposedOutputs)
        {
            AddOutput(new ToolPort(
                outp.ExposedPortId,
                string.IsNullOrEmpty(outp.DisplayName) ? outp.ExposedPortId : outp.DisplayName,
                outp.Type,
                PortDirection.Output,
                Description: outp.Description));
        }

        // Register exposed parameters
        foreach (var param in definition.ExposedParameters)
        {
            AddParameter(new ToolParameter(
                param.ExposedParamName,
                string.IsNullOrEmpty(param.DisplayName) ? param.ExposedParamName : param.DisplayName,
                param.Type,
                param.DefaultValue ?? 0,
                category: "Macro Parameters",
                tooltip: param.Description,
                min: param.Min,
                max: param.Max,
                step: param.Step,
                enumOptions: param.EnumOptions));
        }
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        // 1. Instantiate the inner sub-pipeline DAG
        PipelineGraph innerGraph;
        try
        {
            innerGraph = _serializer.DeserializeFromDocument(Definition.SerializedPipeline, _registry);
        }
        catch (Exception ex)
        {
            return ToolResult.Failed($"Failed to instantiate inner macro graph: {ex.Message}");
        }

        // 2. Apply external exposed parameter overrides to internal node parameters
        foreach (var paramMapping in Definition.ExposedParameters)
        {
            var myParam = Parameters.FirstOrDefault(p => p.Name.Equals(paramMapping.ExposedParamName, StringComparison.OrdinalIgnoreCase));
            if (myParam != null)
            {
                var targetNode = innerGraph.GetNode(paramMapping.InternalNodeId);
                if (targetNode != null)
                {
                    var targetParam = targetNode.Tool.Parameters.FirstOrDefault(p => p.Name.Equals(paramMapping.InternalParamName, StringComparison.OrdinalIgnoreCase));
                    if (targetParam != null)
                    {
                        targetParam.Value = myParam.Value;
                    }
                }
            }
        }

        // 3. Map external inputs to designated internal nodes and ports
        var externalInputs = new Dictionary<(string NodeId, string PortId), object?>();
        foreach (var inpMapping in Definition.ExposedInputs)
        {
            if (ctx.Inputs.TryGetValue(inpMapping.ExposedPortId, out var val))
            {
                externalInputs[(inpMapping.InternalNodeId, inpMapping.InternalPortId)] = val;
            }
            else if (inpMapping.DefaultValue != null)
            {
                externalInputs[(inpMapping.InternalNodeId, inpMapping.InternalPortId)] = inpMapping.DefaultValue;
            }
        }

        // 4. Execute the inner pipeline
        PipelineExecutionResult execResult;
        try
        {
            execResult = _executor.ExecuteAsync(innerGraph, stopAtBreakpoints: false, ct: ct, externalInputs: externalInputs)
                .GetAwaiter()
                .GetResult();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ToolResult.Failed($"Inner macro execution failed: {ex.Message}");
        }

        if (!execResult.Success)
        {
            return ToolResult.Failed($"Inner macro execution returned error: {execResult.ErrorMessage}");
        }

        // 5. Gather outputs according to Definition.ExposedOutputs
        var outputs = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var mergedOverlays = new List<IOverlayPrimitive>();

        foreach (var outpMapping in Definition.ExposedOutputs)
        {
            if (execResult.NodeResults.TryGetValue(outpMapping.InternalNodeId, out var nodeResult))
            {
                if (nodeResult.Outputs.TryGetValue(outpMapping.InternalPortId, out var portVal))
                {
                    outputs[outpMapping.ExposedPortId] = portVal;
                }
            }
        }

        // Collect overlays from all inner nodes
        foreach (var nr in execResult.NodeResults.Values)
        {
            mergedOverlays.AddRange(nr.Overlays);
        }

        bool allPassed = execResult.NodeResults.Values.All(r => r.Status == ToolStatus.Ok);

        return new ToolResult(
            outputs: outputs,
            overlays: mergedOverlays,
            executionTime: sw.Elapsed,
            status: allPassed ? ToolStatus.Ok : ToolStatus.Error,
            message: $"Macro '{Definition.Name}' executed in {sw.Elapsed.TotalMilliseconds:F1} ms (Nodes={innerGraph.Nodes.Count}).");
    }
}
