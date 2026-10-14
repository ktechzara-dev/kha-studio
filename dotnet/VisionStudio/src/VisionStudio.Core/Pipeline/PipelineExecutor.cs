using System.Diagnostics;
using Microsoft.Extensions.Logging;
using VisionStudio.Core.Tools;

namespace VisionStudio.Core.Pipeline;

/// <summary>
/// Execution engine for running pipeline DAGs with per-node caching, step-through, and breakpoints.
/// </summary>
public sealed class PipelineExecutor
{
    private readonly ILogger? _logger;
    private long _executionCounter;

    public PipelineExecutor(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Executes the pipeline graph in topological order.
    /// </summary>
    /// <param name="graph">Validated pipeline graph.</param>
    /// <param name="stopAtBreakpoints">Whether to pause execution when a node with a breakpoint is hit.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="externalInputs">Optional pre-seeded inputs mapped to specific internal nodes and ports.</param>
    public async Task<PipelineExecutionResult> ExecuteAsync(
        PipelineGraph graph,
        bool stopAtBreakpoints = false,
        CancellationToken ct = default,
        IReadOnlyDictionary<(string NodeId, string PortId), object?>? externalInputs = null)
    {
        var validation = graph.Validate();
        if (!validation.IsValid)
        {
            var msg = string.Join("; ", validation.Errors);
            _logger?.LogError("Pipeline validation failed: {Errors}", msg);
            return new PipelineExecutionResult(false, TimeSpan.Zero, new Dictionary<string, ToolResult>(), null, msg);
        }

        var sw = Stopwatch.StartNew();
        var executionIndex = Interlocked.Increment(ref _executionCounter);
        var nodes = graph.GetTopologicalOrder();
        var nodeResults = new Dictionary<string, ToolResult>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in nodes)
        {
            ct.ThrowIfCancellationRequested();

            if (!node.IsEnabled)
            {
                var skipped = ToolResult.Skipped($"Node '{node.Title}' is disabled.");
                nodeResults[node.Id] = skipped;
                continue;
            }

            // Breakpoint check
            if (stopAtBreakpoints && node.HasBreakpoint)
            {
                _logger?.LogInformation("Execution paused at breakpoint on node '{NodeId}' ({Title}).", node.Id, node.Title);
                return new PipelineExecutionResult(
                    Success: true,
                    TotalTime: sw.Elapsed,
                    NodeResults: nodeResults,
                    StoppedNodeId: node.Id);
            }

            // Check cache
            if (!node.IsDirty && node.CachedResult != null)
            {
                _logger?.LogDebug("Reusing cached result for clean node '{NodeId}'.", node.Id);
                nodeResults[node.Id] = node.CachedResult;
                continue;
            }

            // Assemble inputs from upstream connections and external mappings
            var inputs = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            if (externalInputs != null)
            {
                foreach (var kvp in externalInputs)
                {
                    if (kvp.Key.NodeId.Equals(node.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        inputs[kvp.Key.PortId] = kvp.Value;
                    }
                }
            }

            var incomingConnections = graph.Connections.Where(c => c.TargetNodeId.Equals(node.Id, StringComparison.OrdinalIgnoreCase));

            foreach (var conn in incomingConnections)
            {
                if (nodeResults.TryGetValue(conn.SourceNodeId, out var srcResult))
                {
                    if (srcResult.Outputs.TryGetValue(conn.SourcePortId, out var val))
                    {
                        inputs[conn.TargetPortId] = val;
                    }
                }
            }

            var ctx = new ToolContext(inputs, ct, executionIndex, _logger);
            var nodeSw = Stopwatch.StartNew();

            ToolResult result;
            try
            {
                // Execute off the current caller thread if desired, or directly with cancellation
                result = await Task.Run(() => node.Tool.Execute(ctx, ct), ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Node '{NodeId}' ({Title}) failed execution: {Message}", node.Id, node.Title, ex.Message);
                result = ToolResult.Failed($"Execution exception: {ex.Message}", ex, nodeSw.Elapsed);
            }

            nodeResults[node.Id] = result;
            node.CachedResult = result;
            node.IsDirty = false;

            if (result.Status == ToolStatus.Error)
            {
                _logger?.LogWarning("Node '{NodeId}' yielded error status. Halting pipeline execution.", node.Id);
                return new PipelineExecutionResult(
                    Success: false,
                    TotalTime: sw.Elapsed,
                    NodeResults: nodeResults,
                    StoppedNodeId: node.Id,
                    ErrorMessage: result.Message ?? "Node failed");
            }
        }

        sw.Stop();
        return new PipelineExecutionResult(true, sw.Elapsed, nodeResults);
    }
}
