using VisionStudio.Core.Ports;

namespace VisionStudio.Core.Pipeline;

/// <summary>
/// Result of pipeline graph validation.
/// </summary>
public sealed record PipelineValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors);

/// <summary>
/// Directed Acyclic Graph (DAG) managing pipeline nodes, connections, dependency analysis and invalidation.
/// </summary>
public sealed class PipelineGraph
{
    private readonly Dictionary<string, PipelineNode> _nodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<PipelineConnection> _connections = new();

    public IReadOnlyCollection<PipelineNode> Nodes => _nodes.Values;
    public IReadOnlyList<PipelineConnection> Connections => _connections;

    public void AddNode(PipelineNode node)
    {
        if (_nodes.ContainsKey(node.Id))
        {
            throw new InvalidOperationException($"Node with ID '{node.Id}' already exists in the pipeline.");
        }

        _nodes[node.Id] = node;
        node.NodeDirtied += OnNodeDirtied;
    }

    public bool RemoveNode(string nodeId)
    {
        if (_nodes.TryGetValue(nodeId, out var node))
        {
            node.NodeDirtied -= OnNodeDirtied;
            _nodes.Remove(nodeId);
            _connections.RemoveAll(c => c.SourceNodeId.Equals(nodeId, StringComparison.OrdinalIgnoreCase) ||
                                        c.TargetNodeId.Equals(nodeId, StringComparison.OrdinalIgnoreCase));
            return true;
        }
        return false;
    }

    public PipelineNode? GetNode(string nodeId)
    {
        _nodes.TryGetValue(nodeId, out var node);
        return node;
    }

    public void AddConnection(PipelineConnection connection)
    {
        // Check for duplicates
        if (_connections.Any(c => c.SourceNodeId.Equals(connection.SourceNodeId, StringComparison.OrdinalIgnoreCase) &&
                                 c.SourcePortId.Equals(connection.SourcePortId, StringComparison.OrdinalIgnoreCase) &&
                                 c.TargetNodeId.Equals(connection.TargetNodeId, StringComparison.OrdinalIgnoreCase) &&
                                 c.TargetPortId.Equals(connection.TargetPortId, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        _connections.Add(connection);

        // Mark target and its downstream as dirty
        if (_nodes.TryGetValue(connection.TargetNodeId, out var targetNode))
        {
            targetNode.MarkDirty();
        }
    }

    public bool RemoveConnection(PipelineConnection connection)
    {
        var removed = _connections.Remove(connection);
        if (removed && _nodes.TryGetValue(connection.TargetNodeId, out var targetNode))
        {
            targetNode.MarkDirty();
        }
        return removed;
    }

    private void OnNodeDirtied(PipelineNode dirtyNode)
    {
        // Invalidate all downstream nodes reachable from this dirty node
        var queue = new Queue<string>();
        queue.Enqueue(dirtyNode.Id);

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { dirtyNode.Id };

        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();
            var outgoing = _connections.Where(c => c.SourceNodeId.Equals(currentId, StringComparison.OrdinalIgnoreCase));

            foreach (var conn in outgoing)
            {
                if (_nodes.TryGetValue(conn.TargetNodeId, out var downstreamNode))
                {
                    downstreamNode.IsDirty = true;
                    if (visited.Add(downstreamNode.Id))
                    {
                        queue.Enqueue(downstreamNode.Id);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Validates graph structure, port compatibility, and cycle detection.
    /// </summary>
    public PipelineValidationResult Validate()
    {
        var errors = new List<string>();

        // 1. Validate connection endpoints and types
        foreach (var conn in _connections)
        {
            if (!_nodes.TryGetValue(conn.SourceNodeId, out var srcNode))
            {
                errors.Add($"Connection source node '{conn.SourceNodeId}' not found.");
                continue;
            }

            if (!_nodes.TryGetValue(conn.TargetNodeId, out var dstNode))
            {
                errors.Add($"Connection target node '{conn.TargetNodeId}' not found.");
                continue;
            }

            var srcPort = srcNode.Tool.Outputs.FirstOrDefault(p => p.Id.Equals(conn.SourcePortId, StringComparison.OrdinalIgnoreCase));
            if (srcPort == null)
            {
                errors.Add($"Source node '{conn.SourceNodeId}' has no output port '{conn.SourcePortId}'.");
            }

            var dstPort = dstNode.Tool.Inputs.FirstOrDefault(p => p.Id.Equals(conn.TargetPortId, StringComparison.OrdinalIgnoreCase));
            if (dstPort == null)
            {
                errors.Add($"Target node '{conn.TargetNodeId}' has no input port '{conn.TargetPortId}'.");
            }

            if (srcPort != null && dstPort != null && !srcPort.IsCompatibleWith(dstPort))
            {
                errors.Add($"Type mismatch: Cannot connect port {srcPort.Name} ({srcPort.Type}) to {dstPort.Name} ({dstPort.Type}).");
            }
        }

        // 2. Cycle detection via DFS
        if (HasCycle())
        {
            errors.Add("Cycle detected in pipeline graph. Pipelines must be Directed Acyclic Graphs (DAG).");
        }

        return new PipelineValidationResult(errors.Count == 0, errors);
    }

    /// <summary>
    /// Checks for cycles in the graph using DFS 3-state coloring.
    /// </summary>
    public bool HasCycle()
    {
        // 0 = unvisited, 1 = visiting, 2 = visited
        var state = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in _nodes.Keys)
        {
            state[node] = 0;
        }

        foreach (var nodeId in _nodes.Keys)
        {
            if (state[nodeId] == 0)
            {
                if (DfsCheckCycle(nodeId, state))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool DfsCheckCycle(string currentId, Dictionary<string, int> state)
    {
        state[currentId] = 1; // Visiting

        var outgoing = _connections.Where(c => c.SourceNodeId.Equals(currentId, StringComparison.OrdinalIgnoreCase));
        foreach (var conn in outgoing)
        {
            if (!state.TryGetValue(conn.TargetNodeId, out var targetState)) continue;

            if (targetState == 1)
            {
                return true; // Back-edge detected
            }

            if (targetState == 0 && DfsCheckCycle(conn.TargetNodeId, state))
            {
                return true;
            }
        }

        state[currentId] = 2; // Visited
        return false;
    }

    /// <summary>
    /// Returns nodes sorted in topological execution order.
    /// </summary>
    public IReadOnlyList<PipelineNode> GetTopologicalOrder()
    {
        var inDegree = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in _nodes.Keys)
        {
            inDegree[node] = 0;
        }

        foreach (var conn in _connections)
        {
            if (inDegree.ContainsKey(conn.TargetNodeId))
            {
                inDegree[conn.TargetNodeId]++;
            }
        }

        var queue = new Queue<string>(_nodes.Keys.Where(k => inDegree[k] == 0));
        var result = new List<PipelineNode>();

        while (queue.Count > 0)
        {
            var u = queue.Dequeue();
            if (_nodes.TryGetValue(u, out var node))
            {
                result.Add(node);
            }

            var outgoing = _connections.Where(c => c.SourceNodeId.Equals(u, StringComparison.OrdinalIgnoreCase));
            foreach (var conn in outgoing)
            {
                if (inDegree.ContainsKey(conn.TargetNodeId))
                {
                    inDegree[conn.TargetNodeId]--;
                    if (inDegree[conn.TargetNodeId] == 0)
                    {
                        queue.Enqueue(conn.TargetNodeId);
                    }
                }
            }
        }

        if (result.Count != _nodes.Count)
        {
            throw new InvalidOperationException("Graph contains cycles or invalid dependencies; topological sort failed.");
        }

        return result;
    }
}
