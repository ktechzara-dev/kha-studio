using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Devices.Abstractions;

namespace VisionStudio.Devices.Cameras;

/// <summary>
/// Synthetic test pattern generator camera driver.
/// Generates realistic industrial images on-the-fly without requiring physical camera hardware.
/// Includes support for industrial defect injection (missing holes, scratches, burrs, translation/rotation).
/// </summary>
public sealed class SyntheticPatternCamera : BaseCamera
{
    private long _frameIndex;
    private readonly Random _random = new(42);

    public const string FeaturePattern = "Pattern";
    public const string FeatureDefectEnabled = "DefectInjection";
    public const string FeatureDefectType = "DefectType";
    public const string FeatureCheckerRows = "CheckerRows";
    public const string FeatureCheckerCols = "CheckerCols";

    public SyntheticPatternCamera(string id = "SIM_SYNTHETIC", string name = "Synthetic Pattern Generator Camera")
        : base(id, name)
    {
        Parameters.Width = 640;
        Parameters.Height = 480;
        Parameters.FrameRate = 30.0;
        Parameters.ExposureTime = 15000.0;
        Parameters.Gain = 0.0;
        Parameters.PixelFormat = PixelFormat.Bgr24;

        Parameters.CustomFeatures[FeaturePattern] = "TargetPart";
        Parameters.CustomFeatures[FeatureDefectEnabled] = false;
        Parameters.CustomFeatures[FeatureDefectType] = "None";
        Parameters.CustomFeatures[FeatureCheckerRows] = 7;
        Parameters.CustomFeatures[FeatureCheckerCols] = 9;
    }

    protected override void OnConnect(CancellationToken ct)
    {
        _frameIndex = 0;
    }

    protected override void OnDisconnect()
    {
    }

    protected override Task<ImageFrame> AcquireSingleFrameAsync(CancellationToken ct)
    {
        var width = Math.Max(64, Parameters.Width);
        var height = Math.Max(64, Parameters.Height);
        var frameId = Interlocked.Increment(ref _frameIndex);

        var pattern = Parameters.CustomFeatures.TryGetValue(FeaturePattern, out var pObj)
            ? pObj?.ToString() ?? "TargetPart"
            : "TargetPart";

        var defectEnabled = Parameters.CustomFeatures.TryGetValue(FeatureDefectEnabled, out var dObj)
            && Convert.ToBoolean(dObj);

        var defectType = Parameters.CustomFeatures.TryGetValue(FeatureDefectType, out var dtObj)
            ? dtObj?.ToString() ?? "None"
            : "None";

        Mat rawMat = pattern.ToLowerInvariant() switch
        {
            "checkerboard" => GenerateCheckerboard(width, height),
            "movingparts" => GenerateMovingParts(width, height, frameId),
            "concentriccircles" => GenerateConcentricCircles(width, height),
            "colorbars" => GenerateColorBars(width, height),
            _ => GenerateTargetPart(width, height, defectEnabled, defectType, frameId)
        };

        // Apply simulated exposure (brightness scaling)
        ApplyExposureSimulation(rawMat, Parameters.ExposureTime);

        // Apply simulated sensor gain noise
        if (Parameters.Gain > 0.1)
        {
            ApplyGainNoise(rawMat, Parameters.Gain);
        }

        // Convert to desired pixel format if Mono8 requested
        if (Parameters.PixelFormat == PixelFormat.Mono8 && rawMat.Channels() == 3)
        {
            var monoMat = new Mat();
            Cv2.CvtColor(rawMat, monoMat, ColorConversionCodes.BGR2GRAY);
            rawMat.Dispose();
            rawMat = monoMat;
        }

        var frame = new ImageFrame(rawMat, cameraId: Id, frameId: frameId);
        return Task.FromResult(frame);
    }

