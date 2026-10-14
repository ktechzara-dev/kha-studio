namespace VisionStudio.Connectivity.OpcUa;

public sealed class OpcUaServerConfig
{
    public int Port { get; set; } = 4840;
    public string ServerName { get; set; } = "VisionStudio Industrial Server";
    public string EndpointPath { get; set; } = "VisionStudio";
    public bool AllowAnonymous { get; set; } = true;
    public string SecurityPolicy { get; set; } = "None"; // None, Basic256Sha256
}

/// <summary>
/// Address space variable node within the industrial OPC UA hierarchy.
/// </summary>
public sealed class OpcUaNode
{
    public string NodeId { get; }
    public string BrowseName { get; }
    public Type DataType { get; }
    public bool IsWritable { get; }

    private object? _value;
    public object? Value
    {
        get => _value;
        set
        {
            if (!Equals(_value, value))
            {
                _value = value;
                Timestamp = DateTimeOffset.UtcNow;
                ValueChanged?.Invoke(this, value);
            }
        }
    }

    public DateTimeOffset Timestamp { get; private set; } = DateTimeOffset.UtcNow;
    public event Action<OpcUaNode, object?>? ValueChanged;

    public OpcUaNode(string nodeId, string browseName, object? initialValue, bool isWritable = false)
    {
        NodeId = nodeId;
        BrowseName = browseName;
        _value = initialValue;
        DataType = initialValue?.GetType() ?? typeof(string);
        IsWritable = isWritable;
    }
}
