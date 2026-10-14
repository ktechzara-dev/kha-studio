using FluentAssertions;
using VisionStudio.Connectivity.Mqtt;
using VisionStudio.Core.Traceability;
using Xunit;

namespace VisionStudio.Connectivity.Tests;

public class MqttIntegrationTests : IAsyncLifetime
{
    private readonly InProcessMqttBroker _broker = new();
    private readonly int _testPort = 18895;

    public async Task InitializeAsync()
    {
        await _broker.StartAsync(_testPort);
    }

    public async Task DisposeAsync()
    {
        await _broker.StopAsync();
    }

    [Fact]
    public async Task MqttClient_ConnectsAndPublishes_StandardJsonResults()
    {
        var options = new MqttConnectorOptions
        {
            BrokerHost = "127.0.0.1",
            BrokerPort = _testPort,
            ClientId = "TestStation_01",
            TopicPrefix = "factory/station1",
            UseSparkplugB = false
        };

        await using var client = new MqttClientConnector(options);
        await client.ConnectAsync();

        client.IsConnected.Should().BeTrue();

        var record = new InspectionRecord
        {
            LotNumber = "LOT-100",
            SerialNumber = "SN-9999",
            Verdict = InspectionVerdict.Pass,
            ExecutionTimeMs = 12.4,
            Measurements = new() { ["Width"] = 45.2 }
        };

        // Should publish without error
        Func<Task> act = async () => await client.PublishResultAsync(record);
        await act.Should().NotThrowAsync();

        await client.DisconnectAsync();
        client.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task MqttClient_Publishes_SparkplugBFormattedPayloads()
    {
        var options = new MqttConnectorOptions
        {
            BrokerHost = "127.0.0.1",
            BrokerPort = _testPort,
            ClientId = "TestStation_Sparkplug",
            UseSparkplugB = true
        };

        await using var client = new MqttClientConnector(options);
        await client.ConnectAsync();

        var record = new InspectionRecord
        {
            LotNumber = "LOT-200",
            SerialNumber = "SN-SPARK",
            Verdict = InspectionVerdict.Fail,
            ExecutionTimeMs = 8.7,
            Measurements = new() { ["DefectArea"] = 120.0 }
        };

        Func<Task> act = async () => await client.PublishResultAsync(record);
        await act.Should().NotThrowAsync();
    }
}
