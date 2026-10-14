using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Acquisition;

/// <summary>
/// Grabs frames from a live or simulated industrial camera device.
/// Emits synthetic calibration patterns when no physical hardware is attached.
/// </summary>
[VisionTool("Acquisition.CameraGrab", "Camera Grab", "Acquisition", Description = "Acquires a single frame from an industrial or simulated camera")]
public sealed class CameraGrabTool : VisionToolBase
{
    public const string ParamCameraId = "CameraId";
    public const string ParamWidth = "Width";
    public const string ParamHeight = "Height";
    public const string ParamPattern = "Pattern";
    public const string ParamFilePath = "FilePath";

    public const string PortImage = "Image";
    public const string PortFrameId = "FrameId";

    private readonly ToolParameter _pCamId;
    private readonly ToolParameter _pWidth;
    private readonly ToolParameter _pHeight;
    private readonly ToolParameter _pPattern;
    private readonly ToolParameter _pFilePath;

    private long _frameCounter;

    public CameraGrabTool() : base("Acquisition.CameraGrab", "Camera Grab", "Acquisition", "Acquires a frame from camera")
    {
        _pCamId = AddParameter(new ToolParameter(ParamCameraId, "Camera ID", ParameterType.String, "SIM_CAM_01"));
        _pWidth = AddParameter(new ToolParameter(ParamWidth, "Width", ParameterType.Integer, 640, min: 64, max: 8192));
        _pHeight = AddParameter(new ToolParameter(ParamHeight, "Height", ParameterType.Integer, 480, min: 64, max: 8192));
        _pFilePath = AddParameter(new ToolParameter(ParamFilePath, "Tệp ảnh thử nghiệm (File Path)", ParameterType.FilePath, string.Empty));
        _pPattern = AddParameter(new ToolParameter(
            ParamPattern,
            "Simulated Pattern",
            ParameterType.Enum,
            "TargetPart",
            enumOptions: new[] { "TargetPart", "Checkerboard", "ColorBars", "Noise" }));

        AddOutput(new ToolPort(PortImage, "Image", PortType.Image, PortDirection.Output));
        AddOutput(new ToolPort(PortFrameId, "Frame ID", PortType.Number, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var camId = _pCamId.GetValue<string>() ?? "SIM_SYNTHETIC";
        var width = _pWidth.GetValue<int>();
        var height = _pHeight.GetValue<int>();
        var pattern = _pPattern.GetValue<string>() ?? "TargetPart";
        var filePath = _pFilePath.GetValue<string>();

        var frameId = Interlocked.Increment(ref _frameCounter);

        ImageFrame? frame = null;

        // 1. If an image file path is provided, load directly from disk
        if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
        {
            try
            {
                var mat = Cv2.ImRead(filePath, ImreadModes.Color);
                if (!mat.Empty())
                {
                    frame = new ImageFrame(mat, cameraId: "FILE_IMAGE", frameId: frameId);
                }
            }
            catch
            {
                frame = null;
            }
        }

        // 2. Otherwise acquire from connected physical/simulated camera
        if (frame == null)
        {
            var camera = VisionStudio.Devices.Cameras.CameraManager.Instance.GetCamera(camId) 
                      ?? VisionStudio.Devices.Cameras.CameraManager.Instance.ActiveCamera;

            if (camera != null)
            {
                try
                {
                    if (width > 0) camera.Parameters.Width = width;
                    if (height > 0) camera.Parameters.Height = height;

                    if (camera.State != VisionStudio.Devices.Abstractions.DeviceConnectionState.Connected)
                    {
                        camera.ConnectAsync(ct).GetAwaiter().GetResult();
                    }

                    frame = camera.GrabFrameAsync(ct).GetAwaiter().GetResult();
                }
                catch
                {
                    // Fall back to synthetic pattern generation
                    frame = null;
                }
            }
        }

        if (frame == null)
        {
            var mat = GeneratePattern(width, height, pattern, frameId);
            frame = new ImageFrame(mat, cameraId: camId, frameId: frameId);
        }

        var outputs = new Dictionary<string, object?>
        {
            [PortImage] = frame,
            [PortFrameId] = frame.FrameId
        };

        return ToolResult.Ok(outputs, message: $"Acquired frame #{frame.FrameId} ({frame.Width}x{frame.Height}) from '{frame.CameraId}'");
    }

    private static Mat GeneratePattern(int width, int height, string pattern, long frameIndex)
    {
        var mat = new Mat(height, width, MatType.CV_8UC3, new Scalar(30, 30, 30));

        switch (pattern.ToLowerInvariant())
        {
            case "colorbars":
                var barWidth = width / 6;
                var colors = new[]
                {
                    new Scalar(255, 0, 0),     // Blue
                    new Scalar(0, 255, 0),     // Green
                    new Scalar(0, 0, 255),     // Red
                    new Scalar(0, 255, 255),   // Yellow
                    new Scalar(255, 0, 255),   // Magenta
                    new Scalar(255, 255, 255)  // White
                };
                for (int i = 0; i < 6; i++)
                {
                    var rect = new Rect(i * barWidth, 0, barWidth, height);
                    Cv2.Rectangle(mat, rect, colors[i], -1);
                }
                break;

            case "checkerboard":
                var size = 40;
                for (int r = 0; r < height; r += size)
                {
                    for (int c = 0; c < width; c += size)
                    {
                        if ((r / size + c / size) % 2 == 0)
                        {
                            var box = new Rect(c, r, Math.Min(size, width - c), Math.Min(size, height - r));
                            Cv2.Rectangle(mat, box, new Scalar(230, 230, 230), -1);
                        }
                    }
                }
                break;

            case "noise":
                Cv2.Randu(mat, new Scalar(0, 0, 0), new Scalar(255, 255, 255));
                break;

            default: // TargetPart
                // Draw industrial machined part with circles and bores
                Cv2.Rectangle(mat, new Rect(width / 4, height / 4, width / 2, height / 2), new Scalar(180, 180, 180), -1);
                Cv2.Circle(mat, new Point(width / 2, height / 2), Math.Min(width, height) / 6, new Scalar(40, 40, 40), -1);
                Cv2.Circle(mat, new Point(width / 3, height / 3), 15, new Scalar(80, 80, 80), -1);
                Cv2.Circle(mat, new Point(width * 2 / 3, height / 3), 15, new Scalar(80, 80, 80), -1);
                break;
        }

        return mat;
    }

    public override void Reset()
    {
        _frameCounter = 0;
    }
}
