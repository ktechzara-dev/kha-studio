using OpenCvSharp;
using VisionStudio.Matching.Models;

namespace VisionStudio.Matching.Training;

/// <summary>
/// Options for learning a geometric contour model from training imagery.
/// </summary>
public sealed class GeometricModelTrainerOptions
{
    public float MinContrast { get; set; } = 25.0f;
    public float HighContrast { get; set; } = 60.0f;
    public int MaxPoints { get; set; } = 1200;
    public int MinPoints { get; set; } = 30;
    public int PyramidLevels { get; set; } = 3;
    public Point2f? CustomOrigin { get; set; } = null;
    public float PointReductionFactor { get; set; } = 0.5f;
}

/// <summary>
/// Learns geometric models from edge/gradient geometry rather than pixel brightness.
/// </summary>
public static class GeometricModelTrainer
{
    /// <summary>
    /// Trains a multi-scale geometric model from an image ROI and optional mask.
    /// </summary>
    public static GeometricModel Train(
        Mat image,
        Rect trainRegion,
        Mat? mask = null,
        GeometricModelTrainerOptions? options = null,
        string modelName = "GeometricModel")
    {
        if (image == null || image.IsDisposed || image.Empty())
            throw new ArgumentException("Input image must be a valid non-empty Mat.", nameof(image));

        options ??= new GeometricModelTrainerOptions();

        // Clamp ROI to image boundaries
        var clampedRoi = new Rect(
            Math.Max(0, trainRegion.X),
            Math.Max(0, trainRegion.Y),
            Math.Min(trainRegion.Width, image.Width - Math.Max(0, trainRegion.X)),
            Math.Min(trainRegion.Height, image.Height - Math.Max(0, trainRegion.Y)));

        if (clampedRoi.Width < 8 || clampedRoi.Height < 8)
            throw new ArgumentException($"Training region too small ({clampedRoi.Width}x{clampedRoi.Height}). Minimum is 8x8.", nameof(trainRegion));

        using var patch = new Mat(image, clampedRoi);
        using var grayPatch = new Mat();
        if (patch.Channels() > 1)
        {
            Cv2.CvtColor(patch, grayPatch, ColorConversionCodes.BGR2GRAY);
        }
        else
        {
            patch.CopyTo(grayPatch);
        }

        Mat? patchMask = null;
        if (mask != null && !mask.IsDisposed && !mask.Empty())
        {
            var clampedMaskRoi = new Rect(
                Math.Min(mask.Width - 1, Math.Max(0, clampedRoi.X)),
                Math.Min(mask.Height - 1, Math.Max(0, clampedRoi.Y)),
                Math.Min(clampedRoi.Width, mask.Width - clampedRoi.X),
                Math.Min(clampedRoi.Height, mask.Height - clampedRoi.Y));

            if (clampedMaskRoi.Width > 0 && clampedMaskRoi.Height > 0)
            {
                patchMask = new Mat(mask, clampedMaskRoi);
            }
        }

        try
        {
            var origin = options.CustomOrigin ?? new Point2f(clampedRoi.Width / 2.0f, clampedRoi.Height / 2.0f);

            var model = new GeometricModel
            {
                Name = modelName,
                OriginX = origin.X,
                OriginY = origin.Y,
                Width = clampedRoi.Width,
                Height = clampedRoi.Height
            };

            // Build base level (Level 0)
            var basePoints = ExtractPointsFromPatch(grayPatch, patchMask, origin, options.MinContrast, options.HighContrast, options.MaxPoints);
            if (basePoints.Count < options.MinPoints)
            {
                throw new InvalidOperationException($"Extracted only {basePoints.Count} feature points, which is less than required minimum of {options.MinPoints}. Consider lowering MinContrast.");
            }

            model.PyramidLevels.Add(new GeometricPyramidLevel(0, 1.0f, basePoints));

            // Build pyramid levels
            var currentMat = grayPatch.Clone();
            var currentMask = patchMask?.Clone();
            float currentScale = 1.0f;

            for (int lvl = 1; lvl < options.PyramidLevels; lvl++)
            {
                currentScale *= 0.5f;
                using var downMat = new Mat();
                Cv2.PyrDown(currentMat, downMat);

                Mat? downMask = null;
                if (currentMask != null)
                {
                    downMask = new Mat();
                    Cv2.PyrDown(currentMask, downMask);
                }

                var scaledOrigin = new Point2f(origin.X * currentScale, origin.Y * currentScale);
                int lvlMaxPoints = Math.Max(options.MinPoints, (int)(options.MaxPoints * MathF.Pow(options.PointReductionFactor, lvl)));

                var lvlPoints = ExtractPointsFromPatch(downMat, downMask, scaledOrigin, options.MinContrast * 0.8f, options.HighContrast * 0.8f, lvlMaxPoints);

                model.PyramidLevels.Add(new GeometricPyramidLevel(lvl, currentScale, lvlPoints));

                currentMat.Dispose();
                currentMat = downMat.Clone();
                if (currentMask != null)
                {
                    currentMask.Dispose();
                    currentMask = downMask?.Clone();
                }
                downMask?.Dispose();
            }

            currentMat.Dispose();
            currentMask?.Dispose();

            return model;
        }
        finally
        {
            patchMask?.Dispose();
        }
    }

