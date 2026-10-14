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
public sealed class VideoFileTool : VisionToolBase, IDisposable
{
    public const string ParamFilePath = "FilePath";
    public const string ParamLoop = "Loop";

    public const string PortImage = "Image";
    public const string PortFrameIndex = "FrameIndex";

    private readonly ToolParameter _pFilePath;
    private readonly ToolParameter _pLoop;

    private VideoCapture? _capture;
    private string? _currentPath;
    private int _frameIndex;

    public VideoFileTool() : base("Acquisition.VideoFile", "Video File", "Acquisition", "Decodes frames from a video file")
    {
        _pFilePath = AddParameter(new ToolParameter(ParamFilePath, "File Path", ParameterType.FilePath, string.Empty));
        _pLoop = AddParameter(new ToolParameter(ParamLoop, "Loop Video", ParameterType.Boolean, true));

        AddOutput(new ToolPort(PortImage, "Image", PortType.Image, PortDirection.Output));
        AddOutput(new ToolPort(PortFrameIndex, "Frame Index", PortType.Number, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var path = _pFilePath.GetValue<string>();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return ToolResult.Failed($"Video file not found: '{path}'");
        }

        if (_capture == null || _currentPath != path)
        {
            _capture?.Dispose();
            _capture = new VideoCapture(path);
            _currentPath = path;
            _frameIndex = 0;
        }

        if (!_capture.IsOpened())
        {
            return ToolResult.Failed($"Failed to open video source: '{path}'");
        }

        var mat = new Mat();
        if (!_capture.Read(mat) || mat.Empty())
        {
            if (_pLoop.GetValue<bool>())
            {
                _capture.Set(VideoCaptureProperties.PosFrames, 0);
                if (!_capture.Read(mat) || mat.Empty())
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

        var frame = new ImageFrame(mat, cameraId: "video-file", frameId: _frameIndex);
        var outputs = new Dictionary<string, object?>
        {
            [PortImage] = frame,
            [PortFrameIndex] = _frameIndex
        };

        _frameIndex++;
        return ToolResult.Ok(outputs, message: $"Grabbed video frame #{_frameIndex}");
    }

    public override void Reset()
    {
        _frameIndex = 0;
        _capture?.Set(VideoCaptureProperties.PosFrames, 0);
    }

    public void Dispose()
    {
        _capture?.Dispose();
        _capture = null;
    }
}
