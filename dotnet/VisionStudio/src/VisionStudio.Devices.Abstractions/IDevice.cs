namespace VisionStudio.Devices.Abstractions;

/// <summary>
/// Status of an industrial device connection.
/// </summary>
public enum DeviceConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting,
    Faulted
}

/// <summary>
/// Common contract for all industrial hardware & simulated devices.
/// </summary>
public interface IDevice : IAsyncDisposable
{
    string Id { get; }
    string Name { get; }
    DeviceConnectionState State { get; }
    Task ConnectAsync(CancellationToken ct = default);
    Task DisconnectAsync(CancellationToken ct = default);
}
