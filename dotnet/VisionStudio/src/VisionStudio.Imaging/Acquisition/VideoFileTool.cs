using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Acquisition;

/// <summary>
/// Plays back an encoded video container (AVI/MP4/MKV) frame by frame.
/// </summary>
[VisionTool("Acquisition.VideoFile", "Video File", "Acquisition", Description = "Decodes frames sequentially from a video file")]
public sealed class VideoFileTool : VisionToolBase, IVideoSeekable, IDisposable
{
    public const string ParamFilePath = "FilePath";
    public const string ParamLoop = "Loop";
    public const string ParamPosFrames = "PosFrames";

    public const string PortImage = "Image";
    public const string PortFrameIndex = "FrameIndex";
    public const string PortTotalFrames = "TotalFrames";

    private readonly ToolParameter _pFilePath;
    private readonly ToolParameter _pLoop;
    private readonly ToolParameter _pPosFrames;

    private readonly object _syncLock = new();
    private VideoCapture? _capture;
    private string? _currentPath;
    private int _frameIndex;
    private int _targetSeekFrame = -1;
    private int _cachedTotalFrames;
    private double _cachedFps = 30.0;

    public int CurrentFrame
    {
        get { lock (_syncLock) { return _frameIndex; } }
    }

    public int TotalFrames
    {
        get { lock (_syncLock) { return _cachedTotalFrames; } }
    }

    public double Fps
    {
        get { lock (_syncLock) { return _cachedFps; } }
    }

    public bool IsVideoLoaded
    {
        get { lock (_syncLock) { return _capture != null && _capture.IsOpened(); } }
    }

    public VideoFileTool() : base("Acquisition.VideoFile", "Video File", "Acquisition", "Decodes frames from a video file")
    {
        _pFilePath = AddParameter(new ToolParameter(ParamFilePath, "File Path", ParameterType.FilePath, string.Empty));
        _pLoop = AddParameter(new ToolParameter(ParamLoop, "Loop Video", ParameterType.Boolean, true));
        _pPosFrames = AddParameter(new ToolParameter(ParamPosFrames, "Frame Position", ParameterType.Double, 0.0));

        AddOutput(new ToolPort(PortImage, "Image", PortType.Image, PortDirection.Output));
        AddOutput(new ToolPort(PortFrameIndex, "Frame Index", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortTotalFrames, "Total Frames", PortType.Number, PortDirection.Output));
    }

    public void Seek(int frameIndex)
    {
        lock (_syncLock)
        {
            int total = _cachedTotalFrames;
            int clamped = total > 0 ? Math.Clamp(frameIndex, 0, Math.Max(0, total - 1)) : Math.Max(0, frameIndex);
            _targetSeekFrame = clamped;
            _frameIndex = clamped;
            _pPosFrames.Value = (double)clamped;
        }
    }

    public void Restart()
    {
        Seek(0);
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var path = _pFilePath.GetValue<string>();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return ToolResult.Failed($"Video file not found: '{path}'");
        }

        var mat = new Mat();
        int curFrame;
        int totFrames;

        lock (_syncLock)
        {
            if (_capture == null || _currentPath != path)
            {
                _capture?.Dispose();
                _capture = new VideoCapture(path);
                _currentPath = path;
                _frameIndex = 0;
                _targetSeekFrame = -1;
                if (_capture.IsOpened())
                {
                    _cachedTotalFrames = (int)_capture.Get(VideoCaptureProperties.FrameCount);
                    _cachedFps = Math.Max(1.0, _capture.Get(VideoCaptureProperties.Fps));
                }
                else
                {
                    _cachedTotalFrames = 0;
                    _cachedFps = 30.0;
                }
            }

            if (!_capture.IsOpened())
            {
                mat.Dispose();
                return ToolResult.Failed($"Failed to open video source: '{path}'");
            }

            totFrames = _cachedTotalFrames;

            if (_targetSeekFrame >= 0)
            {
                int clamped = totFrames > 0 ? Math.Clamp(_targetSeekFrame, 0, Math.Max(0, totFrames - 1)) : _targetSeekFrame;
                _targetSeekFrame = -1;
                try
                {
                    _capture.Set(VideoCaptureProperties.PosFrames, clamped);
                    _frameIndex = clamped;
                }
                catch
                {
                    // Ignore seek errors on tricky codecs
                }
            }

            bool readSuccess = false;
            try
            {
                readSuccess = _capture.Read(mat) && !mat.Empty();
            }
            catch
            {
                readSuccess = false;
            }

            if (!readSuccess)
            {
                if (_pLoop.GetValue<bool>())
                {
                    try
                    {
                        _capture.Set(VideoCaptureProperties.PosFrames, 0);
                        readSuccess = _capture.Read(mat) && !mat.Empty();
                    }
                    catch
                    {
                        readSuccess = false;
                    }

                    if (!readSuccess)
                    {
                        mat.Dispose();
                        return ToolResult.Failed("Failed to rewind and read loop frame from video.");
                    }
                    _frameIndex = 0;
                }
                else
                {
                    mat.Dispose();
                    return ToolResult.Skipped("End of video stream reached.");
                }
            }

            curFrame = _frameIndex;
            _frameIndex++;
            _pPosFrames.Value = (double)_frameIndex;
        }

        var frame = new ImageFrame(mat, cameraId: "video-file", frameId: curFrame);
        var outputs = new Dictionary<string, object?>
        {
            [PortImage] = frame,
            [PortFrameIndex] = curFrame,
            [PortTotalFrames] = totFrames
        };

        return ToolResult.Ok(outputs, message: $"Grabbed video frame #{curFrame}/{totFrames}");
    }

    public override void Reset()
    {
        lock (_syncLock)
        {
            _frameIndex = 0;
            _targetSeekFrame = 0;
            _pPosFrames.Value = 0.0;
        }
    }

    public void Dispose()
    {
        lock (_syncLock)
        {
            _capture?.Dispose();
            _capture = null;
            _cachedTotalFrames = 0;
        }
    }
}
