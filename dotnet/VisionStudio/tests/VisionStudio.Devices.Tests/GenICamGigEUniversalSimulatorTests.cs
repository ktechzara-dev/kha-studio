using FluentAssertions;
using VisionStudio.Core.Models;
using VisionStudio.Devices.Abstractions;
using VisionStudio.Devices.Cameras;
using Xunit;

namespace VisionStudio.Devices.Tests;

public class GenICamGigEUniversalSimulatorTests
{
    [Fact]
    public async Task GenICamGigE_InitializesSFNCFeaturesCorrectly()
    {
        var camera = new GenICamGigEUniversalSimulator();
        await camera.ConnectAsync();

        camera.DeviceVendorName.Should().Be("VisionStudio Industrial");
        camera.DeviceModelName.Should().Be("VS-GigE-5000M");
        camera.DeviceSerialNumber.Should().StartWith("VS2026-GIGE");
        camera.CurrentIpAddress.Should().Be("192.168.1.100");
        camera.PacketSize.Should().Be(1500);

        var ip = await camera.GetParameterAsync<string>(GenICamGigEUniversalSimulator.SFNC_GevCurrentIPAddress);
        ip.Should().Be("192.168.1.100");

        var heartbeatOk = camera.PingHeartbeat();
        heartbeatOk.Should().BeTrue();

        await camera.DisconnectAsync();
    }

    [Fact]
    public async Task GenICamGigE_AcquiresHorizontalRampTestPattern()
    {
        var camera = new GenICamGigEUniversalSimulator();
        camera.Parameters.PixelFormat = PixelFormat.Mono8;
        await camera.ConnectAsync();

        await camera.SetParameterAsync(GenICamGigEUniversalSimulator.SFNC_TestPattern, "GreyHorizontalRamp");

        using var frame = await camera.GrabFrameAsync();
        frame.Should().NotBeNull();
        frame.Mat.Channels().Should().Be(1);
        frame.Width.Should().Be(640);
        frame.Height.Should().Be(480);

        await camera.DisconnectAsync();
    }

    [Fact]
    public async Task GenICamGigE_SupportsSoftwareTriggerViaSFNC()
    {
        var camera = new GenICamGigEUniversalSimulator();
        await camera.ConnectAsync();

        await camera.SetParameterAsync(GenICamGigEUniversalSimulator.SFNC_TriggerMode, "On");
        camera.Parameters.TriggerMode.Should().Be(CameraTriggerMode.Software);

        var frameReceived = false;
        var tcs = new TaskCompletionSource<bool>();

        await camera.StartContinuousGrabbingAsync(onFrame: f =>
        {
            frameReceived = true;
            tcs.TrySetResult(true);
        });

        await Task.Delay(100);
        frameReceived.Should().BeFalse();

        // Fire TriggerSoftware SFNC feature
        await camera.SetParameterAsync(GenICamGigEUniversalSimulator.SFNC_TriggerSoftware, true);

        var res = await Task.WhenAny(tcs.Task, Task.Delay(1000));
        res.Should().Be(tcs.Task);
        frameReceived.Should().BeTrue();

        await camera.StopContinuousGrabbingAsync();
        await camera.DisconnectAsync();
    }
}
