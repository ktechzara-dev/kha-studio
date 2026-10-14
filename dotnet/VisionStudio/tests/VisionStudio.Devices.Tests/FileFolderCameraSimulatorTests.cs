using FluentAssertions;
using OpenCvSharp;
using VisionStudio.Devices.Abstractions;
using VisionStudio.Devices.Cameras;
using Xunit;

namespace VisionStudio.Devices.Tests;

public class FileFolderCameraSimulatorTests : IDisposable
{
    private readonly string _tempDir;

    public FileFolderCameraSimulatorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "VisionStudio_Test_Images_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch
        {
            // Ignore cleanup failure
        }
    }

    [Fact]
    public async Task FileFolderCameraSimulator_EmptyFolder_FallsBackToSyntheticFrame()
    {
        var camera = new FileFolderCameraSimulator("SIM_EMPTY", "Empty Folder Camera", _tempDir);
        await camera.ConnectAsync();

        using var frame = await camera.GrabFrameAsync();
        frame.Should().NotBeNull();
        frame.Width.Should().Be(640);
        frame.Height.Should().Be(480);
        frame.Mat.Empty().Should().BeFalse();

        await camera.DisconnectAsync();
    }

    [Fact]
    public async Task FileFolderCameraSimulator_LoadsAndSequencesThroughImages()
    {
        // Create 3 test image files
        for (int i = 0; i < 3; i++)
        {
            using var mat = new Mat(100, 100, MatType.CV_8UC3, new Scalar(i * 60, i * 60, 200));
            Cv2.ImWrite(Path.Combine(_tempDir, $"img_{i:D2}.png"), mat);
        }

        var camera = new FileFolderCameraSimulator("SIM_TEST_SEQ", "Sequence Test", _tempDir);
        await camera.ConnectAsync();

        camera.TotalImages.Should().Be(3);
        camera.CurrentIndex.Should().Be(0);

        // Frame 1
        using (var f1 = await camera.GrabFrameAsync())
        {
            f1.Should().NotBeNull();
            camera.CurrentIndex.Should().Be(1);
        }

        // Frame 2
        using (var f2 = await camera.GrabFrameAsync())
        {
            f2.Should().NotBeNull();
            camera.CurrentIndex.Should().Be(2);
        }

        // Frame 3 (in loop mode, next index loops to 0)
        using (var f3 = await camera.GrabFrameAsync())
        {
            f3.Should().NotBeNull();
            camera.CurrentIndex.Should().Be(0);
        }

        // Test manual stepping
        camera.NextFrame();
        camera.CurrentIndex.Should().Be(1);

        camera.PreviousFrame();
        camera.CurrentIndex.Should().Be(0);

        camera.JumpTo(2);
        camera.CurrentIndex.Should().Be(2);

        await camera.DisconnectAsync();
    }
}
