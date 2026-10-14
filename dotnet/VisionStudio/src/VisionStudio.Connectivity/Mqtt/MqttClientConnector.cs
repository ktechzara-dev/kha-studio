using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using VisionStudio.Core.Traceability;

namespace VisionStudio.Connectivity.Mqtt;

/// <summary>
/// Industrial MQTT client publishing inspection telemetry, quality verdicts, measurements,
/// and subscribing to remote line control commands with optional Sparkplug B payload format.
/// </summary>
public sealed class MqttClientConnector : IAsyncDisposable
{
    private readonly MqttConnectorOptions _options;
    private readonly ILogger? _logger;
    private IMqttClient? _client;
    private Action<string, string>? _commandHandler;

    public bool IsConnected => _client != null && _client.IsConnected;

    public MqttClientConnector(MqttConnectorOptions options, ILogger? logger = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        var factory = new MqttFactory();
        _client = factory.CreateMqttClient();

        var builder = new MqttClientOptionsBuilder()
            .WithTcpServer(_options.BrokerHost, _options.BrokerPort)
            .WithClientId(_options.ClientId)
            .WithCleanSession();

        if (!string.IsNullOrEmpty(_options.Username))
        {
            builder.WithCredentials(_options.Username, _options.Password);
        }

        if (_options.UseTls)
        {
            builder.WithTlsOptions(o => o.UseTls());
        }

        _client.ApplicationMessageReceivedAsync += e =>
        {
            string topic = e.ApplicationMessage.Topic;
            string payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);
            _logger?.LogDebug("MQTT message received on '{Topic}': {Payload}", topic, payload);
            _commandHandler?.Invoke(topic, payload);
            return Task.CompletedTask;
        };

        var clientOptions = builder.Build();
        await _client.ConnectAsync(clientOptions, ct);
        _logger?.LogInformation("Connected to MQTT Broker at {Host}:{Port}", _options.BrokerHost, _options.BrokerPort);

        // Auto subscribe to command topic
        string cmdTopic = $"{_options.TopicPrefix.TrimEnd('/')}/commands/#";
        await _client.SubscribeAsync(cmdTopic, (MqttQualityOfServiceLevel)_options.QosLevel, ct);
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        if (_client != null && _client.IsConnected)
        {
            await _client.DisconnectAsync(new MqttClientDisconnectOptions(), ct);
            _client.Dispose();
            _client = null;
        }
    }

    public async Task PublishResultAsync(InspectionRecord record, CancellationToken ct = default)
    {
        if (_client == null || !_client.IsConnected)
        {
            throw new InvalidOperationException("MQTT client is not connected to broker.");
        }

        string topic;
        string payloadJson;

        if (_options.UseSparkplugB)
        {
            topic = $"spBv1.0/VisionStudio/DDATA/{_options.ClientId}";
            var metrics = new List<SparkplugMetric>
            {
                new("SequenceNumber", "Int64", record.SequenceNumber, record.Timestamp.ToUnixTimeMilliseconds()),
                new("LotNumber", "String", record.LotNumber, record.Timestamp.ToUnixTimeMilliseconds()),
                new("SerialNumber", "String", record.SerialNumber, record.Timestamp.ToUnixTimeMilliseconds()),
                new("Verdict", "String", record.Verdict.ToString(), record.Timestamp.ToUnixTimeMilliseconds()),
                new("ExecutionTimeMs", "Double", record.ExecutionTimeMs, record.Timestamp.ToUnixTimeMilliseconds())
            };

            foreach (var (k, v) in record.Measurements)
            {
                metrics.Add(new($"Measurements/{k}", "Double", v, record.Timestamp.ToUnixTimeMilliseconds()));
            }

            var spPayload = new SparkplugPayload(record.Timestamp.ToUnixTimeMilliseconds(), metrics);
            payloadJson = JsonSerializer.Serialize(spPayload);
        }
        else
        {
            topic = $"{_options.TopicPrefix.TrimEnd('/')}/results/{record.Verdict}";
            payloadJson = JsonSerializer.Serialize(record);
        }

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payloadJson)
            .WithQualityOfServiceLevel((MqttQualityOfServiceLevel)_options.QosLevel)
            .Build();

        await _client.PublishAsync(message, ct);
        _logger?.LogDebug("Published inspection record #{Seq} to MQTT topic '{Topic}'", record.SequenceNumber, topic);
    }

    public void RegisterCommandHandler(Action<string, string> handler)
    {
        _commandHandler = handler;
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }
}
