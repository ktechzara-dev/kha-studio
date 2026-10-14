using VisionStudio.Core.Pipeline;
using VisionStudio.Core.Ports;

namespace VisionStudio.Core.Macro;

/// <summary>
/// Helper to extract selected nodes from an existing PipelineGraph and package them into a MacroDefinition.
/// </summary>
public static class MacroBuilder
{
    public static MacroDefinition CreateMacroFromSelection(
        PipelineGraph graph,
        IEnumerable<string> selectedNodeIds,
        string macroName,
        string version = "1.0.0",
        string description = "")
    {
        var selectedSet = new HashSet<string>(selectedNodeIds, StringComparer.OrdinalIgnoreCase);
        if (selectedSet.Count == 0)
        {
            throw new ArgumentException("At least one node must be selected to create a macro.", nameof(selectedNodeIds));
        }

        var macro = new MacroDefinition
        {
            Name = macroName,
            Version = version,
            Description = description,
            CreatedAt = DateTimeOffset.UtcNow,
            ModifiedAt = DateTimeOffset.UtcNow
        };

        var serializer = new PipelineSerializer();
        var subDoc = new SerializedPipelineDocument
        {
            Name = macroName,
            Description = description
        };

        // 1. Copy selected nodes
        foreach (var nodeId in selectedSet)
        {
            var node = graph.GetNode(nodeId);
            if (node == null) continue;

            var nodeDto = new SerializedNodeDto
            {
                Id = node.Id,
                ToolId = node.ToolId,
                Title = node.Title,
                X = node.X,
                Y = node.Y,
                IsEnabled = node.IsEnabled,
                HasBreakpoint = node.HasBreakpoint
            };

            foreach (var p in node.Tool.Parameters)
            {
                nodeDto.Parameters[p.Name] = p.Value;
            }

            subDoc.Nodes.Add(nodeDto);
        }

        // 2. Classify connections & identify boundary inputs/outputs
        var incomingExternal = new List<PipelineConnection>();
        var outgoingExternal = new List<PipelineConnection>();

        foreach (var conn in graph.Connections)
        {
            bool srcIn = selectedSet.Contains(conn.SourceNodeId);
            bool tgtIn = selectedSet.Contains(conn.TargetNodeId);

            if (srcIn && tgtIn)
            {
                // Internal connection
                subDoc.Connections.Add(new SerializedConnectionDto
                {
                    SourceNodeId = conn.SourceNodeId,
                    SourcePortId = conn.SourcePortId,
                    TargetNodeId = conn.TargetNodeId,
                    TargetPortId = conn.TargetPortId
                });
            }
            else if (!srcIn && tgtIn)
            {
                incomingExternal.Add(conn);
            }
            else if (srcIn && !tgtIn)
            {
                outgoingExternal.Add(conn);
            }
        }

        // 3. Map boundary incoming connections to ExposedInputs
        int inIdx = 1;
        var handledInputs = new HashSet<(string, string)>();
        foreach (var conn in incomingExternal)
        {
            if (handledInputs.Add((conn.TargetNodeId, conn.TargetPortId)))
            {
                var targetNode = graph.GetNode(conn.TargetNodeId);
                var targetPort = targetNode?.Tool.Inputs.FirstOrDefault(p => p.Id.Equals(conn.TargetPortId, StringComparison.OrdinalIgnoreCase));
                var portType = targetPort?.Type ?? PortType.Any;

                string exposedName = targetPort != null ? $"{targetNode!.Title}_{targetPort.Name}" : $"Input_{inIdx++}";

                macro.ExposedInputs.Add(new MacroPortMapping
                {
                    ExposedPortId = exposedName,
                    DisplayName = exposedName,
                    Type = portType,
                    Direction = PortDirection.Input,
                    InternalNodeId = conn.TargetNodeId,
                    InternalPortId = conn.TargetPortId,
                    IsRequired = true
                });
            }
        }

        // 4. Map boundary outgoing connections to ExposedOutputs
        int outIdx = 1;
        var handledOutputs = new HashSet<(string, string)>();
        foreach (var conn in outgoingExternal)
        {
            if (handledOutputs.Add((conn.SourceNodeId, conn.SourcePortId)))
            {
                var srcNode = graph.GetNode(conn.SourceNodeId);
                var srcPort = srcNode?.Tool.Outputs.FirstOrDefault(p => p.Id.Equals(conn.SourcePortId, StringComparison.OrdinalIgnoreCase));
                var portType = srcPort?.Type ?? PortType.Any;

                string exposedName = srcPort != null ? $"{srcNode!.Title}_{srcPort.Name}" : $"Output_{outIdx++}";

                macro.ExposedOutputs.Add(new MacroPortMapping
                {
                    ExposedPortId = exposedName,
                    DisplayName = exposedName,
                    Type = portType,
                    Direction = PortDirection.Output,
                    InternalNodeId = conn.SourceNodeId,
                    InternalPortId = conn.SourcePortId
                });
            }
        }

        // If no external outputs were connected, expose outputs of leaf nodes
        if (macro.ExposedOutputs.Count == 0)
        {
            foreach (var nodeId in selectedSet)
            {
                var node = graph.GetNode(nodeId);
                if (node == null) continue;

                foreach (var outp in node.Tool.Outputs)
                {
                    macro.ExposedOutputs.Add(new MacroPortMapping
                    {
                        ExposedPortId = $"{node.Title}_{outp.Name}",
                        DisplayName = $"{node.Title} {outp.Name}",
                        Type = outp.Type,
                        Direction = PortDirection.Output,
                        InternalNodeId = node.Id,
                        InternalPortId = outp.Id
                    });
                }
            }
        }

        // 5. Expose primary parameters from internal nodes
        foreach (var nodeId in selectedSet)
        {
            var node = graph.GetNode(nodeId);
            if (node == null) continue;

            foreach (var p in node.Tool.Parameters)
            {
                macro.ExposedParameters.Add(new MacroParameterMapping
                {
                    ExposedParamName = $"{node.Id}_{p.Name}",
                    DisplayName = $"{node.Title} - {p.DisplayName}",
                    Type = p.Type,
                    InternalNodeId = node.Id,
                    InternalParamName = p.Name,
                    DefaultValue = p.Value,
                    Min = p.Min,
                    Max = p.Max,
                    Step = p.Step,
                    EnumOptions = p.EnumOptions?.ToArray(),
                    Description = p.Tooltip
                });
            }
        }

        macro.SerializedPipeline = subDoc;
        return macro;
    }
}
