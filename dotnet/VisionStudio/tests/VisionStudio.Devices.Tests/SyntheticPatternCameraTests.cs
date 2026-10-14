using FluentAssertions;
using VisionStudio.Core.Models;
using VisionStudio.Devices.Abstractions;
using VisionStudio.Devices.Cameras;
using Xunit;

namespace VisionStudio.Devices.Tests;

public class SyntheticPatternCameraTests
{
    [Fact]
    public async Task SyntheticPatternCamera_ConnectAndGrabSingleFrame_Succeeds()
    {
        var camera = new SyntheticPatternCamera("TEST_SYNTHETIC", "Test Synthetic Camera");
        camera.State.Should().Be(DeviceConnectionState.Disconnected);

        await camera.ConnectAsync();
        camera.State.Should().Be(DeviceConnectionState.Connected);

        using var frame = await camera.GrabFrameAsync();
        frame.Should().NotBeNull();
        frame.Width.Should().Be(640);
        frame.Height.Should().Be(480);
        frame.CameraId.Should().Be("TEST_SYNTHETIC");
        frame.FrameId.Should().BeGreaterThan(0);
        frame.Mat.Empty().Should().BeFalse();

        camera.Statistics.FrameCount.Should().Be(1);
        camera.Statistics.LastGrabDurationMs.Should().BeGreaterThan(0);

        await camera.DisconnectAsync();
        camera.State.Should().Be(DeviceConnectionState.Disconnected);
    }

    [Theory]
    [InlineData("TargetPart")]
    [InlineData("Checkerboard")]
    [InlineData("MovingParts")]
    [InlineData("ConcentricCircles")]
    [InlineData("ColorBars")]
    public async Task SyntheticPatternCamera_CanGenerateAllFivePatterns(string pattern)
    {
        var camera = new SyntheticPatternCamera();
        await camera.ConnectAsync();

        await camera.SetParameterAsync(SyntheticPatternCamera.FeaturePattern, pattern);

        using var frame = await camera.GrabFrameAsync();
        frame.Should().NotBeNull();
        frame.Mat.Rows.Should().Be(480);
        frame.Mat.Cols.Should().Be(640);

        await camera.DisconnectAsync();
    }

    [Fact]
    public async Task SyntheticPatternCamera_SupportsMono8PixelFormat()
    {
        var camera = new SyntheticPatternCamera();
        camera.Parameters.PixelFormat = PixelFormat.Mono8;
        await camera.ConnectAsync();

        using var frame = await camera.GrabFrameAsync();
        frame.Mat.Channels().Should().Be(1);

        await camera.DisconnectAsync();
    }

    [Theory]
    [InlineData("MissingHole")]
    [InlineData("Scratch")]
    [InlineData("Burr")]
    [InlineData("Random")]
    public async Task SyntheticPatternCamera_InjectsDefectsSuccessfully(string defectType)
    {
        var camera = new SyntheticPatternCamera();
        await camera.ConnectAsync();

        await camera.SetParameterAsync(SyntheticPatternCamera.FeatureDefectEnabled, true);
        await camera.SetParameterAsync(SyntheticPatternCamera.FeatureDefectType, defectType);

        using var frame = await camera.GrabFrameAsync();
        frame.Should().NotBeNull();
        frame.Mat.Empty().Should().BeFalse();

        await camera.DisconnectAsync();
    }

    [Fact]
    public async Task SyntheticPatternCamera_ContinuousGrabbing_StreamsFramesAndCalculatesStats()
    {
        var camera = new SyntheticPatternCamera();
        camera.Parameters.FrameRate = 60.0;
        await camera.ConnectAsync();

        var framesReceived = 0;
        var tcs = new TaskCompletionSource<bool>();

        await camera.StartContinuousGrabbingAsync(onFrame: f =>
        {
            var count = Interlocked.Increment(ref framesReceived);
            if (count >= 5)
            {
                tcs.TrySetResult(true);
            }
        });

        camera.IsAcquiring.Should().BeTrue();

        // Wait up to 2 seconds for at least 5 frames
        var completed = await Task.WhenAny(tcs.Task, Task.Delay(2000));
        completed.Should().Be(tcs.Task, "Continuous grabbing should acquire at least 5 frames within 2 seconds");

        await camera.StopContinuousGrabbingAsync();
        camera.IsAcquiring.Should().BeFalse();

        camera.Statistics.FrameCount.Should().BeGreaterThanOrEqualTo(5);

        await camera.DisconnectAsync();
    }

    [Fact]
    public async Task SyntheticPatternCamera_SoftwareTriggerMode_OnlyEmitsOnTrigger()
    {
        var camera = new SyntheticPatternCamera();
        camera.Parameters.TriggerMode = CameraTriggerMode.Software;
        await camera.ConnectAsync();

        var triggered = false;
        var tcs = new TaskCompletionSource<bool>();

        await camera.StartContinuousGrabbingAsync(onFrame: f =>
        {
            triggered = true;
            tcs.TrySetResult(true);
        });

        // Verify no immediate trigger
        await Task.Delay(100);
        triggered.Should().BeFalse();

        // Send software trigger pulse
        await camera.TriggerAsync();

        var result = await Task.WhenAny(tcs.Task, Task.Delay(1000));
        result.Should().Be(tcs.Task);
        triggered.Should().BeTrue();

        await camera.StopContinuousGrabbingAsync();
        await camera.DisconnectAsync();
    }
}
