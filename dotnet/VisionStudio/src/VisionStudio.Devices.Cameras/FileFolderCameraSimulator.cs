using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Devices.Abstractions;

namespace VisionStudio.Devices.Cameras;

/// <summary>
/// Camera simulator driver that loads and streams sequential image files from a local directory.
/// Gracefully falls back to synthetic test frame generation if the folder is missing or empty.
/// </summary>
public sealed class FileFolderCameraSimulator : BaseCamera
{
    private readonly List<string> _imageFiles = new();
    private int _currentIndex;
    private long _frameIndex;

    public const string FeatureFolderPath = "FolderPath";
    public const string FeatureLoop = "Loop";
    public const string FeatureCurrentIndex = "CurrentIndex";
    public const string FeatureTotalImages = "TotalImages";

    public string FolderPath
    {
        get => Parameters.CustomFeatures.TryGetValue(FeatureFolderPath, out var p) ? p?.ToString() ?? string.Empty : string.Empty;
        set
        {
            Parameters.CustomFeatures[FeatureFolderPath] = value;
            ScanFolder();
        }
    }

    public bool Loop
    {
        get => Parameters.CustomFeatures.TryGetValue(FeatureLoop, out var l) && Convert.ToBoolean(l);
        set => Parameters.CustomFeatures[FeatureLoop] = value;
    }

    public int CurrentIndex => _currentIndex;
    public int TotalImages => _imageFiles.Count;

    public FileFolderCameraSimulator(string id = "SIM_FOLDER", string name = "File & Folder Camera Simulator", string? initialFolder = null)
        : base(id, name)
    {
        Parameters.Width = 640;
        Parameters.Height = 480;
        Parameters.FrameRate = 10.0;
        Parameters.CustomFeatures[FeatureFolderPath] = initialFolder ?? string.Empty;
        Parameters.CustomFeatures[FeatureLoop] = true;
        Parameters.CustomFeatures[FeatureCurrentIndex] = 0;
        Parameters.CustomFeatures[FeatureTotalImages] = 0;

        ScanFolder();
    }

    public void ScanFolder()
    {
        _imageFiles.Clear();
        var path = FolderPath;

        if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
        {
            var validExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".bmp", ".png", ".jpg", ".jpeg", ".tif", ".tiff"
            };

            var files = Directory.GetFiles(path)
                .Where(f => validExtensions.Contains(Path.GetExtension(f)))
                .OrderBy(f => f)
                .ToList();

            _imageFiles.AddRange(files);
        }

        _currentIndex = 0;
        Parameters.CustomFeatures[FeatureTotalImages] = _imageFiles.Count;
        Parameters.CustomFeatures[FeatureCurrentIndex] = _currentIndex;
    }

    public void NextFrame()
    {
        if (_imageFiles.Count == 0) return;
        _currentIndex = (_currentIndex + 1) % _imageFiles.Count;
        Parameters.CustomFeatures[FeatureCurrentIndex] = _currentIndex;
    }

    public void PreviousFrame()
    {
        if (_imageFiles.Count == 0) return;
        _currentIndex = (_currentIndex - 1 + _imageFiles.Count) % _imageFiles.Count;
        Parameters.CustomFeatures[FeatureCurrentIndex] = _currentIndex;
    }

    public void JumpTo(int index)
    {
        if (_imageFiles.Count == 0) return;
        _currentIndex = Math.Clamp(index, 0, _imageFiles.Count - 1);
        Parameters.CustomFeatures[FeatureCurrentIndex] = _currentIndex;
    }

    protected override void OnConnect(CancellationToken ct)
    {
        ScanFolder();
        _frameIndex = 0;
    }

    protected override void OnDisconnect()
    {
    }

    protected override Task<ImageFrame> AcquireSingleFrameAsync(CancellationToken ct)
    {
        var frameId = Interlocked.Increment(ref _frameIndex);

        if (_imageFiles.Count > 0)
        {
            var file = _imageFiles[_currentIndex];
            Mat rawMat;
            try
            {
                var loadMode = Parameters.PixelFormat == PixelFormat.Mono8
                    ? ImreadModes.Grayscale
                    : ImreadModes.Color;

                rawMat = Cv2.ImRead(file, loadMode);
            }
            catch
            {
                rawMat = GenerateFallbackFrame(frameId, Path.GetFileName(file));
            }

            if (rawMat == null || rawMat.Empty())
            {
                rawMat = GenerateFallbackFrame(frameId, Path.GetFileName(file));
            }

            // Advance index
            if (Loop)
            {
                _currentIndex = (_currentIndex + 1) % _imageFiles.Count;
            }
            else
            {
                _currentIndex = Math.Min(_currentIndex + 1, _imageFiles.Count - 1);
            }
            Parameters.CustomFeatures[FeatureCurrentIndex] = _currentIndex;

            var frame = new ImageFrame(rawMat, cameraId: Id, frameId: frameId);
            return Task.FromResult(frame);
        }
        else
        {
            // Fallback generation when no files present
            var rawMat = GenerateFallbackFrame(frameId, "Folder Empty (Simulated Frame)");
            var frame = new ImageFrame(rawMat, cameraId: Id, frameId: frameId);
            return Task.FromResult(frame);
        }
    }

    private Mat GenerateFallbackFrame(long frameIndex, string caption)
    {
        var width = Math.Max(64, Parameters.Width);
        var height = Math.Max(64, Parameters.Height);

        var mat = new Mat(height, width, MatType.CV_8UC3, new Scalar(35, 30, 42));

        // Background grid pattern
        for (int y = 0; y < height; y += 40)
        {
            Cv2.Line(mat, new Point(0, y), new Point(width, y), new Scalar(50, 45, 60), 1);
        }
        for (int x = 0; x < width; x += 40)
        {
            Cv2.Line(mat, new Point(x, 0), new Point(x, height), new Scalar(50, 45, 60), 1);
        }

        // Test part rectangle
        var rectW = width / 2;
        var rectH = height / 3;
        var rx = (width - rectW) / 2;
        var ry = (height - rectH) / 2;
        Cv2.Rectangle(mat, new Rect(rx, ry, rectW, rectH), new Scalar(160, 170, 185), -1);
        Cv2.Rectangle(mat, new Rect(rx, ry, rectW, rectH), new Scalar(230, 235, 245), 2);

        // Watermark text
        Cv2.PutText(mat, "FILE/FOLDER CAMERA SIMULATOR", new Point(20, 30), HersheyFonts.HersheySimplex, 0.6, new Scalar(255, 200, 50), 2);
        Cv2.PutText(mat, $"Frame #{frameIndex} | {caption}", new Point(20, 60), HersheyFonts.HersheySimplex, 0.45, new Scalar(200, 200, 200), 1);

        if (Parameters.PixelFormat == PixelFormat.Mono8)
        {
            var mono = new Mat();
            Cv2.CvtColor(mat, mono, ColorConversionCodes.BGR2GRAY);
            mat.Dispose();
            mat = mono;
        }

        return mat;
    }
}
