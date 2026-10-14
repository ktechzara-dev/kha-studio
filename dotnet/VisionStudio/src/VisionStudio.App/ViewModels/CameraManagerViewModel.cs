using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisionStudio.App.Converters;
using VisionStudio.Core.Models;
using VisionStudio.Devices.Abstractions;
using VisionStudio.Devices.Cameras;

namespace VisionStudio.App.ViewModels;

public partial class CameraManagerViewModel : ObservableObject, IAsyncDisposable
{
    private readonly ICameraManager _cameraManager;
    private readonly Action<ImageFrame>? _onSnapshotToPipeline;

    [ObservableProperty]
    private ICamera? _selectedCamera;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private bool _isAcquiring;

    [ObservableProperty]
    private BitmapSource? _previewImage;

    [ObservableProperty]
    private double _exposureTime = 15000;

    [ObservableProperty]
    private double _gain = 0;

    [ObservableProperty]
    private double _frameRate = 30;

    [ObservableProperty]
    private CameraTriggerMode _triggerMode = CameraTriggerMode.FreeRun;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private double _currentFps;

    [ObservableProperty]
    private long _frameCount;

    [ObservableProperty]
    private long _droppedFrames;

    [ObservableProperty]
    private double _lastLatencyMs;

    [ObservableProperty]
    private string _selectedPattern = "TargetPart";

    [ObservableProperty]
    private bool _defectInjection;

    [ObservableProperty]
    private string _defectType = "None";

    [ObservableProperty]
    private string _folderPath = string.Empty;

    public ObservableCollection<ICamera> Cameras { get; } = new();

    public string[] AvailablePatterns { get; } = new[]
    {
        "TargetPart", "Checkerboard", "MovingParts", "ConcentricCircles", "ColorBars"
    };

    public string[] AvailableDefectTypes { get; } = new[]
    {
        "None", "MissingHole", "Scratch", "Burr", "Random"
    };

    public CameraManagerViewModel(ICameraManager? cameraManager = null, Action<ImageFrame>? onSnapshotToPipeline = null)
    {
        _cameraManager = cameraManager ?? CameraManager.Instance;
        _onSnapshotToPipeline = onSnapshotToPipeline;

        RefreshCamerasList();
        SelectedCamera = _cameraManager.ActiveCamera ?? Cameras.FirstOrDefault();
    }

    public void RefreshCamerasList()
    {
        Cameras.Clear();
        foreach (var cam in _cameraManager.GetAllCameras())
        {
            Cameras.Add(cam);
        }
    }

    partial void OnSelectedCameraChanged(ICamera? oldValue, ICamera? newValue)
    {
        if (oldValue != null)
        {
            oldValue.FrameGrabbed -= OnCameraFrameGrabbed;
        }

        if (newValue != null)
        {
            _cameraManager.ActiveCamera = newValue;
            IsConnected = newValue.State == DeviceConnectionState.Connected;
            IsAcquiring = newValue.IsAcquiring;
            ExposureTime = newValue.Parameters.ExposureTime;
            Gain = newValue.Parameters.Gain;
            FrameRate = newValue.Parameters.FrameRate;
            TriggerMode = newValue.Parameters.TriggerMode;

            if (newValue.Parameters.CustomFeatures.TryGetValue(SyntheticPatternCamera.FeaturePattern, out var pObj))
                SelectedPattern = pObj?.ToString() ?? "TargetPart";

            if (newValue.Parameters.CustomFeatures.TryGetValue(SyntheticPatternCamera.FeatureDefectEnabled, out var dObj))
                DefectInjection = Convert.ToBoolean(dObj);

            if (newValue.Parameters.CustomFeatures.TryGetValue(FileFolderCameraSimulator.FeatureFolderPath, out var fObj))
                FolderPath = fObj?.ToString() ?? string.Empty;

            newValue.FrameGrabbed += OnCameraFrameGrabbed;
        }
    }

