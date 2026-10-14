using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace VisionStudio.Connectivity.OpcUa;

/// <summary>
/// Industrial OPC UA Client used to read process tags, write inspection verdicts,
/// and trigger external PLC registers or MES synchronization.
/// </summary>
public sealed class OpcUaClientConnector : IAsyncDisposable
{
    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private readonly OpcUaServerManager? _inProcessServer;

    public bool IsConnected => _tcpClient != null && _tcpClient.Connected || _inProcessServer != null;

    /// <summary>
    /// Connects directly to an in-process OPC UA Server instance for ultra-fast, zero-overhead testing and local IPC.
    /// </summary>
    public OpcUaClientConnector(OpcUaServerManager inProcessServer)
    {
        _inProcessServer = inProcessServer ?? throw new ArgumentNullException(nameof(inProcessServer));
    }

    /// <summary>
    /// Creates a network client to connect over TCP/IP to a remote or local OPC UA server.
    /// </summary>
    public OpcUaClientConnector()
    {
    }

    public async Task ConnectAsync(string host = "localhost", int port = 4840, CancellationToken ct = default)
    {
        if (_inProcessServer != null) return;

        _tcpClient = new TcpClient();
        await _tcpClient.ConnectAsync(host, port, ct);
        _stream = _tcpClient.GetStream();
        _reader = new StreamReader(_stream, Encoding.UTF8);
        _writer = new StreamWriter(_stream, Encoding.UTF8) { AutoFlush = true };
    }

    public async Task<T?> ReadNodeAsync<T>(string nodeId, CancellationToken ct = default)
    {
        if (_inProcessServer != null)
        {
            var raw = _inProcessServer.ReadNodeValue(nodeId);
            if (raw == null) return default;
            if (raw is T typed) return typed;
            return (T)Convert.ChangeType(raw, typeof(T));
        }

        if (_writer == null || _reader == null) throw new InvalidOperationException("OPC UA Client is not connected.");

        await _writer.WriteLineAsync($"READ {nodeId}");
        string? resp = await _reader.ReadLineAsync(ct);
        if (resp == null) return default;

        using var doc = JsonDocument.Parse(resp);
        if (doc.RootElement.TryGetProperty("value", out var valProp))
        {
            if (typeof(T) == typeof(string)) return (T)(object)valProp.GetString()!;
            if (typeof(T) == typeof(int)) return (T)(object)valProp.GetInt32();
            if (typeof(T) == typeof(long)) return (T)(object)valProp.GetInt64();
            if (typeof(T) == typeof(double)) return (T)(object)valProp.GetDouble();
            if (typeof(T) == typeof(bool)) return (T)(object)valProp.GetBoolean();
        }

        return default;
    }

    public async Task<bool> WriteNodeAsync<T>(string nodeId, T value, CancellationToken ct = default)
    {
        if (_inProcessServer != null)
        {
            var node = _inProcessServer.GetNode(nodeId);
            if (node != null && node.IsWritable)
            {
                node.Value = value;
                return true;
            }
            return false;
        }

        if (_writer == null || _reader == null) throw new InvalidOperationException("OPC UA Client is not connected.");

        await _writer.WriteLineAsync($"WRITE {nodeId} {value}");
        string? resp = await _reader.ReadLineAsync(ct);
        if (resp == null) return false;

        using var doc = JsonDocument.Parse(resp);
        return doc.RootElement.TryGetProperty("status", out var st) && st.GetString() == "ok";
    }

    public async Task DisconnectAsync()
    {
        if (_writer != null) { await _writer.DisposeAsync(); _writer = null; }
        _reader?.Dispose(); _reader = null;
        if (_stream != null) { await _stream.DisposeAsync(); _stream = null; }
        _tcpClient?.Dispose(); _tcpClient = null;
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }
}
