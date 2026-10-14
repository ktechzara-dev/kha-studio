using VisionStudio.Devices.Abstractions;

namespace VisionStudio.Devices.Tests;

public class DeviceContractsTests
{
    [Fact]
    public void DeviceConnectionState_ContainsExpectedValues()
    {
        Enum.GetValues<DeviceConnectionState>().Should().Contain(new[]
        {
            DeviceConnectionState.Disconnected,
            DeviceConnectionState.Connecting,
            DeviceConnectionState.Connected,
            DeviceConnectionState.Reconnecting,
            DeviceConnectionState.Faulted
        });
    }

    [Fact]
    public void CameraTriggerMode_ContainsExpectedModes()
    {
        Enum.GetValues<CameraTriggerMode>().Should().Contain(new[]
        {
            CameraTriggerMode.FreeRun,
            CameraTriggerMode.Software,
            CameraTriggerMode.HardwareLine
        });
    }
}
