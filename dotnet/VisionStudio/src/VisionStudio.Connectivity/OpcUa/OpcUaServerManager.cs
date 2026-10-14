using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using VisionStudio.Core.Traceability;

namespace VisionStudio.Connectivity.OpcUa;

/// <summary>
/// Industrial OPC UA Server and Address Space Manager.
/// Exposes inspection results, yield counters, and measurements as nodes,
/// while providing writable command nodes for PLC and MES synchronization.
/// </summary>
public sealed class OpcUaServerManager : IAsyncDisposable
{
    private readonly OpcUaServerConfig _config;
    private readonly ILogger? _logger;
    private readonly ConcurrentDictionary<string, OpcUaNode> _nodes = new(StringComparer.OrdinalIgnoreCase);
    private TcpListener? _listener;
    private CancellationTokenSource? _serverCts;

    public IReadOnlyCollection<OpcUaNode> Nodes => _nodes.Values.ToList();
    public bool IsRunning => _listener != null;

    public OpcUaServerManager(OpcUaServerConfig? config = null, ILogger? logger = null)
    {
        _config = config ?? new OpcUaServerConfig();
        _logger = logger;
        InitializeStandardAddressSpace();
    }

    private void InitializeStandardAddressSpace()
    {
        CreateNode("ns=2;s=Station.Status", "Status", "Idle");
        CreateNode("ns=2;s=Station.Verdict", "Verdict", "Pass");
        CreateNode("ns=2;s=Station.TotalCount", "TotalCount", 0L);
        CreateNode("ns=2;s=Station.PassCount", "PassCount", 0L);
        CreateNode("ns=2;s=Station.FailCount", "FailCount", 0L);
        CreateNode("ns=2;s=Station.CycleTimeMs", "CycleTimeMs", 0.0);
        CreateNode("ns=2;s=Station.ActiveRecipe", "ActiveRecipe", "Default");

        // Commands (Writable by PLC/MES)
        CreateNode("ns=2;s=Commands.Trigger", "Trigger", false, isWritable: true);
        CreateNode("ns=2;s=Commands.ResetCounters", "ResetCounters", false, isWritable: true);
        CreateNode("ns=2;s=Commands.TargetRecipe", "TargetRecipe", string.Empty, isWritable: true);
    }

    public OpcUaNode CreateNode(string nodeId, string browseName, object? initialValue, bool isWritable = false)
    {
        var node = new OpcUaNode(nodeId, browseName, initialValue, isWritable);
        _nodes[nodeId] = node;
        return node;
    }

    public OpcUaNode? GetNode(string nodeId)
    {
        _nodes.TryGetValue(nodeId, out var node);
        return node;
    }

    public void UpdateNodeValue(string nodeId, object? value)
    {
        if (_nodes.TryGetValue(nodeId, out var node))
        {
            node.Value = value;
        }
    }

    public object? ReadNodeValue(string nodeId)
    {
        return _nodes.TryGetValue(nodeId, out var node) ? node.Value : null;
    }

    /// <summary>
    /// Synchronizes OPC UA address space with an inspection outcome from the vision pipeline.
    /// </summary>
    public void UpdateFromInspection(InspectionRecord record)
    {
        UpdateNodeValue("ns=2;s=Station.Verdict", record.Verdict.ToString());
        UpdateNodeValue("ns=2;s=Station.CycleTimeMs", record.ExecutionTimeMs);
        UpdateNodeValue("ns=2;s=Station.ActiveRecipe", record.RecipeName);

        long total = Convert.ToInt64(ReadNodeValue("ns=2;s=Station.TotalCount") ?? 0L) + 1;
        UpdateNodeValue("ns=2;s=Station.TotalCount", total);

        if (record.Verdict == InspectionVerdict.Pass)
        {
            long pass = Convert.ToInt64(ReadNodeValue("ns=2;s=Station.PassCount") ?? 0L) + 1;
            UpdateNodeValue("ns=2;s=Station.PassCount", pass);
        }
        else
        {
            long fail = Convert.ToInt64(ReadNodeValue("ns=2;s=Station.FailCount") ?? 0L) + 1;
            UpdateNodeValue("ns=2;s=Station.FailCount", fail);
        }

        // Expose dynamic measurement values as nodes
        foreach (var (k, v) in record.Measurements)
        {
            string mNodeId = $"ns=2;s=Measurements.{k}";
            if (!_nodes.ContainsKey(mNodeId))
            {
                CreateNode(mNodeId, k, v);
            }
            else
            {
                UpdateNodeValue(mNodeId, v);
            }
        }
    }

    /// <summary>
    /// Starts the in-process OPC UA endpoint listener.
    /// </summary>
    public async Task StartAsync(int? overridePort = null)
    {
        int port = overridePort ?? _config.Port;
        _serverCts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Loopback, port);
        _listener.Start();

        _ = Task.Run(() => ListenClientsLoop(_listener, _serverCts.Token));
        _logger?.LogInformation("OPC UA Server started on opc.tcp://localhost:{Port}/{Path}", port, _config.EndpointPath);
    }

    private async Task ListenClientsLoop(TcpListener listener, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var client = await listener.AcceptTcpClientAsync(ct);
                _ = Task.Run(() => HandleClientAsync(client, ct), ct);
            }
            catch when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error accepting OPC UA TCP client.");
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        using (client)
        using (var stream = client.GetStream())
        using (var reader = new StreamReader(stream, Encoding.UTF8))
        using (var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true })
        {
            while (!ct.IsCancellationRequested && client.Connected)
            {
                string? line = await reader.ReadLineAsync(ct);
                if (line == null) break;

                // Simple JSON-RPC-like command protocol for in-process client queries:
                // READ <nodeId> -> {"status":"ok","value":...}
                // WRITE <nodeId> <value> -> {"status":"ok"}
                if (line.StartsWith("READ ", StringComparison.OrdinalIgnoreCase))
                {
                    string nodeId = line[5..].Trim();
                    object? val = ReadNodeValue(nodeId);
                    await writer.WriteLineAsync(JsonSerializer.Serialize(new { status = "ok", value = val }));
                }
                else if (line.StartsWith("WRITE ", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = line[6..].Split(' ', 2);
                    if (parts.Length == 2)
                    {
                        string nodeId = parts[0].Trim();
                        string rawVal = parts[1].Trim();
                        if (_nodes.TryGetValue(nodeId, out var node) && node.IsWritable)
                        {
                            node.Value = rawVal;
                            await writer.WriteLineAsync(JsonSerializer.Serialize(new { status = "ok" }));
                        }
                        else
                        {
                            await writer.WriteLineAsync(JsonSerializer.Serialize(new { status = "error", message = "Node not found or read-only" }));
                        }
                    }
                }
            }
        }
    }

    public async Task StopAsync()
    {
        if (_serverCts != null)
        {
            _serverCts.Cancel();
            _listener?.Stop();
            _listener = null;
            _serverCts = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }
}
