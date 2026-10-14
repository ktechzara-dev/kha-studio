using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Devices.Abstractions;

namespace VisionStudio.Devices.Cameras;

/// <summary>
/// Universal GigE Vision camera simulator implementing GenICam SFNC (Standard Features Naming Convention).
/// Emulates network transport layer (MTU/jumbo frames, IP/MAC addresses, heartbeat timer)
/// and acquisition modes (FreeRun, Software Trigger, SFNC Test Patterns).
/// </summary>
public sealed class GenICamGigEUniversalSimulator : BaseCamera
{
    private long _frameIndex;
    private DateTime _lastHeartbeatTime = DateTime.UtcNow;

    // GenICam SFNC Feature Names
    public const string SFNC_DeviceVendorName = "DeviceVendorName";
    public const string SFNC_DeviceModelName = "DeviceModelName";
    public const string SFNC_DeviceSerialNumber = "DeviceSerialNumber";
    public const string SFNC_DeviceFirmwareVersion = "DeviceFirmwareVersion";
    public const string SFNC_GevCurrentIPAddress = "GevCurrentIPAddress";
    public const string SFNC_GevMACAddress = "GevMACAddress";
    public const string SFNC_GevSCPSPacketSize = "GevSCPSPacketSize";
    public const string SFNC_GevHeartbeatTimeout = "GevHeartbeatTimeout";
    public const string SFNC_TriggerMode = "TriggerMode";
    public const string SFNC_TriggerSource = "TriggerSource";
    public const string SFNC_TriggerSoftware = "TriggerSoftware";
    public const string SFNC_TestPattern = "TestPattern";

    public string DeviceVendorName => Parameters.CustomFeatures[SFNC_DeviceVendorName]?.ToString() ?? "VisionStudio Industrial";
    public string DeviceModelName => Parameters.CustomFeatures[SFNC_DeviceModelName]?.ToString() ?? "VS-GigE-5000M";
    public string DeviceSerialNumber => Parameters.CustomFeatures[SFNC_DeviceSerialNumber]?.ToString() ?? "VS2026-GIGE-8821";
    public string CurrentIpAddress => Parameters.CustomFeatures[SFNC_GevCurrentIPAddress]?.ToString() ?? "192.168.1.100";
    public int PacketSize => Convert.ToInt32(Parameters.CustomFeatures[SFNC_GevSCPSPacketSize]);

    public GenICamGigEUniversalSimulator(string id = "SIM_GIGE_01", string name = "GigE Vision GenICam Simulator (VS-GigE-5000M)")
        : base(id, name)
    {
        Parameters.Width = 640;
        Parameters.Height = 480;
        Parameters.FrameRate = 25.0;
        Parameters.ExposureTime = 20000.0;
        Parameters.Gain = 0.0;
        Parameters.PixelFormat = PixelFormat.Mono8;

        // Populate GenICam SFNC Node Tree
        Parameters.CustomFeatures[SFNC_DeviceVendorName] = "VisionStudio Industrial";
        Parameters.CustomFeatures[SFNC_DeviceModelName] = "VS-GigE-5000M";
        Parameters.CustomFeatures[SFNC_DeviceSerialNumber] = "VS2026-GIGE-8821";
        Parameters.CustomFeatures[SFNC_DeviceFirmwareVersion] = "v2.6.4-build2026";
        Parameters.CustomFeatures[SFNC_GevCurrentIPAddress] = "192.168.1.100";
        Parameters.CustomFeatures[SFNC_GevMACAddress] = "00:1B:44:11:3A:B7";
        Parameters.CustomFeatures[SFNC_GevSCPSPacketSize] = 1500; // Standard MTU (or 9000 for Jumbo Frames)
        Parameters.CustomFeatures[SFNC_GevHeartbeatTimeout] = 3000; // 3000 ms
        Parameters.CustomFeatures[SFNC_TriggerMode] = "Off"; // Off (FreeRun) or On
        Parameters.CustomFeatures[SFNC_TriggerSource] = "Software"; // Software or Line1
        Parameters.CustomFeatures[SFNC_TestPattern] = "SyntheticIndustrialPart"; // Off, GreyHorizontalRamp, Checkerboard, SyntheticIndustrialPart
    }

    protected override void OnConnect(CancellationToken ct)
    {
        _frameIndex = 0;
        _lastHeartbeatTime = DateTime.UtcNow;
    }

    protected override void OnDisconnect()
    {
    }

    /// <summary>
    /// Emulates GenICam GEV Heartbeat ping from host application.
    /// </summary>
    public bool PingHeartbeat()
    {
        if (State != DeviceConnectionState.Connected)
            return false;

        _lastHeartbeatTime = DateTime.UtcNow;
        return true;
    }

