using System.Text.Json;
using System.Text.Json.Serialization;
using VisionStudio.Core.Tools;

namespace VisionStudio.Core.Pipeline;

/// <summary>
/// Serializable DTO representing a serialized node in a pipeline.
/// </summary>
public sealed class SerializedNodeDto
{
    public string Id { get; set; } = string.Empty;
    public string ToolId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool HasBreakpoint { get; set; }
    public Dictionary<string, object?> Parameters { get; set; } = new();
}

/// <summary>
/// Serializable DTO representing a serialized port connection.
/// </summary>
public sealed class SerializedConnectionDto
{
    public string SourceNodeId { get; set; } = string.Empty;
    public string SourcePortId { get; set; } = string.Empty;
    public string TargetNodeId { get; set; } = string.Empty;
    public string TargetPortId { get; set; } = string.Empty;
}

/// <summary>
/// Full versioned pipeline schema document.
/// </summary>
public sealed class SerializedPipelineDocument
{
    public string SchemaVersion { get; set; } = "1.0.0";
    public string Name { get; set; } = "Untitled Pipeline";
    public string Description { get; set; } = string.Empty;
    public DateTimeOffset SavedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<SerializedNodeDto> Nodes { get; set; } = new();
    public List<SerializedConnectionDto> Connections { get; set; } = new();
}

/// <summary>
/// Handles versioned JSON serialization and deserialization of vision pipeline DAGs.
/// </summary>
public sealed class PipelineSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Serializes a pipeline graph into a SerializedPipelineDocument DTO.
    /// </summary>
    public SerializedPipelineDocument SerializeToDocument(PipelineGraph graph, string pipelineName = "Vision Pipeline", string description = "")
    {
        var doc = new SerializedPipelineDocument
        {
            SchemaVersion = "1.0.0",
            Name = pipelineName,
            Description = description,
            SavedAt = DateTimeOffset.UtcNow
        };

        foreach (var node in graph.Nodes)
        {
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

            foreach (var param in node.Tool.Parameters)
            {
                nodeDto.Parameters[param.Name] = param.Value;
            }

            doc.Nodes.Add(nodeDto);
        }

        foreach (var conn in graph.Connections)
        {
            doc.Connections.Add(new SerializedConnectionDto
            {
                SourceNodeId = conn.SourceNodeId,
                SourcePortId = conn.SourcePortId,
                TargetNodeId = conn.TargetNodeId,
                TargetPortId = conn.TargetPortId
            });
        }

        return doc;
    }

    /// <summary>
    /// Serializes a pipeline graph into formatted JSON.
    /// </summary>
    public string SerializeToJson(PipelineGraph graph, string pipelineName = "Vision Pipeline", string description = "")
    {
        var doc = SerializeToDocument(graph, pipelineName, description);
        return JsonSerializer.Serialize(doc, JsonOptions);
    }

    /// <summary>
    /// Deserializes JSON into a fully restored PipelineGraph using the provided ToolRegistry.
    /// </summary>
    public PipelineGraph DeserializeFromJson(string json, ToolRegistry registry)
    {
        var doc = JsonSerializer.Deserialize<SerializedPipelineDocument>(json, JsonOptions);
        if (doc == null)
        {
            throw new InvalidOperationException("Failed to deserialize pipeline JSON document.");
        }

        return DeserializeFromDocument(doc, registry);
    }

    /// <summary>
    /// Restores a PipelineGraph directly from a SerializedPipelineDocument.
    /// </summary>
    public PipelineGraph DeserializeFromDocument(SerializedPipelineDocument doc, ToolRegistry registry)
    {
        if (doc == null) throw new ArgumentNullException(nameof(doc));

        // Schema migration hook if necessary
        if (doc.SchemaVersion != "1.0.0")
        {
            // Future migration versions handled here
        }

        var graph = new PipelineGraph();

        foreach (var nodeDto in doc.Nodes)
        {
            var toolInstance = registry.CreateInstance(nodeDto.ToolId);

            // Restore parameters
            if (nodeDto.Parameters != null)
            {
                foreach (var (pName, pVal) in nodeDto.Parameters)
                {
                    var targetParam = toolInstance.Parameters.FirstOrDefault(p => p.Name.Equals(pName, StringComparison.OrdinalIgnoreCase));
                    if (targetParam != null && pVal != null)
                    {
                        if (pVal is JsonElement je)
                        {
                            targetParam.Value = ParseJsonElement(je, targetParam.Type);
                        }
                        else
                        {
                            targetParam.Value = pVal;
                        }
                    }
                }
            }

            var node = new PipelineNode(nodeDto.Id, toolInstance, nodeDto.Title, nodeDto.X, nodeDto.Y)
            {
                IsEnabled = nodeDto.IsEnabled,
                HasBreakpoint = nodeDto.HasBreakpoint
            };

            graph.AddNode(node);
        }

        foreach (var connDto in doc.Connections)
        {
            graph.AddConnection(new PipelineConnection(
                connDto.SourceNodeId,
                connDto.SourcePortId,
                connDto.TargetNodeId,
                connDto.TargetPortId));
        }

        return graph;
    }

    private static object? ParseJsonElement(JsonElement element, Parameters.ParameterType type)
    {
        return type switch
        {
            Parameters.ParameterType.Integer => element.TryGetInt32(out var i) ? i : (int)element.GetDouble(),
            Parameters.ParameterType.Double => element.GetDouble(),
            Parameters.ParameterType.Boolean => element.GetBoolean(),
            Parameters.ParameterType.String => element.GetString(),
            Parameters.ParameterType.FilePath => element.GetString(),
            Parameters.ParameterType.Enum => element.GetString(),
            _ => element.GetString()
        };
    }
}
