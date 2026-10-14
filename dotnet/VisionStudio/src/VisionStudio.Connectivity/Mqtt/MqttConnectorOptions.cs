namespace VisionStudio.Connectivity.Mqtt;

/// <summary>
/// Configuration parameters for industrial MQTT connectivity.
/// </summary>
public sealed class MqttConnectorOptions
{
    public string BrokerHost { get; set; } = "localhost";
    public int BrokerPort { get; set; } = 1883;
    public string ClientId { get; set; } = "VisionStudio_Inspection_Station_01";
    public string TopicPrefix { get; set; } = "visionstudio/station_01";
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool UseTls { get; set; } = false;
    public int QosLevel { get; set; } = 1; // 0, 1, or 2
    public bool UseSparkplugB { get; set; } = false;
}

public sealed record SparkplugMetric(string Name, string Type, object? Value, long Timestamp);

public sealed record SparkplugPayload(long Timestamp, IReadOnlyList<SparkplugMetric> Metrics);