    private Mat GenerateTargetPart(int width, int height, bool defectEnabled, string defectType, long frameIndex)
    {
        // Dark factory background
        var mat = new Mat(height, width, MatType.CV_8UC3, new Scalar(28, 30, 36));

        // Part center and dimensions
        var cx = width / 2.0;
        var cy = height / 2.0;
        var partW = Math.Min(width * 0.7, 360.0);
        var partH = Math.Min(height * 0.65, 240.0);

        // Sub-pixel jitter / vibration simulation
        var jitterX = (Math.Sin(frameIndex * 0.2) * 1.5);
        var jitterY = (Math.Cos(frameIndex * 0.2) * 1.5);
        cx += jitterX;
        cy += jitterY;

        var partRect = new Rect2d(cx - partW / 2.0, cy - partH / 2.0, partW, partH);

        // Draw machined metal bracket body
        var bodyColor = new Scalar(175, 180, 190);
        var borderColor = new Scalar(220, 225, 230);

        Cv2.Rectangle(mat, new Rect((int)partRect.X, (int)partRect.Y, (int)partRect.Width, (int)partRect.Height), bodyColor, -1);
        Cv2.Rectangle(mat, new Rect((int)partRect.X, (int)partRect.Y, (int)partRect.Width, (int)partRect.Height), borderColor, 2);

        // Center borehole (radius ~ 35px)
        var centerRadius = (int)Math.Min(partH * 0.25, 38);
        Cv2.Circle(mat, (int)cx, (int)cy, centerRadius, new Scalar(45, 45, 50), -1);
        Cv2.Circle(mat, (int)cx, (int)cy, centerRadius, new Scalar(90, 90, 100), 2);

        // 4 Corner mounting holes
        var holeRadius = (int)Math.Min(partH * 0.12, 14);
        var offsetHoleX = partW * 0.36;
        var offsetHoleY = partH * 0.32;

        var holes = new (double x, double y)[]
        {
            (cx - offsetHoleX, cy - offsetHoleY), // Top-Left
            (cx + offsetHoleX, cy - offsetHoleY), // Top-Right
            (cx - offsetHoleX, cy + offsetHoleY), // Bottom-Left
            (cx + offsetHoleX, cy + offsetHoleY)  // Bottom-Right
        };

        var skipTopRightHole = defectEnabled && (defectType.Equals("MissingHole", StringComparison.OrdinalIgnoreCase) ||
                                                 defectType.Equals("Random", StringComparison.OrdinalIgnoreCase) && (frameIndex % 3 == 0));

        for (int i = 0; i < holes.Length; i++)
        {
            if (i == 1 && skipTopRightHole)
            {
                // Defect: Missing drilled hole!
                continue;
            }

            Cv2.Circle(mat, (int)holes[i].x, (int)holes[i].y, holeRadius, new Scalar(35, 35, 40), -1);
            Cv2.Circle(mat, (int)holes[i].x, (int)holes[i].y, holeRadius, new Scalar(80, 80, 90), 1);
        }

        // Defect: Surface Scratch
        var injectScratch = defectEnabled && (defectType.Equals("Scratch", StringComparison.OrdinalIgnoreCase) ||
                                              defectType.Equals("Random", StringComparison.OrdinalIgnoreCase) && (frameIndex % 3 == 1));
        if (injectScratch)
        {
            var p1 = new Point((int)(cx - 30), (int)(cy - 40));
            var p2 = new Point((int)(cx + 45), (int)(cy + 50));
            Cv2.Line(mat, p1, p2, new Scalar(40, 40, 45), 2);
            Cv2.Line(mat, new Point(p1.X + 1, p1.Y + 1), new Point(p2.X + 1, p2.Y + 1), new Scalar(240, 240, 245), 1);
        }

        // Defect: Edge Burr
        var injectBurr = defectEnabled && (defectType.Equals("Burr", StringComparison.OrdinalIgnoreCase) ||
                                           defectType.Equals("Random", StringComparison.OrdinalIgnoreCase) && (frameIndex % 3 == 2));
        if (injectBurr)
        {
            var burrX = (int)(cx + partW / 2.0);
            var burrY = (int)(cy - 20);
            Cv2.Ellipse(mat, new Point(burrX, burrY), new Size(12, 8), 0, 0, 360, new Scalar(175, 180, 190), -1);
            Cv2.Ellipse(mat, new Point(burrX, burrY), new Size(12, 8), 0, 0, 360, new Scalar(220, 225, 230), 1);
        }

        return mat;
    }

