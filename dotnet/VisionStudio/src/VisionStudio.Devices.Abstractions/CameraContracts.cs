using System.Collections.Concurrent;
using VisionStudio.Core.Models;

namespace VisionStudio.Devices.Abstractions;

/// <summary>
/// Configurable hardware and acquisition parameters for industrial & simulated cameras.
/// </summary>
public sealed class CameraParameters
{
    public int Width { get; set; } = 640;
    public int Height { get; set; } = 480;
    public double ExposureTime { get; set; } = 15000.0; // microseconds
    public double Gain { get; set; } = 0.0; // dB
    public double FrameRate { get; set; } = 30.0; // FPS
    public CameraTriggerMode TriggerMode { get; set; } = CameraTriggerMode.FreeRun;
    public PixelFormat PixelFormat { get; set; } = PixelFormat.Bgr24;

    /// <summary>
    /// Custom vendor-specific or GenICam SFNC features.
    /// </summary>
    public ConcurrentDictionary<string, object> CustomFeatures { get; } = new(StringComparer.OrdinalIgnoreCase);

    public CameraParameters Clone()
    {
        var copy = new CameraParameters
        {
            Width = Width,
            Height = Height,
            ExposureTime = ExposureTime,
            Gain = Gain,
            FrameRate = FrameRate,
            TriggerMode = TriggerMode,
            PixelFormat = PixelFormat
        };

        foreach (var (k, v) in CustomFeatures)
        {
            copy.CustomFeatures[k] = v;
        }

        return copy;
    }
}

/// <summary>
/// Real-time operational statistics for an active camera stream.
/// </summary>
public sealed class CameraStatistics
{
    public long FrameCount { get; set; }
    public double CurrentFps { get; set; }
    public long DroppedFrames { get; set; }
    public double LastGrabDurationMs { get; set; }

    public CameraStatistics Clone() => new()
    {
        FrameCount = FrameCount,
        CurrentFps = CurrentFps,
        DroppedFrames = DroppedFrames,
        LastGrabDurationMs = LastGrabDurationMs
    };
}

/// <summary>
/// Event arguments delivered when a new image frame is acquired by the camera driver.
/// </summary>
public sealed class CameraFrameEventArgs : EventArgs
{
    public ImageFrame Frame { get; }
    public CameraStatistics Statistics { get; }

    public CameraFrameEventArgs(ImageFrame frame, CameraStatistics statistics)
    {
        Frame = frame ?? throw new ArgumentNullException(nameof(frame));
        Statistics = statistics ?? throw new ArgumentNullException(nameof(statistics));
    }
}

/// <summary>
/// High-level abstraction for 2D industrial cameras (GigE, USB3, DirectShow, File, Synthetic).
/// </summary>
public interface ICamera : IDevice
{
    CameraParameters Parameters { get; }
    CameraStatistics Statistics { get; }
    bool IsAcquiring { get; }

    /// <summary>
    /// Acquires a single image frame synchronously or asynchronously.
    /// </summary>
    Task<ImageFrame> GrabFrameAsync(CancellationToken ct = default);

    /// <summary>
    /// Starts high-speed continuous acquisition stream at the specified frame rate.
    /// </summary>
    Task StartContinuousGrabbingAsync(Action<ImageFrame>? onFrame = null, CancellationToken ct = default);

    /// <summary>
    /// Stops continuous image acquisition.
    /// </summary>
    Task StopContinuousGrabbingAsync(CancellationToken ct = default);

    /// <summary>
    /// Emits a software trigger pulse to capture a single frame in Software Trigger mode.
    /// </summary>
    Task TriggerAsync(CancellationToken ct = default);

    /// <summary>
    /// Sets a generic camera or GenICam SFNC feature node.
    /// </summary>
    Task SetParameterAsync(string name, object value, CancellationToken ct = default);

    /// <summary>
    /// Reads a generic camera or GenICam SFNC feature node.
    /// </summary>
    Task<T> GetParameterAsync<T>(string name, CancellationToken ct = default);

    /// <summary>
    /// Event fired whenever a new frame has been acquired and decoded.
    /// </summary>
    event EventHandler<CameraFrameEventArgs>? FrameGrabbed;
}

/// <summary>
/// Central registry, discovery, and lifecycle manager for all camera devices.
/// </summary>
public interface ICameraManager
{
    IReadOnlyList<ICamera> GetAllCameras();
    ICamera? GetCamera(string id);
    ICamera? ActiveCamera { get; set; }

    void RegisterCamera(ICamera camera);
    bool UnregisterCamera(string id);
    Task<IReadOnlyList<ICamera>> DiscoverCamerasAsync(CancellationToken ct = default);

    event EventHandler<ICamera>? CameraRegistered;
    event EventHandler<string>? CameraUnregistered;
    event EventHandler<ICamera?>? ActiveCameraChanged;
}
