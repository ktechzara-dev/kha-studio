using VisionStudio.Core.Tools;

namespace VisionStudio.Core.Pipeline;

/// <summary>
/// Execution state summary of a pipeline run.
/// </summary>
public sealed record PipelineExecutionResult(
    bool Success,
    TimeSpan TotalTime,
    IReadOnlyDictionary<string, ToolResult> NodeResults,
    string? StoppedNodeId = null,
    string? ErrorMessage = null);
