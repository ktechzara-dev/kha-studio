using OpenCvSharp;

namespace VisionStudio.Core.Models;

/// <summary>
/// Encapsulates an OpenCV Mat with industrial metadata and precise lifecycle semantics.
/// </summary>
/// <remarks>
/// <para><b>Ownership and Disposal Rules:</b></para>
/// <list type="bullet">
///   <item>
///     <description><b>Producer Ownership:</b> Any component (Camera, Tool) creating an <see cref="ImageFrame"/>
///     owns the underlying native <see cref="Mat"/> allocation until transferred to a consumer.</description>
///   </item>
///   <item>
///     <description><b>Deep Copying:</b> When an image passes between independent asynchronous nodes or caches,
///     call <see cref="Clone"/> to ensure the consumer owns its private native memory buffer.</description>
///   </item>
///   <item>
///     <description><b>Disposal:</b> Calling <see cref="Dispose"/> safely releases the unmanaged Mat handle.
///     Accessing a disposed frame throws <see cref="ObjectDisposedException"/>.</description>
///   </item>
/// </list>
/// </remarks>
public sealed class ImageFrame : IDisposable
{
    private Mat? _mat;
    private bool _isDisposed;
    private readonly object _lock = new();

    /// <summary>
    /// Gets the underlying OpenCV Mat.
    /// </summary>
    public Mat Mat
    {
        get
        {
            ThrowIfDisposed();
            return _mat!;
        }
    }

    /// <summary>
    /// Acquisition UTC timestamp.
    /// </summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Identifier of the producing camera or source.
    /// </summary>
    public string CameraId { get; }

    /// <summary>
    /// Monotonically increasing frame index.
    /// </summary>
    public long FrameId { get; }

    /// <summary>
    /// Logical pixel format.
    /// </summary>
    public PixelFormat Format { get; }

    /// <summary>
    /// Optional calibration model reference or matrix.
    /// </summary>
    public object? CalibrationReference { get; init; }

    /// <summary>
    /// Width in pixels.
    /// </summary>
    public int Width => ThrowIfDisposed().Cols;

    /// <summary>
    /// Height in pixels.
    /// </summary>
    public int Height => ThrowIfDisposed().Rows;

    /// <summary>
    /// Number of channels (e.g. 1 for grayscale, 3 for BGR).
    /// </summary>
    public int Channels => ThrowIfDisposed().Channels();

    /// <summary>
    /// Indicates whether unmanaged native resources have been released.
    /// </summary>
    public bool IsDisposed => _isDisposed;

    public ImageFrame(
        Mat mat,
        string cameraId = "simulated",
        long frameId = 0,
        PixelFormat? format = null,
        DateTimeOffset? timestamp = null,
        object? calibrationReference = null)
    {
        _mat = mat ?? throw new ArgumentNullException(nameof(mat));
        CameraId = cameraId;
        FrameId = frameId;
        Timestamp = timestamp ?? DateTimeOffset.UtcNow;
        CalibrationReference = calibrationReference;

        Format = format ?? InferPixelFormat(mat);
    }

    /// <summary>
    /// Creates a deep copy of the frame including a duplicated native OpenCV Mat.
    /// </summary>
    public ImageFrame Clone()
    {
        ThrowIfDisposed();
        lock (_lock)
        {
            ThrowIfDisposed();
            var clonedMat = _mat!.Clone();
            return new ImageFrame(clonedMat, CameraId, FrameId, Format, Timestamp, CalibrationReference);
        }
    }

    private static PixelFormat InferPixelFormat(Mat mat)
    {
        if (mat.Channels() == 1)
        {
            return mat.Depth() == MatType.CV_16U ? PixelFormat.Mono16 : PixelFormat.Mono8;
        }

        if (mat.Channels() == 3)
        {
            return PixelFormat.Bgr24;
        }

        if (mat.Channels() == 4)
        {
            return PixelFormat.Bgra32;
        }

        return PixelFormat.Bgr24;
    }

    private Mat ThrowIfDisposed()
    {
        if (_isDisposed || _mat == null || _mat.IsDisposed)
        {
            throw new ObjectDisposedException(nameof(ImageFrame), "The underlying OpenCV Mat has been disposed.");
        }
        return _mat;
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _mat?.Dispose();
            _mat = null;
        }
    }
}
