using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenCvSharp;
using VisionStudio.Devices.Abstractions;

namespace VisionStudio.Devices.Cameras;

/// <summary>
/// Camera configuration preset for persistence.
/// </summary>
public sealed class CameraPresetDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public double ExposureTime { get; set; }
    public double Gain { get; set; }
    public double FrameRate { get; set; }
    public string TriggerMode { get; set; } = "FreeRun";
    public string PixelFormat { get; set; } = "Bgr24";
    public Dictionary<string, string> CustomFeatures { get; set; } = new();
}

/// <summary>
/// Central camera device manager, discovery engine, and preset persistence service.
/// </summary>
public sealed class CameraManager : ICameraManager, IAsyncDisposable
{
    private static readonly Lazy<CameraManager> _lazyInstance = new(() => new CameraManager());
    public static CameraManager Instance => _lazyInstance.Value;

    private readonly ConcurrentDictionary<string, ICamera> _cameras = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger _logger;
    private ICamera? _activeCamera;

    public event EventHandler<ICamera>? CameraRegistered;
    public event EventHandler<string>? CameraUnregistered;
    public event EventHandler<ICamera?>? ActiveCameraChanged;

    public ICamera? ActiveCamera
    {
        get => _activeCamera;
        set
        {
            if (_activeCamera != value)
            {
                _activeCamera = value;
                ActiveCameraChanged?.Invoke(this, _activeCamera);
            }
        }
    }

    public CameraManager(ILogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
        RegisterDefaultSimulators();
    }

    public void RegisterDefaultSimulators()
    {
        var synthetic = new SyntheticPatternCamera("SIM_SYNTHETIC", "Synthetic Pattern Generator (5 Modes)");
        var folder = new FileFolderCameraSimulator("SIM_FOLDER", "File & Folder Sequence Camera");
        var gige = new GenICamGigEUniversalSimulator("SIM_GIGE_01", "GigE Vision GenICam Simulator (VS-GigE-5000M)");
        var webcam = new DirectShowWebcamCamera("PHYS_WEBCAM_0", "Webcam Laptop / USB Camera (DirectShow/MSMF)", deviceIndex: 0);

        RegisterCamera(synthetic);
        RegisterCamera(folder);
        RegisterCamera(gige);
        RegisterCamera(webcam);

        ActiveCamera ??= synthetic;
    }

    public IReadOnlyList<ICamera> GetAllCameras()
    {
        return _cameras.Values.ToList();
    }

    public ICamera? GetCamera(string id)
    {
        return _cameras.TryGetValue(id, out var cam) ? cam : null;
    }

    public void RegisterCamera(ICamera camera)
    {
        ArgumentNullException.ThrowIfNull(camera);
        _cameras[camera.Id] = camera;
        _logger.LogInformation("Camera registered: {CameraId} ({CameraName})", camera.Id, camera.Name);
        CameraRegistered?.Invoke(this, camera);
    }

    public bool UnregisterCamera(string id)
    {
        if (_cameras.TryRemove(id, out var cam))
        {
            if (ActiveCamera?.Id == id)
            {
                ActiveCamera = _cameras.Values.FirstOrDefault();
            }

            CameraUnregistered?.Invoke(this, id);
            return true;
        }
        return false;
    }

    public Task<IReadOnlyList<ICamera>> DiscoverCamerasAsync(CancellationToken ct = default)
    {
        // Check for USB DirectShow and laptop webcams
        for (int i = 0; i < 4; i++)
        {
            var devId = $"PHYS_WEBCAM_{i}";
            if (!_cameras.ContainsKey(devId))
            {
                try
                {
                    using var cap = new VideoCapture(i, VideoCaptureAPIs.DSHOW);
                    bool opened = cap.IsOpened();
                    if (!opened)
                    {
                        using var capAny = new VideoCapture(i, VideoCaptureAPIs.ANY);
                        opened = capAny.IsOpened();
                    }

                    if (opened)
                    {
                        var devName = i == 0 ? "Webcam Laptop / USB Camera (#0)" : $"Camera USB / Thiết bị phụ (#{i})";
                        var dev = new DirectShowWebcamCamera(devId, devName, i);
                        RegisterCamera(dev);
                    }
                }
                catch
                {
                    // Ignore probing exceptions
                }
            }
        }

        return Task.FromResult<IReadOnlyList<ICamera>>(GetAllCameras());
    }

    public async Task SavePresetsAsync(string filePath)
    {
        var dtos = _cameras.Values.Select(c => new CameraPresetDto
        {
            Id = c.Id,
            Name = c.Name,
            TypeName = c.GetType().Name,
            Width = c.Parameters.Width,
            Height = c.Parameters.Height,
            ExposureTime = c.Parameters.ExposureTime,
            Gain = c.Parameters.Gain,
            FrameRate = c.Parameters.FrameRate,
            TriggerMode = c.Parameters.TriggerMode.ToString(),
            PixelFormat = c.Parameters.PixelFormat.ToString(),
            CustomFeatures = c.Parameters.CustomFeatures.ToDictionary(k => k.Key, v => v.Value?.ToString() ?? string.Empty)
        }).ToList();

        var json = JsonSerializer.Serialize(dtos, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath, json);
    }

    public async Task LoadPresetsAsync(string filePath)
    {
        if (!File.Exists(filePath)) return;

        var json = await File.ReadAllTextAsync(filePath);
        var dtos = JsonSerializer.Deserialize<List<CameraPresetDto>>(json);
        if (dtos == null) return;

        foreach (var dto in dtos)
        {
            if (_cameras.TryGetValue(dto.Id, out var cam))
            {
                cam.Parameters.Width = dto.Width;
                cam.Parameters.Height = dto.Height;
                cam.Parameters.ExposureTime = dto.ExposureTime;
                cam.Parameters.Gain = dto.Gain;
                cam.Parameters.FrameRate = dto.FrameRate;
                if (Enum.TryParse<CameraTriggerMode>(dto.TriggerMode, out var trig))
                    cam.Parameters.TriggerMode = trig;
                if (Enum.TryParse<Core.Models.PixelFormat>(dto.PixelFormat, out var pix))
                    cam.Parameters.PixelFormat = pix;

                foreach (var (k, v) in dto.CustomFeatures)
                {
                    cam.Parameters.CustomFeatures[k] = v;
                }
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var cam in _cameras.Values)
        {
            await cam.DisposeAsync();
        }
        _cameras.Clear();
    }
}
