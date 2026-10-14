using MQTTnet;
using MQTTnet.Server;

namespace VisionStudio.Connectivity.Mqtt;

/// <summary>
/// Lightweight in-process embedded MQTT Broker used for isolated unit/integration tests and local development.
/// </summary>
public sealed class InProcessMqttBroker : IAsyncDisposable
{
    private MqttServer? _server;
    public int Port { get; private set; }
    public bool IsRunning => _server != null && _server.IsStarted;

    public async Task StartAsync(int port = 18883)
    {
        Port = port;
        var options = new MqttServerOptionsBuilder()
            .WithDefaultEndpoint()
            .WithDefaultEndpointPort(port)
            .WithDefaultEndpointBoundIPAddress(System.Net.IPAddress.Loopback)
            .Build();

        _server = new MqttFactory().CreateMqttServer(options);
        await _server.StartAsync();
    }

    public async Task StopAsync()
    {
        if (_server != null)
        {
            await _server.StopAsync();
            _server.Dispose();
            _server = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }
}