    private static List<GeometricPoint> ExtractPointsFromPatch(
        Mat gray,
        Mat? mask,
        Point2f origin,
        float minContrast,
        float highContrast,
        int maxPoints)
    {
        // 1. Compute Sobel gradients in float32
        using var gx = new Mat();
        using var gy = new Mat();
        Cv2.Sobel(gray, gx, MatType.CV_32F, 1, 0, 3);
        Cv2.Sobel(gray, gy, MatType.CV_32F, 0, 1, 3);

        // 2. Compute Canny edge mask to lock onto thin contour ridges
        using var cannyEdges = new Mat();
        Cv2.Canny(gray, cannyEdges, minContrast, highContrast);

        var points = new List<GeometricPoint>();
        int rows = gray.Rows;
        int cols = gray.Cols;

        unsafe
        {
            byte* pCanny = cannyEdges.DataPointer;
            byte* pMask = mask != null ? mask.DataPointer : null;
            float* pGx = (float*)gx.DataPointer;
            float* pGy = (float*)gy.DataPointer;
            int stepCanny = (int)cannyEdges.Step();
            int stepMask = mask != null ? (int)mask.Step() : 0;
            int stepGx = (int)(gx.Step() / sizeof(float));
            int stepGy = (int)(gy.Step() / sizeof(float));

            for (int y = 1; y < rows - 1; y++)
            {
                byte* rowCanny = pCanny + y * stepCanny;
                byte* rowMask = pMask != null ? pMask + y * stepMask : null;
                float* rowGx = pGx + y * stepGx;
                float* rowGy = pGy + y * stepGy;

                for (int x = 1; x < cols - 1; x++)
                {
                    if (rowCanny[x] == 0) continue;
                    if (rowMask != null && rowMask[x] == 0) continue;

                    float vx = rowGx[x];
                    float vy = rowGy[x];
                    float mag = MathF.Sqrt(vx * vx + vy * vy);

                    if (mag >= minContrast)
                    {
                        float invMag = 1.0f / mag;
                        float dirX = vx * invMag;
                        float dirY = vy * invMag;

                        points.Add(new GeometricPoint(
                            X: x - origin.X,
                            Y: y - origin.Y,
                            DirectionX: dirX,
                            DirectionY: dirY,
                            Magnitude: mag,
                            Weight: 1.0f));
                    }
                }
            }
        }

        // Subsample if too dense
        if (points.Count > maxPoints)
        {
            points.Sort((a, b) => b.Magnitude.CompareTo(a.Magnitude));
            float step = (float)points.Count / maxPoints;
            var sampled = new List<GeometricPoint>(maxPoints);
            for (int i = 0; i < maxPoints; i++)
            {
                int idx = Math.Min(points.Count - 1, (int)(i * step));
                sampled.Add(points[idx]);
            }
            return sampled;
        }

        return points;
    }
}
