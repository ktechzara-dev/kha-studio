using VisionStudio.Core.Tools;

namespace VisionStudio.Core.Pipeline;

/// <summary>
/// Directed data link between an upstream output port and a downstream input port.
/// </summary>
public sealed record PipelineConnection(
    string SourceNodeId,
    string SourcePortId,
    string TargetNodeId,
    string TargetPortId);

/// <summary>
/// A node in the pipeline DAG containing a concrete tool instance and execution state.
/// </summary>
public sealed class PipelineNode
{
    public string Id { get; }
    public string ToolId { get; }
    public string Title { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool HasBreakpoint { get; set; } = false;

    public IVisionTool Tool { get; }

    /// <summary>
    /// Cached result of the last execution for rapid downstream re-execution.
    /// </summary>
    public ToolResult? CachedResult { get; set; }

    /// <summary>
    /// Indicates whether parameters or upstream inputs have changed and need re-calculation.
    /// </summary>
    public bool IsDirty { get; set; } = true;

    public event Action<PipelineNode>? NodeDirtied;

    public PipelineNode(string id, IVisionTool tool, string? title = null, double x = 0, double y = 0)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Tool = tool ?? throw new ArgumentNullException(nameof(tool));
        ToolId = tool.Id;
        Title = title ?? tool.DisplayName;
        X = x;
        Y = y;

        // Invalidate cache when any parameter changes
        foreach (var param in Tool.Parameters)
        {
            param.ValueChanged += (_, _) => MarkDirty();
        }
    }

    public void MarkDirty()
    {
        IsDirty = true;
        NodeDirtied?.Invoke(this);
    }
}