    [RelayCommand]
    public async Task ConnectAsync()
    {
        if (SelectedCamera == null) return;
        try
        {
            StatusMessage = "Connecting...";
            await SelectedCamera.ConnectAsync();
            IsConnected = SelectedCamera.State == DeviceConnectionState.Connected;
            StatusMessage = $"Connected to {SelectedCamera.Name}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Connection failed: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task DisconnectAsync()
    {
        if (SelectedCamera == null) return;
        try
        {
            StatusMessage = "Disconnecting...";
            await SelectedCamera.DisconnectAsync();
            IsConnected = false;
            IsAcquiring = false;
            StatusMessage = "Disconnected";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Disconnection error: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task StartGrabbingAsync()
    {
        if (SelectedCamera == null) return;
        if (!IsConnected)
        {
            await ConnectAsync();
        }

        try
        {
            await SelectedCamera.StartContinuousGrabbingAsync();
            IsAcquiring = SelectedCamera.IsAcquiring;
            StatusMessage = "Streaming live...";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Acquisition error: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task StopGrabbingAsync()
    {
        if (SelectedCamera == null) return;
        try
        {
            await SelectedCamera.StopContinuousGrabbingAsync();
            IsAcquiring = false;
            StatusMessage = "Acquisition paused";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Stop error: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task SnapFrameAsync()
    {
        if (SelectedCamera == null) return;
        if (!IsConnected)
        {
            await ConnectAsync();
        }

        try
        {
            if (SelectedCamera.Parameters.TriggerMode == CameraTriggerMode.Software && IsAcquiring)
            {
                await SelectedCamera.TriggerAsync();
            }
            else
            {
                using var frame = await SelectedCamera.GrabFrameAsync();
                UpdatePreview(frame);
            }
            StatusMessage = "Captured single frame";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Capture failed: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task SendSnapshotToPipelineAsync()
    {
        if (SelectedCamera == null) return;
        if (!IsConnected)
        {
            await ConnectAsync();
        }

        try
        {
            var frame = await SelectedCamera.GrabFrameAsync();
            _onSnapshotToPipeline?.Invoke(frame);
            StatusMessage = $"Transferred Frame #{frame.FrameId} to Pipeline Canvas!";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Snapshot transfer failed: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task DiscoverCamerasAsync()
    {
        StatusMessage = "Discovering devices...";
        await _cameraManager.DiscoverCamerasAsync();
        RefreshCamerasList();
        StatusMessage = $"Discovery completed. {Cameras.Count} camera(s) available.";
    }

    partial void OnExposureTimeChanged(double value)
    {
        if (SelectedCamera != null)
        {
            _ = SelectedCamera.SetParameterAsync("ExposureTime", value);
        }
    }

    partial void OnGainChanged(double value)
    {
        if (SelectedCamera != null)
        {
            _ = SelectedCamera.SetParameterAsync("Gain", value);
        }
    }

    partial void OnFrameRateChanged(double value)
    {
        if (SelectedCamera != null)
        {
            _ = SelectedCamera.SetParameterAsync("FrameRate", value);
        }
    }

    partial void OnTriggerModeChanged(CameraTriggerMode value)
    {
        if (SelectedCamera != null)
        {
            _ = SelectedCamera.SetParameterAsync("TriggerMode", value);
        }
    }

    partial void OnSelectedPatternChanged(string value)
    {
        if (SelectedCamera is SyntheticPatternCamera syn)
        {
            _ = syn.SetParameterAsync(SyntheticPatternCamera.FeaturePattern, value);
        }
    }

    partial void OnDefectInjectionChanged(bool value)
    {
        if (SelectedCamera is SyntheticPatternCamera syn)
        {
            _ = syn.SetParameterAsync(SyntheticPatternCamera.FeatureDefectEnabled, value);
        }
    }

    partial void OnDefectTypeChanged(string value)
    {
        if (SelectedCamera is SyntheticPatternCamera syn)
        {
            _ = syn.SetParameterAsync(SyntheticPatternCamera.FeatureDefectType, value);
        }
    }

    partial void OnFolderPathChanged(string value)
    {
        if (SelectedCamera is FileFolderCameraSimulator fcs)
        {
            fcs.FolderPath = value;
        }
    }

    private void OnCameraFrameGrabbed(object? sender, CameraFrameEventArgs e)
    {
        UpdatePreview(e.Frame);

        CurrentFps = e.Statistics.CurrentFps;
        FrameCount = e.Statistics.FrameCount;
        DroppedFrames = e.Statistics.DroppedFrames;
        LastLatencyMs = Math.Round(e.Statistics.LastGrabDurationMs, 1);
    }

    private void UpdatePreview(ImageFrame frame)
    {
        if (Application.Current?.Dispatcher == null) return;

        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            try
            {
                PreviewImage = ImageConversionHelper.MatToBitmapSource(frame.Mat);
            }
            catch
            {
                // Ignore conversion error during fast teardown
            }
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (SelectedCamera != null)
        {
            SelectedCamera.FrameGrabbed -= OnCameraFrameGrabbed;
            await SelectedCamera.StopContinuousGrabbingAsync();
        }
    }
}
