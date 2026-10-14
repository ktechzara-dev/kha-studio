using FluentAssertions;
using VisionStudio.Devices.Abstractions;
using VisionStudio.Devices.Cameras;
using Xunit;

namespace VisionStudio.Devices.Tests;

public class CameraManagerTests : IDisposable
{
    private readonly string _tempPresetFile;

    public CameraManagerTests()
    {
        _tempPresetFile = Path.Combine(Path.GetTempPath(), $"cam_presets_{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_tempPresetFile))
                File.Delete(_tempPresetFile);
        }
        catch
        {
            // Ignore cleanup
        }
    }

    [Fact]
    public void CameraManager_RegistersDefaultSimulatorsOnInit()
    {
        var manager = new CameraManager();
        var cameras = manager.GetAllCameras();

        cameras.Should().Contain(c => c.Id == "SIM_SYNTHETIC");
        cameras.Should().Contain(c => c.Id == "SIM_FOLDER");
        cameras.Should().Contain(c => c.Id == "SIM_GIGE_01");
        cameras.Should().Contain(c => c.Id == "PHYS_WEBCAM_0");

        manager.ActiveCamera.Should().NotBeNull();
        manager.ActiveCamera!.Id.Should().Be("SIM_SYNTHETIC");
    }

    [Fact]
    public void CameraManager_CanRegisterAndUnregisterCameras()
    {
        var manager = new CameraManager();
        var customCam = new SyntheticPatternCamera("CUSTOM_CAM_99", "Custom Camera");

        bool registeredFired = false;
        bool unregisteredFired = false;

        manager.CameraRegistered += (s, c) => { if (c.Id == "CUSTOM_CAM_99") registeredFired = true; };
        manager.CameraUnregistered += (s, id) => { if (id == "CUSTOM_CAM_99") unregisteredFired = true; };

        manager.RegisterCamera(customCam);
        registeredFired.Should().BeTrue();
        manager.GetCamera("CUSTOM_CAM_99").Should().BeSameAs(customCam);

        var removed = manager.UnregisterCamera("CUSTOM_CAM_99");
        removed.Should().BeTrue();
        unregisteredFired.Should().BeTrue();
        manager.GetCamera("CUSTOM_CAM_99").Should().BeNull();
    }

    [Fact]
    public async Task CameraManager_SavesAndLoadsPresetsJsonRoundtrip()
    {
        var manager = new CameraManager();
        var cam = manager.GetCamera("SIM_SYNTHETIC");
        cam.Should().NotBeNull();

        cam!.Parameters.Width = 1280;
        cam.Parameters.Height = 720;
        cam.Parameters.ExposureTime = 45000;
        cam.Parameters.Gain = 12.5;

        await manager.SavePresetsAsync(_tempPresetFile);
        File.Exists(_tempPresetFile).Should().BeTrue();

        // Reset parameters
        cam.Parameters.Width = 640;
        cam.Parameters.Height = 480;

        // Restore from JSON preset
        await manager.LoadPresetsAsync(_tempPresetFile);

        cam.Parameters.Width.Should().Be(1280);
        cam.Parameters.Height.Should().Be(720);
        cam.Parameters.ExposureTime.Should().Be(45000);
        cam.Parameters.Gain.Should().Be(12.5);
    }

    [Fact]
    public async Task DirectShowWebcamCamera_SafelyConnectsAndGrabsWithFallback()
    {
        var cam = new DirectShowWebcamCamera("TEST_WEBCAM", "DirectShow Webcam Test", deviceIndex: 99);
        await cam.ConnectAsync();
        cam.State.Should().Be(DeviceConnectionState.Connected);

        using var frame = await cam.GrabFrameAsync();
        frame.Should().NotBeNull();
        frame.Width.Should().Be(640);
        frame.Height.Should().Be(480);
        frame.Mat.Empty().Should().BeFalse();

        await cam.DisconnectAsync();
        cam.State.Should().Be(DeviceConnectionState.Disconnected);
    }
}
