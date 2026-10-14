namespace VisionStudio.Devices.Abstractions;

/// <summary>
/// Trigger modes supported by cameras.
/// </summary>
public enum CameraTriggerMode
{
    FreeRun,
    Software,
    HardwareLine
}



/// <summary>
/// Abstraction for PLC clients (Siemens, Modbus, MC, FINS, EtherNet/IP, Simulated).
/// </summary>
public interface IPlcClient : IDevice
{
    Task<bool> ReadBoolAsync(string address, CancellationToken ct = default);
    Task WriteBoolAsync(string address, bool value, CancellationToken ct = default);
}

/// <summary>
/// Abstraction for industrial robot clients (Fanuc, ABB, Yaskawa, UR, KUKA, Simulated).
/// </summary>
public interface IRobotClient : IDevice
{
    Task SendPoseAsync(double x, double y, double z, double rx, double ry, double rz, CancellationToken ct = default);
}