    public override Task SetParameterAsync(string name, object value, CancellationToken ct = default)
    {
        if (name.Equals(SFNC_TriggerMode, StringComparison.OrdinalIgnoreCase))
        {
            var modeStr = value?.ToString();
            Parameters.TriggerMode = (modeStr?.Equals("On", StringComparison.OrdinalIgnoreCase) == true)
                ? CameraTriggerMode.Software
                : CameraTriggerMode.FreeRun;
        }

        if (name.Equals(SFNC_TriggerSoftware, StringComparison.OrdinalIgnoreCase))
        {
            return TriggerAsync(ct);
        }

        return base.SetParameterAsync(name, value ?? string.Empty, ct);
    }

    protected override Task<ImageFrame> AcquireSingleFrameAsync(CancellationToken ct)
    {
        var frameId = Interlocked.Increment(ref _frameIndex);
        var width = Math.Max(64, Parameters.Width);
        var height = Math.Max(64, Parameters.Height);

        var testPattern = Parameters.CustomFeatures.TryGetValue(SFNC_TestPattern, out var p)
            ? p?.ToString() ?? "SyntheticIndustrialPart"
            : "SyntheticIndustrialPart";

        Mat rawMat = testPattern.ToLowerInvariant() switch
        {
            "greyhorizontalramp" or "ramp" => GenerateHorizontalRamp(width, height),
            "checkerboard" => GenerateCheckerboard(width, height),
            _ => GenerateIndustrialPart(width, height, frameId)
        };

        // Convert to Mono8 if requested
        if (Parameters.PixelFormat == PixelFormat.Mono8 && rawMat.Channels() == 3)
        {
            var mono = new Mat();
            Cv2.CvtColor(rawMat, mono, ColorConversionCodes.BGR2GRAY);
            rawMat.Dispose();
            rawMat = mono;
        }

        var frame = new ImageFrame(rawMat, cameraId: Id, frameId: frameId);
        return Task.FromResult(frame);
    }

    private static Mat GenerateHorizontalRamp(int width, int height)
    {
        var mat = new Mat(height, width, MatType.CV_8UC1);
        for (int x = 0; x < width; x++)
        {
            byte val = (byte)((x * 255) / Math.Max(1, width - 1));
            mat.Col(x).SetTo(new Scalar(val));
        }
        return mat;
    }

    private static Mat GenerateCheckerboard(int width, int height)
    {
        var mat = new Mat(height, width, MatType.CV_8UC1, new Scalar(255));
        var sq = 32;
        for (int y = 0; y < height; y += sq)
        {
            for (int x = 0; x < width; x += sq)
            {
                if (((x / sq) + (y / sq)) % 2 == 1)
                {
                    var rect = new Rect(x, y, Math.Min(sq, width - x), Math.Min(sq, height - y));
                    Cv2.Rectangle(mat, rect, new Scalar(0), -1);
                }
            }
        }
        return mat;
    }

    private static Mat GenerateIndustrialPart(int width, int height, long frameIndex)
    {
        var mat = new Mat(height, width, MatType.CV_8UC3, new Scalar(30, 32, 36));

        // Part center
        var cx = width / 2;
        var cy = height / 2;
        var partW = Math.Min(width * 0.7, 320);
        var partH = Math.Min(height * 0.6, 200);

        var rx = (int)(cx - partW / 2);
        var ry = (int)(cy - partH / 2);

        // Industrial aluminum chassis
        Cv2.Rectangle(mat, new Rect(rx, ry, (int)partW, (int)partH), new Scalar(170, 175, 180), -1);
        Cv2.Rectangle(mat, new Rect(rx, ry, (int)partW, (int)partH), new Scalar(220, 225, 230), 2);

        // Center aperture
        Cv2.Circle(mat, new Point(cx, cy), 35, new Scalar(35, 35, 40), -1);
        Cv2.Circle(mat, new Point(cx, cy), 35, new Scalar(100, 105, 110), 2);

        // Corner drillings
        Cv2.Circle(mat, new Point(rx + 25, ry + 25), 10, new Scalar(40, 40, 45), -1);
        Cv2.Circle(mat, new Point(rx + (int)partW - 25, ry + 25), 10, new Scalar(40, 40, 45), -1);
        Cv2.Circle(mat, new Point(rx + 25, ry + (int)partH - 25), 10, new Scalar(40, 40, 45), -1);
        Cv2.Circle(mat, new Point(rx + (int)partW - 25, ry + (int)partH - 25), 10, new Scalar(40, 40, 45), -1);

        // Overlay GigE GenICam telemetry
        Cv2.PutText(mat, "GENICAM GIGE VISION STREAM", new Point(20, 30), HersheyFonts.HersheySimplex, 0.5, new Scalar(0, 220, 255), 1);
        Cv2.PutText(mat, $"Dev: VS-GigE-5000M | IP: 192.168.1.100 | Frame #{frameIndex}", new Point(20, 50), HersheyFonts.HersheySimplex, 0.4, new Scalar(180, 180, 190), 1);

        return mat;
    }
}
