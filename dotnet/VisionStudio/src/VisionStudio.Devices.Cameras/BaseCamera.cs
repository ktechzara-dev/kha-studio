using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VisionStudio.Core.Models;
using VisionStudio.Devices.Abstractions;

namespace VisionStudio.Devices.Cameras;

/// <summary>
/// Abstract base class for industrial and simulated camera drivers.
/// Handles connection lifecycle, continuous acquisition loops, performance statistics, and software triggers.
/// </summary>
public abstract class BaseCamera : ICamera
{
    private readonly ILogger _logger;
    private readonly object _stateLock = new();
    private CancellationTokenSource? _continuousGrabCts;
    private Task? _continuousGrabTask;
    private Action<ImageFrame>? _continuousCallback;

    private readonly SemaphoreSlim _softwareTriggerSignal = new(0, int.MaxValue);

    public string Id { get; }
    public string Name { get; }
    public DeviceConnectionState State { get; protected set; } = DeviceConnectionState.Disconnected;
    public CameraParameters Parameters { get; } = new();
    public CameraStatistics Statistics { get; } = new();
    public bool IsAcquiring => _continuousGrabTask != null && !_continuousGrabTask.IsCompleted;

    public event EventHandler<CameraFrameEventArgs>? FrameGrabbed;

    protected BaseCamera(string id, string name, ILogger? logger = null)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        _logger = logger ?? NullLogger.Instance;
    }

    public virtual Task ConnectAsync(CancellationToken ct = default)
    {
        lock (_stateLock)
        {
            if (State == DeviceConnectionState.Connected)
                return Task.CompletedTask;

            State = DeviceConnectionState.Connecting;
        }

        try
        {
            OnConnect(ct);
            lock (_stateLock)
            {
                State = DeviceConnectionState.Connected;
            }
            _logger.LogInformation("Camera {CameraId} ({CameraName}) connected successfully.", Id, Name);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            lock (_stateLock)
            {
                State = DeviceConnectionState.Faulted;
            }
            _logger.LogError(ex, "Failed to connect camera {CameraId}.", Id);
            throw;
        }
    }

    public virtual async Task DisconnectAsync(CancellationToken ct = default)
    {
        await StopContinuousGrabbingAsync(ct);

        lock (_stateLock)
        {
            if (State == DeviceConnectionState.Disconnected)
                return;
        }

        try
        {
            OnDisconnect();
        }
        finally
        {
            lock (_stateLock)
            {
                State = DeviceConnectionState.Disconnected;
            }
            _logger.LogInformation("Camera {CameraId} disconnected.", Id);
        }
    }

    public async Task<ImageFrame> GrabFrameAsync(CancellationToken ct = default)
    {
        EnsureConnected();

        var sw = Stopwatch.StartNew();
        try
        {
            var frame = await AcquireSingleFrameAsync(ct);
            sw.Stop();

            lock (Statistics)
            {
                Statistics.FrameCount++;
                Statistics.LastGrabDurationMs = sw.Elapsed.TotalMilliseconds;
            }

            NotifyFrameGrabbed(frame);
            return frame;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            lock (Statistics)
            {
                Statistics.DroppedFrames++;
            }
            _logger.LogError(ex, "Error grabbing frame from camera {CameraId}", Id);
            throw;
        }
    }

    public Task StartContinuousGrabbingAsync(Action<ImageFrame>? onFrame = null, CancellationToken ct = default)
    {
        EnsureConnected();

        lock (_stateLock)
        {
            if (IsAcquiring)
                return Task.CompletedTask;

            _continuousCallback = onFrame;
            _continuousGrabCts = new CancellationTokenSource();
            _continuousGrabTask = Task.Run(() => ContinuousAcquisitionWorkerAsync(_continuousGrabCts.Token), CancellationToken.None);
        }

        _logger.LogInformation("Continuous grabbing started for camera {CameraId} at target FPS {Fps}.", Id, Parameters.FrameRate);
        return Task.CompletedTask;
    }

    public async Task StopContinuousGrabbingAsync(CancellationToken ct = default)
    {
        Task? taskToWait = null;
        lock (_stateLock)
        {
            if (_continuousGrabCts != null)
            {
                _continuousGrabCts.Cancel();
                taskToWait = _continuousGrabTask;
            }
        }

        if (taskToWait != null)
        {
            try
            {
                await taskToWait.WaitAsync(TimeSpan.FromSeconds(3), ct);
            }
            catch (Exception ex) when (ex is OperationCanceledException or TimeoutException)
            {
                // Expected during abort
            }
            finally
            {
                lock (_stateLock)
                {
                    _continuousGrabCts?.Dispose();
                    _continuousGrabCts = null;
                    _continuousGrabTask = null;
                    _continuousCallback = null;
                }
            }
        }

        _logger.LogInformation("Continuous grabbing stopped for camera {CameraId}.", Id);
    }

    public Task TriggerAsync(CancellationToken ct = default)
    {
        EnsureConnected();
        _softwareTriggerSignal.Release();
        return Task.CompletedTask;
    }

    public virtual Task SetParameterAsync(string name, object value, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        switch (name.ToLowerInvariant())
        {
            case "width":
                Parameters.Width = Convert.ToInt32(value);
                break;
            case "height":
                Parameters.Height = Convert.ToInt32(value);
                break;
            case "exposuretime":
            case "exposure":
                Parameters.ExposureTime = Convert.ToDouble(value);
                break;
            case "gain":
                Parameters.Gain = Convert.ToDouble(value);
                break;
            case "framerate":
            case "fps":
                Parameters.FrameRate = Math.Max(0.1, Convert.ToDouble(value));
                break;
            case "triggermode":
                if (value is CameraTriggerMode mode)
                    Parameters.TriggerMode = mode;
                else if (Enum.TryParse<CameraTriggerMode>(value?.ToString(), true, out var parsedMode))
                    Parameters.TriggerMode = parsedMode;
                break;
            case "pixelformat":
                if (value is PixelFormat fmt)
                    Parameters.PixelFormat = fmt;
                else if (Enum.TryParse<PixelFormat>(value?.ToString(), true, out var parsedFmt))
                    Parameters.PixelFormat = parsedFmt;
                break;
            default:
                Parameters.CustomFeatures[name] = value;
                break;
        }

        return Task.CompletedTask;
    }

    public virtual Task<T> GetParameterAsync<T>(string name, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        object? val = name.ToLowerInvariant() switch
        {
            "width" => Parameters.Width,
            "height" => Parameters.Height,
            "exposuretime" or "exposure" => Parameters.ExposureTime,
            "gain" => Parameters.Gain,
            "framerate" or "fps" => Parameters.FrameRate,
            "triggermode" => Parameters.TriggerMode,
            "pixelformat" => Parameters.PixelFormat,
            _ => Parameters.CustomFeatures.TryGetValue(name, out var customVal) ? customVal : null
        };

        if (val == null)
        {
            throw new KeyNotFoundException($"Camera parameter '{name}' not found on {Id}.");
        }

        return Task.FromResult((T)Convert.ChangeType(val, typeof(T)));
    }

    public virtual async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        _softwareTriggerSignal.Dispose();
        GC.SuppressFinalize(this);
    }

    protected abstract void OnConnect(CancellationToken ct);
    protected abstract void OnDisconnect();
    protected abstract Task<ImageFrame> AcquireSingleFrameAsync(CancellationToken ct);

    protected void EnsureConnected()
    {
        if (State != DeviceConnectionState.Connected)
        {
            throw new InvalidOperationException($"Camera '{Id}' is not connected (Current State: {State}). Call ConnectAsync first.");
        }
    }

    protected void NotifyFrameGrabbed(ImageFrame frame)
    {
        CameraStatistics statsSnapshot;
        lock (Statistics)
        {
            statsSnapshot = Statistics.Clone();
        }

        FrameGrabbed?.Invoke(this, new CameraFrameEventArgs(frame, statsSnapshot));
        _continuousCallback?.Invoke(frame);
    }

    private async Task ContinuousAcquisitionWorkerAsync(CancellationToken ct)
    {
        var fpsStopwatch = Stopwatch.StartNew();
        long lastFrameCount = 0;
        var frameIntervalSw = Stopwatch.StartNew();

        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (Parameters.TriggerMode == CameraTriggerMode.Software)
                {
                    // Wait for software trigger signal
                    await _softwareTriggerSignal.WaitAsync(ct);
                }

                var sw = Stopwatch.StartNew();
                var frame = await AcquireSingleFrameAsync(ct);
                sw.Stop();

                lock (Statistics)
                {
                    Statistics.FrameCount++;
                    Statistics.LastGrabDurationMs = sw.Elapsed.TotalMilliseconds;
                }

                NotifyFrameGrabbed(frame);

                // Update FPS calculation every 500ms
                if (fpsStopwatch.ElapsedMilliseconds >= 500)
                {
                    var elapsedSec = fpsStopwatch.Elapsed.TotalSeconds;
                    var frameDiff = Statistics.FrameCount - lastFrameCount;
                    lock (Statistics)
                    {
                        Statistics.CurrentFps = Math.Round(frameDiff / elapsedSec, 1);
                    }
                    lastFrameCount = Statistics.FrameCount;
                    fpsStopwatch.Restart();
                }

                // Throttle frame rate based on Parameters.FrameRate in FreeRun mode
                if (Parameters.TriggerMode == CameraTriggerMode.FreeRun && Parameters.FrameRate > 0)
                {
                    var targetIntervalMs = 1000.0 / Parameters.FrameRate;
                    var elapsedMs = frameIntervalSw.Elapsed.TotalMilliseconds;
                    var sleepMs = targetIntervalMs - elapsedMs;

                    if (sleepMs > 1.0)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(sleepMs), ct);
                    }
                    frameIntervalSw.Restart();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                lock (Statistics)
                {
                    Statistics.DroppedFrames++;
                }
                _logger.LogWarning(ex, "Error in continuous acquisition loop for camera {CameraId}", Id);
                await Task.Delay(50, ct); // Avoid tight spinning on error
            }
        }
    }
}