    private Mat GenerateCheckerboard(int width, int height)
    {
        var rows = Parameters.CustomFeatures.TryGetValue(FeatureCheckerRows, out var rObj) ? Convert.ToInt32(rObj) : 7;
        var cols = Parameters.CustomFeatures.TryGetValue(FeatureCheckerCols, out var cObj) ? Convert.ToInt32(cObj) : 9;

        var mat = new Mat(height, width, MatType.CV_8UC3, new Scalar(255, 255, 255));
        var squareSize = Math.Min(width / (cols + 2), height / (rows + 2));
        var startX = (width - cols * squareSize) / 2;
        var startY = (height - rows * squareSize) / 2;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                if ((r + c) % 2 == 1)
                {
                    var rect = new Rect(startX + c * squareSize, startY + r * squareSize, squareSize, squareSize);
                    Cv2.Rectangle(mat, rect, new Scalar(15, 15, 15), -1);
                }
            }
        }

        return mat;
    }

    private static Mat GenerateMovingParts(int width, int height, long frameIndex)
    {
        // Conveyor belt grey background
        var mat = new Mat(height, width, MatType.CV_8UC3, new Scalar(45, 48, 52));

        // Draw conveyor lane guides
        Cv2.Line(mat, new Point(0, height / 4), new Point(width, height / 4), new Scalar(70, 75, 80), 2);
        Cv2.Line(mat, new Point(0, (height * 3) / 4), new Point(width, (height * 3) / 4), new Scalar(70, 75, 80), 2);

        // 3 Moving parts traveling rightward across conveyor
        var speed = 6;
        var partSpacing = width * 0.45;
        var baseY = height / 2;

        for (int i = 0; i < 4; i++)
        {
            var xPos = (int)(((frameIndex * speed) + (i * partSpacing)) % (width + 120)) - 60;
            if (xPos > -60 && xPos < width + 60)
            {
                Cv2.Circle(mat, new Point(xPos, baseY), 32, new Scalar(180, 190, 205), -1);
                Cv2.Circle(mat, new Point(xPos, baseY), 32, new Scalar(230, 235, 240), 2);
                Cv2.Circle(mat, new Point(xPos, baseY), 10, new Scalar(25, 25, 30), -1);
            }
        }

        return mat;
    }

    private static Mat GenerateConcentricCircles(int width, int height)
    {
        var mat = new Mat(height, width, MatType.CV_8UC3, new Scalar(20, 20, 25));
        var cx = width / 2;
        var cy = height / 2;
        var maxR = Math.Min(cx, cy) - 20;

        for (int r = 20; r <= maxR; r += 25)
        {
            Cv2.Circle(mat, new Point(cx, cy), r, new Scalar(220, 220, 230), 2);
        }

        // Crosshairs
        Cv2.Line(mat, new Point(cx, 10), new Point(cx, height - 10), new Scalar(0, 128, 255), 1);
        Cv2.Line(mat, new Point(10, cy), new Point(width - 10, cy), new Scalar(0, 128, 255), 1);

        return mat;
    }

    private static Mat GenerateColorBars(int width, int height)
    {
        var mat = new Mat(height, width, MatType.CV_8UC3, Scalar.Black);
        var colors = new[]
        {
            new Scalar(255, 255, 255), // White
            new Scalar(0, 255, 255),   // Yellow
            new Scalar(255, 255, 0),   // Cyan
            new Scalar(0, 255, 0),     // Green
            new Scalar(255, 0, 255),   // Magenta
            new Scalar(0, 0, 255),     // Red
            new Scalar(255, 0, 0)      // Blue
        };

        var barW = width / colors.Length;
        for (int i = 0; i < colors.Length; i++)
        {
            var rect = new Rect(i * barW, 0, (i == colors.Length - 1) ? (width - i * barW) : barW, height);
            Cv2.Rectangle(mat, rect, colors[i], -1);
        }

        return mat;
    }

    private static void ApplyExposureSimulation(Mat mat, double exposureTimeUs)
    {
        // Nominal exposure is 15000 us (ratio = 1.0)
        var ratio = Math.Clamp(exposureTimeUs / 15000.0, 0.2, 2.5);
        if (Math.Abs(ratio - 1.0) > 0.05)
        {
            mat.ConvertTo(mat, -1, alpha: ratio, beta: 0);
        }
    }

    private void ApplyGainNoise(Mat mat, double gainDb)
    {
        // Gain creates Gaussian sensor noise proportional to gain level
        var noiseStdDev = Math.Clamp(gainDb * 1.5, 2.0, 40.0);
        using var noise = new Mat(mat.Size(), mat.Type());
        Cv2.Randn(noise, Scalar.All(0), Scalar.All(noiseStdDev));
        Cv2.Add(mat, noise, mat);
    }
}
