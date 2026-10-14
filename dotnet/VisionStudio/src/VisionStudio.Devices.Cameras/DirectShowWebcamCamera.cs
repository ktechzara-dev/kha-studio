using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Devices.Abstractions;

namespace VisionStudio.Devices.Cameras;

/// <summary>
/// Camera driver for physical USB webcams, industrial DirectShow grabbers, or virtual capture sources (OBS).
/// Seamlessly falls back to synthetic webcam test card simulation when no physical hardware is plugged in.
/// </summary>
public sealed class DirectShowWebcamCamera : BaseCamera
{
    private VideoCapture? _capture;
    private bool _isHardwareEmulated;
    private long _frameIndex;
    private readonly object _captureLock = new();

    public const string FeatureDeviceIndex = "DeviceIndex";
    public const string FeatureIsHardwareActive = "IsHardwareActive";

    public int DeviceIndex
    {
        get => Parameters.CustomFeatures.TryGetValue(FeatureDeviceIndex, out var idx) ? Convert.ToInt32(idx) : 0;
        set => Parameters.CustomFeatures[FeatureDeviceIndex] = value;
    }

    public bool IsHardwareActive => !_isHardwareEmulated && _capture != null && _capture.IsOpened();

    public DirectShowWebcamCamera(string id = "PHYS_WEBCAM_0", string name = "DirectShow USB Webcam", int deviceIndex = 0)
        : base(id, name)
    {
        Parameters.Width = 640;
        Parameters.Height = 480;
        Parameters.FrameRate = 30.0;
        Parameters.CustomFeatures[FeatureDeviceIndex] = deviceIndex;
        Parameters.CustomFeatures[FeatureIsHardwareActive] = false;
    }

    protected override void OnConnect(CancellationToken ct)
    {
        _frameIndex = 0;
        var idx = DeviceIndex;

        lock (_captureLock)
        {
            try
            {
                // Attempt to open native video capture with DirectShow backend on Windows
                _capture = new VideoCapture(idx, VideoCaptureAPIs.DSHOW);

                if (!_capture.IsOpened())
                {
                    _capture.Dispose();
                    // Fallback to ANY API backend (e.g. Media Foundation on modern laptops)
                    _capture = new VideoCapture(idx, VideoCaptureAPIs.ANY);
                }

                if (_capture.IsOpened())
                {
                    // Detect actual hardware resolution
                    var actualW = (int)_capture.Get(VideoCaptureProperties.FrameWidth);
                    var actualH = (int)_capture.Get(VideoCaptureProperties.FrameHeight);
                    if (actualW > 0 && actualH > 0)
                    {
                        Parameters.Width = actualW;
                        Parameters.Height = actualH;
                    }
                    else
                    {
                        _capture.Set(VideoCaptureProperties.FrameWidth, Parameters.Width);
                        _capture.Set(VideoCaptureProperties.FrameHeight, Parameters.Height);
                    }

                    _isHardwareEmulated = false;
                    Parameters.CustomFeatures[FeatureIsHardwareActive] = true;
                }
                else
                {
                    // Physical webcam not present; activate simulated webcam fallback
                    _capture?.Dispose();
                    _capture = null;
                    _isHardwareEmulated = true;
                    Parameters.CustomFeatures[FeatureIsHardwareActive] = false;
                }
            }
            catch
            {
                _capture?.Dispose();
                _capture = null;
                _isHardwareEmulated = true;
                Parameters.CustomFeatures[FeatureIsHardwareActive] = false;
            }
        }
    }

    protected override void OnDisconnect()
    {
        lock (_captureLock)
        {
            if (_capture != null)
            {
                if (_capture.IsOpened())
                {
                    _capture.Release();
                }
                _capture.Dispose();
                _capture = null;
            }
            _isHardwareEmulated = false;
            Parameters.CustomFeatures[FeatureIsHardwareActive] = false;
        }
    }

    protected override Task<ImageFrame> AcquireSingleFrameAsync(CancellationToken ct)
    {
        var frameId = Interlocked.Increment(ref _frameIndex);
        Mat outMat;

        lock (_captureLock)
        {
            if (!_isHardwareEmulated && _capture != null && _capture.IsOpened())
            {
                var mat = new Mat();
                if (_capture.Read(mat) && !mat.Empty())
                {
                    outMat = mat;
                }
                else
                {
                    mat.Dispose();
                    outMat = GenerateSimulatedWebcamFrame(frameId);
                }
            }
            else
            {
                outMat = GenerateSimulatedWebcamFrame(frameId);
            }
        }

        if (Parameters.PixelFormat == PixelFormat.Mono8 && outMat.Channels() == 3)
        {
            var mono = new Mat();
            Cv2.CvtColor(outMat, mono, ColorConversionCodes.BGR2GRAY);
            outMat.Dispose();
            outMat = mono;
        }

        var frame = new ImageFrame(outMat, cameraId: Id, frameId: frameId);
        return Task.FromResult(frame);
    }

    private Mat GenerateSimulatedWebcamFrame(long frameIndex)
    {
        var width = Math.Max(64, Parameters.Width);
        var height = Math.Max(64, Parameters.Height);

        var mat = new Mat(height, width, MatType.CV_8UC3, new Scalar(22, 24, 28));

        // Draw camera viewfinder border brackets
        var margin = 25;
        var bracketLen = 30;
        var bracketColor = new Scalar(0, 200, 255);

        // Top-left
        Cv2.Line(mat, new Point(margin, margin), new Point(margin + bracketLen, margin), bracketColor, 2);
        Cv2.Line(mat, new Point(margin, margin), new Point(margin, margin + bracketLen), bracketColor, 2);
        // Top-right
        Cv2.Line(mat, new Point(width - margin, margin), new Point(width - margin - bracketLen, margin), bracketColor, 2);
        Cv2.Line(mat, new Point(width - margin, margin), new Point(width - margin, margin + bracketLen), bracketColor, 2);
        // Bottom-left
        Cv2.Line(mat, new Point(margin, height - margin), new Point(margin + bracketLen, height - margin), bracketColor, 2);
        Cv2.Line(mat, new Point(margin, height - margin), new Point(margin, height - margin - bracketLen), bracketColor, 2);
        // Bottom-right
        Cv2.Line(mat, new Point(width - margin, height - margin), new Point(width - margin - bracketLen, height - margin), bracketColor, 2);
        Cv2.Line(mat, new Point(width - margin, height - margin), new Point(width - margin, height - margin - bracketLen), bracketColor, 2);

        // Simulated focus reticle in center
        var cx = width / 2;
        var cy = height / 2;
        Cv2.Circle(mat, new Point(cx, cy), 45, bracketColor, 1);
        Cv2.Line(mat, new Point(cx - 15, cy), new Point(cx + 15, cy), bracketColor, 1);
        Cv2.Line(mat, new Point(cx, cy - 15), new Point(cx, cy + 15), bracketColor, 1);

        // Informational overlay
        Cv2.PutText(mat, $"DIRECTSHOW USB WEBCAM [DEV #{DeviceIndex}]", new Point(35, 45), HersheyFonts.HersheySimplex, 0.5, new Scalar(255, 255, 255), 1);
        Cv2.PutText(mat, $"Frame #{frameIndex} | {(IsHardwareActive ? "Hardware Active" : "Emulated Feed")}", new Point(35, 70), HersheyFonts.HersheySimplex, 0.45, new Scalar(160, 200, 255), 1);

        return mat;
    }
}
