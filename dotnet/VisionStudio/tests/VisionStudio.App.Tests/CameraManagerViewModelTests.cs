using FluentAssertions;
using VisionStudio.App.ViewModels;
using VisionStudio.Core.Models;
using VisionStudio.Devices.Abstractions;
using VisionStudio.Devices.Cameras;
using Xunit;

namespace VisionStudio.App.Tests;

public class CameraManagerViewModelTests
{
    [Fact]
    public void CameraManagerViewModel_InitializesWithRegisteredCameras()
    {
        var manager = new CameraManager();
        var vm = new CameraManagerViewModel(manager);

        vm.Cameras.Should().NotBeEmpty();
        vm.Cameras.Count.Should().BeGreaterThanOrEqualTo(4);
        vm.SelectedCamera.Should().NotBeNull();
        vm.SelectedCamera!.Id.Should().Be("SIM_SYNTHETIC");
    }

    [Fact]
    public async Task CameraManagerViewModel_ConnectAndDisconnect_UpdatesStates()
    {
        var manager = new CameraManager();
        var vm = new CameraManagerViewModel(manager);

        vm.IsConnected.Should().BeFalse();

        await vm.ConnectCommand.ExecuteAsync(null);
        vm.IsConnected.Should().BeTrue();
        vm.StatusMessage.Should().Contain("Connected");

        await vm.DisconnectCommand.ExecuteAsync(null);
        vm.IsConnected.Should().BeFalse();
        vm.StatusMessage.Should().Be("Disconnected");
    }

    [Fact]
    public async Task CameraManagerViewModel_SnapFrame_AcquiresSingleFrame()
    {
        var manager = new CameraManager();
        var vm = new CameraManagerViewModel(manager);

        await vm.ConnectCommand.ExecuteAsync(null);
        await vm.SnapFrameCommand.ExecuteAsync(null);

        vm.StatusMessage.Should().Contain("Captured");
        await vm.DisconnectCommand.ExecuteAsync(null);
    }

    [Fact]
    public async Task CameraManagerViewModel_SendSnapshotToPipeline_InvokesCallback()
    {
        var manager = new CameraManager();
        ImageFrame? receivedFrame = null;
        var vm = new CameraManagerViewModel(manager, onSnapshotToPipeline: f => receivedFrame = f);

        await vm.ConnectCommand.ExecuteAsync(null);
        await vm.SendSnapshotToPipelineCommand.ExecuteAsync(null);

        receivedFrame.Should().NotBeNull();
        receivedFrame!.Mat.Empty().Should().BeFalse();
        vm.StatusMessage.Should().Contain("Transferred Frame");

        await vm.DisconnectCommand.ExecuteAsync(null);
    }

    [Fact]
    public void CameraManagerViewModel_ParameterChange_PropagatesToCamera()
    {
        var manager = new CameraManager();
        var vm = new CameraManagerViewModel(manager);

        vm.ExposureTime = 22000;
        vm.SelectedCamera!.Parameters.ExposureTime.Should().Be(22000);

        vm.Gain = 6.5;
        vm.SelectedCamera.Parameters.Gain.Should().Be(6.5);

        vm.FrameRate = 45;
        vm.SelectedCamera.Parameters.FrameRate.Should().Be(45);
    }
}
