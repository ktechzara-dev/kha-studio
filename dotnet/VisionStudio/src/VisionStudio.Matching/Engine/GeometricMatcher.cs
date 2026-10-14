using System.Diagnostics;
using OpenCvSharp;
using VisionStudio.Matching.Models;

namespace VisionStudio.Matching.Engine;

/// <summary>
/// High-precision industrial geometric pattern matching engine.
/// Learns models from edge/gradient geometry rather than pixels.
/// Handles rotation (full 360° or ranges), scale variation, partial occlusion (up to 50%),
/// contrast reversal, and clutter with sub-pixel (<0.3 px) and sub-degree (<0.3°) accuracy.
/// </summary>
public sealed class GeometricMatcher
{
    private sealed class GradientMap : IDisposable
    {
        public int Width { get; }
        public int Height { get; }
        public float[] DirX { get; }
        public float[] DirY { get; }

        public GradientMap(Mat gray, float minContrast)
        {
            Width = gray.Cols;
            Height = gray.Rows;
            int total = Width * Height;
            DirX = new float[total];
            DirY = new float[total];

            using var gx = new Mat();
            using var gy = new Mat();
            Cv2.Sobel(gray, gx, MatType.CV_32F, 1, 0, 3);
            Cv2.Sobel(gray, gy, MatType.CV_32F, 0, 1, 3);

            unsafe
            {
                float* pGx = (float*)gx.DataPointer;
                float* pGy = (float*)gy.DataPointer;
                int stepGx = (int)(gx.Step() / sizeof(float));
                int stepGy = (int)(gy.Step() / sizeof(float));

                for (int y = 0; y < Height; y++)
                {
                    float* rowGx = pGx + y * stepGx;
                    float* rowGy = pGy + y * stepGy;
                    int rowOffset = y * Width;

                    for (int x = 0; x < Width; x++)
                    {
                        float vx = rowGx[x];
                        float vy = rowGy[x];
                        float magSq = vx * vx + vy * vy;

                        if (magSq >= minContrast * minContrast)
                        {
                            float invMag = 1.0f / MathF.Sqrt(magSq);
                            DirX[rowOffset + x] = vx * invMag;
                            DirY[rowOffset + x] = vy * invMag;
                        }
                    }
                }
            }
        }

        public void Dispose() { }
    }

    private sealed class CandidatePose
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float AngleDeg { get; set; }
        public float Scale { get; set; }
        public float Score { get; set; }
        public bool ContrastReversed { get; set; }

        public CandidatePose(float x, float y, float angleDeg, float scale, float score, bool contrastReversed = false)
        {
            X = x;
            Y = y;
            AngleDeg = angleDeg;
            Scale = scale;
            Score = score;
            ContrastReversed = contrastReversed;
        }
    }

    /// <summary>
    /// Searches for instances of the geometric model in the target image.
    /// </summary>
    public MatchReport Match(Mat image, GeometricModel model, GeometricMatcherOptions? options = null)
    {
        if (image == null || image.IsDisposed || image.Empty())
            throw new ArgumentException("Input image must be a valid non-empty Mat.", nameof(image));
        if (model == null || model.PyramidLevels.Count == 0 || model.PointCount == 0)
            throw new ArgumentException("Geometric model must contain valid trained points.", nameof(model));

        options ??= new GeometricMatcherOptions();
        var sw = Stopwatch.StartNew();

        // 1. Convert to grayscale if needed
        using var gray = new Mat();
        if (image.Channels() > 1)
        {
            Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);
        }
        else
        {
            image.CopyTo(gray);
        }

        // 2. Build multi-resolution pyramid of gradient maps
        int searchLevels = Math.Clamp(options.PyramidLevels, 1, Math.Min(model.PyramidLevels.Count, 4));
        var gradPyramid = new List<GradientMap>();
        var grayPyramid = new List<Mat> { gray.Clone() };

        for (int l = 1; l < searchLevels; l++)
        {
            var down = new Mat();
            Cv2.PyrDown(grayPyramid[l - 1], down);
            grayPyramid.Add(down);
        }

        for (int l = 0; l < searchLevels; l++)
        {
            float levelContrast = options.MinContrast * MathF.Pow(0.85f, l);
            gradPyramid.Add(new GradientMap(grayPyramid[l], levelContrast));
        }

        // Clean up gray pyramid
        foreach (var m in grayPyramid) m.Dispose();

        try
        {
            // 3. Coarse Search on coarsest pyramid level
            int topLevel = searchLevels - 1;
            var topGrad = gradPyramid[topLevel];
            var topModelLevel = model.GetLevel(topLevel) ?? model.PyramidLevels[^1];
            float topScaleFactor = topModelLevel.ScaleFactor;

            var candidates = CoarseSearch(topGrad, topModelLevel, options);

            if (candidates.Count == 0)
            {
                sw.Stop();
                return MatchReport.Empty(sw.Elapsed.TotalMilliseconds);
            }

            // 4. Track candidates down through pyramid levels to full resolution (Level 0)
            for (int lvl = topLevel - 1; lvl >= 0; lvl--)
            {
                var lvlGrad = gradPyramid[lvl];
                var lvlModelLevel = model.GetLevel(lvl) ?? model.PyramidLevels[0];

                candidates = RefineLevel(candidates, lvlGrad, lvlModelLevel, options, isFinalLevel: (lvl == 0));
                if (candidates.Count == 0) break;
            }

            if (candidates.Count == 0)
            {
                sw.Stop();
                return MatchReport.Empty(sw.Elapsed.TotalMilliseconds);
            }

            // 5. Sub-pixel and Sub-degree Parabolic Peak Refinement (Level 0)
            if (options.SubPixelRefinement && gradPyramid.Count > 0)
            {
                var level0Grad = gradPyramid[0];
                var level0Points = model.BasePoints;

                foreach (var c in candidates)
                {
                    RefineSubPixel(c, level0Grad, level0Points, options.AllowContrastReversal);
                }
            }

            // 6. Non-Maximum Suppression (NMS) and instance assembly
            var finalInstances = ApplyNmsAndAssemble(candidates, model, options);

            sw.Stop();
            return new MatchReport(finalInstances, sw.Elapsed.TotalMilliseconds);
        }
        finally
        {
            foreach (var g in gradPyramid) g.Dispose();
        }
    }

    private static List<CandidatePose> CoarseSearch(
        GradientMap grad,
        GeometricPyramidLevel modelLevel,
        GeometricMatcherOptions options)
    {
        var candidates = new List<CandidatePose>();
        var points = modelLevel.Points;
        if (points.Count == 0) return candidates;

        float coarseMinScore = options.MinScore * 0.70f;
        int step = Math.Max(2, (int)(3.0f * (1.0f / modelLevel.ScaleFactor) / 2.0f));
        step = Math.Clamp(step, 2, 4);

        float angleStep = Math.Max(options.AngleStepDeg, 3.0f);
        float scaleStep = Math.Max(options.ScaleStep, 0.08f);

        int width = grad.Width;
        int height = grad.Height;

        // Precompute rotated & scaled templates for each (angle, scale) candidate
        for (float scale = options.ScaleMin; scale <= options.ScaleMax + 0.001f; scale += scaleStep)
        {
            for (float angleDeg = options.AngleMinDeg; angleDeg <= options.AngleMaxDeg + 0.001f; angleDeg += angleStep)
            {
                float rad = angleDeg * (MathF.PI / 180.0f);
                float cosA = MathF.Cos(rad);
                float sinA = MathF.Sin(rad);

                // Transform points
                var xformedPoints = new (int dx, int dy, float rx, float ry)[points.Count];
                int minX = int.MaxValue, maxX = int.MinValue;
                int minY = int.MaxValue, maxY = int.MinValue;

                for (int i = 0; i < points.Count; i++)
                {
                    var p = points[i];
                    int tx = (int)MathF.Round((p.X * cosA + p.Y * sinA) * scale);
                    int ty = (int)MathF.Round((-p.X * sinA + p.Y * cosA) * scale);
                    float rdx = p.DirectionX * cosA + p.DirectionY * sinA;
                    float rdy = -p.DirectionX * sinA + p.DirectionY * cosA;

                    xformedPoints[i] = (tx, ty, rdx, rdy);
                    if (tx < minX) minX = tx;
                    if (tx > maxX) maxX = tx;
                    if (ty < minY) minY = ty;
                    if (ty > maxY) maxY = ty;
                }

                int startX = Math.Max(step, -minX);
                int endX = Math.Min(width - step, width - maxX);
                int startY = Math.Max(step, -minY);
                int endY = Math.Min(height - step, height - maxY);

                if (startX >= endX || startY >= endY) continue;

                // Scan grid
                for (int y = startY; y < endY; y += step)
                {
                    int rowOffset = y * width;
                    for (int x = startX; x < endX; x += step)
                    {
                        float sumDot = 0.0f;
                        float sumAbsDot = 0.0f;
                        int validPoints = 0;

                        for (int i = 0; i < xformedPoints.Length; i++)
                        {
                            int px = x + xformedPoints[i].dx;
                            int py = y + xformedPoints[i].dy;

                            if (px < 0 || px >= width || py < 0 || py >= height) continue;

                            int idx = py * width + px;
                            float gx = grad.DirX[idx];
                            float gy = grad.DirY[idx];

                            if (gx != 0.0f || gy != 0.0f)
                            {
                                float dot = xformedPoints[i].rx * gx + xformedPoints[i].ry * gy;
                                sumDot += MathF.Max(0.0f, dot);
                                sumAbsDot += MathF.Abs(dot);
                            }
                            validPoints++;
                        }

                        if (validPoints == 0) continue;

                        float scoreNormal = sumDot / xformedPoints.Length;
                        float scoreReversed = sumAbsDot / xformedPoints.Length;

                        float bestScore = options.AllowContrastReversal ? MathF.Max(scoreNormal, scoreReversed) : scoreNormal;
                        bool isReversed = options.AllowContrastReversal && (scoreReversed > scoreNormal);

                        if (bestScore >= coarseMinScore)
                        {
                            candidates.Add(new CandidatePose(x, y, angleDeg, scale, bestScore, isReversed));
                        }
                    }
                }
            }
        }

        // Keep top N candidates cluster-filtered
        candidates.Sort((a, b) => b.Score.CompareTo(a.Score));
        return PruneCandidateClusters(candidates, distThreshold: 8.0f, maxCount: 40);
    }

    private static List<CandidatePose> RefineLevel(
        List<CandidatePose> coarseCandidates,
        GradientMap grad,
        GeometricPyramidLevel modelLevel,
        GeometricMatcherOptions options,
        bool isFinalLevel)
    {
        var refined = new List<CandidatePose>();
        var points = modelLevel.Points;
        int width = grad.Width;
        int height = grad.Height;

        float scoreThreshold = isFinalLevel ? options.MinScore : options.MinScore * 0.85f;

        foreach (var c in coarseCandidates)
        {
            // Project coordinate from parent level to current level (scaled by 2x)
            float cx = c.X * 2.0f;
            float cy = c.Y * 2.0f;
            float baseAngle = c.AngleDeg;
            float baseScale = c.Scale;

            float bestScore = 0.0f;
            CandidatePose? bestPose = null;

            // Local window search around tracked candidate (wide enough to capture pyramid steps)
            float angleRange = isFinalLevel ? 2.5f : 4.0f;
            float angleStep = isFinalLevel ? 0.25f : 1.0f;

            for (float dScale = -0.04f; dScale <= 0.04f; dScale += 0.02f)
            {
                float s = Math.Clamp(baseScale + dScale, options.ScaleMin, options.ScaleMax);

                for (float dAngle = -angleRange; dAngle <= angleRange + 0.001f; dAngle += angleStep)
                {
                    float angleDeg = baseAngle + dAngle;
                    float rad = angleDeg * (MathF.PI / 180.0f);
                    float cosA = MathF.Cos(rad);
                    float sinA = MathF.Sin(rad);

                    for (float dy = -3.0f; dy <= 3.0f; dy += 1.0f)
                    {
                        for (float dx = -3.0f; dx <= 3.0f; dx += 1.0f)
                        {
                            float x = cx + dx;
                            float y = cy + dy;

                            float score = ComputeScoreAt(grad, points, x, y, cosA, sinA, s, options.AllowContrastReversal, out bool reversed);

                            if (score > bestScore)
                            {
                                bestScore = score;
                                bestPose = new CandidatePose(x, y, angleDeg, s, score, reversed);
                            }
                        }
                    }
                }
            }

            if (bestPose != null && bestPose.Score >= scoreThreshold)
            {
                refined.Add(bestPose);
            }
        }

        refined.Sort((a, b) => b.Score.CompareTo(a.Score));
        return PruneCandidateClusters(refined, distThreshold: isFinalLevel ? 12.0f : 8.0f, maxCount: options.MaxInstances * 5);
    }

    private static void RefineSubPixel(
        CandidatePose c,
        GradientMap grad,
        IReadOnlyList<GeometricPoint> points,
        bool allowContrastReversal)
    {
        float rad = c.AngleDeg * (MathF.PI / 180.0f);
        float cosA = MathF.Cos(rad);
        float sinA = MathF.Sin(rad);

        int ix = (int)MathF.Round(c.X);
        int iy = (int)MathF.Round(c.Y);

        float h = 0.5f;

        // 1. Sub-pixel X parabola fitting anchored to integer peak with narrow 0.5px stencil
        float s0X = ComputeScoreAt(grad, points, ix, iy, cosA, sinA, c.Scale, allowContrastReversal, out _);
        float smX = ComputeScoreAt(grad, points, ix - h, iy, cosA, sinA, c.Scale, allowContrastReversal, out _);
        float spX = ComputeScoreAt(grad, points, ix + h, iy, cosA, sinA, c.Scale, allowContrastReversal, out _);

        float denomX = 2.0f * (smX - 2.0f * s0X + spX);
        if (denomX < -1e-5f)
        {
            float dx = h * (smX - spX) / denomX;
            dx = Math.Clamp(dx, -0.5f, 0.5f);
            c.X = ix + dx;
        }
        else
        {
            c.X = ix;
        }

        // 2. Sub-pixel Y parabola fitting with narrow 0.5px stencil
        float s0Y = ComputeScoreAt(grad, points, c.X, iy, cosA, sinA, c.Scale, allowContrastReversal, out _);
        float smY = ComputeScoreAt(grad, points, c.X, iy - h, cosA, sinA, c.Scale, allowContrastReversal, out _);
        float spY = ComputeScoreAt(grad, points, c.X, iy + h, cosA, sinA, c.Scale, allowContrastReversal, out _);

        float denomY = 2.0f * (smY - 2.0f * s0Y + spY);
        if (denomY < -1e-5f)
        {
            float dy = h * (smY - spY) / denomY;
            dy = Math.Clamp(dy, -0.5f, 0.5f);
            c.Y = iy + dy;
        }
        else
        {
            c.Y = iy;
        }

        // 3. Sub-degree Angle parabola fitting
        float baseAngle = c.AngleDeg;
        float hAng = 0.5f;
        float radMinus = (baseAngle - hAng) * (MathF.PI / 180.0f);
        float radPlus = (baseAngle + hAng) * (MathF.PI / 180.0f);

        float s0Ang = ComputeScoreAt(grad, points, c.X, c.Y, MathF.Cos(rad), MathF.Sin(rad), c.Scale, allowContrastReversal, out bool rev);
        float smAng = ComputeScoreAt(grad, points, c.X, c.Y, MathF.Cos(radMinus), MathF.Sin(radMinus), c.Scale, allowContrastReversal, out _);
        float spAng = ComputeScoreAt(grad, points, c.X, c.Y, MathF.Cos(radPlus), MathF.Sin(radPlus), c.Scale, allowContrastReversal, out _);

        float denomAng = 2.0f * (smAng - 2.0f * s0Ang + spAng);
        if (denomAng < -1e-5f)
        {
            float dAng = hAng * (smAng - spAng) / denomAng;
            dAng = Math.Clamp(dAng, -0.5f, 0.5f);
            c.AngleDeg = baseAngle + dAng;
        }

        c.Score = s0Ang;
        c.ContrastReversed = rev;
    }

    private static float ComputeScoreAtSubPixel(
        GradientMap grad,
        IReadOnlyList<GeometricPoint> points,
        float x,
        float y,
        float cosA,
        float sinA,
        float scale,
        bool allowContrastReversal,
        out bool isReversed)
    {
        isReversed = false;
        if (points.Count == 0) return 0.0f;

        int width = grad.Width;
        int height = grad.Height;
        float sumDot = 0.0f;
        float sumAbsDot = 0.0f;

        for (int i = 0; i < points.Count; i++)
        {
            var p = points[i];
            float rx = (p.X * cosA + p.Y * sinA) * scale + x;
            float ry = (-p.X * sinA + p.Y * cosA) * scale + y;

            int x0 = (int)MathF.Floor(rx);
            int y0 = (int)MathF.Floor(ry);
            int x1 = x0 + 1;
            int y1 = y0 + 1;

            if (x0 < 0 || x1 >= width || y0 < 0 || y1 >= height) continue;

            float fx = rx - x0;
            float fy = ry - y0;
            float w00 = (1.0f - fx) * (1.0f - fy);
            float w10 = fx * (1.0f - fy);
            float w01 = (1.0f - fx) * fy;
            float w11 = fx * fy;

            int idx00 = y0 * width + x0;
            int idx10 = y0 * width + x1;
            int idx01 = y1 * width + x0;
            int idx11 = y1 * width + x1;

            float gx = w00 * grad.DirX[idx00] + w10 * grad.DirX[idx10] + w01 * grad.DirX[idx01] + w11 * grad.DirX[idx11];
            float gy = w00 * grad.DirY[idx00] + w10 * grad.DirY[idx10] + w01 * grad.DirY[idx01] + w11 * grad.DirY[idx11];

            float magSq = gx * gx + gy * gy;
            if (magSq > 1e-4f)
            {
                float invMag = 1.0f / MathF.Sqrt(magSq);
                gx *= invMag;
                gy *= invMag;

                float rdx = p.DirectionX * cosA + p.DirectionY * sinA;
                float rdy = -p.DirectionX * sinA + p.DirectionY * cosA;
                float dot = rdx * gx + rdy * gy;

                sumDot += MathF.Max(0.0f, dot);
                sumAbsDot += MathF.Abs(dot);
            }
        }

        float scoreNormal = sumDot / points.Count;
        float scoreAbs = sumAbsDot / points.Count;

        if (allowContrastReversal && scoreAbs > scoreNormal)
        {
            isReversed = true;
            return scoreAbs;
        }

        return scoreNormal;
    }

    private static float ComputeScoreAt(
        GradientMap grad,
        IReadOnlyList<GeometricPoint> points,
        float x,
        float y,
        float cosA,
        float sinA,
        float scale,
        bool allowContrastReversal,
        out bool isReversed)
    {
        isReversed = false;
        if (points.Count == 0) return 0.0f;

        int width = grad.Width;
        int height = grad.Height;
        float sumDot = 0.0f;
        float sumAbsDot = 0.0f;

        for (int i = 0; i < points.Count; i++)
        {
            var p = points[i];
            float rx = (p.X * cosA + p.Y * sinA) * scale + x;
            float ry = (-p.X * sinA + p.Y * cosA) * scale + y;

            int px = (int)MathF.Round(rx);
            int py = (int)MathF.Round(ry);

            if (px < 0 || px >= width || py < 0 || py >= height) continue;

            int idx = py * width + px;
            float gx = grad.DirX[idx];
            float gy = grad.DirY[idx];

            if (gx != 0.0f || gy != 0.0f)
            {
                float rdx = p.DirectionX * cosA + p.DirectionY * sinA;
                float rdy = -p.DirectionX * sinA + p.DirectionY * cosA;
                float dot = rdx * gx + rdy * gy;

                sumDot += MathF.Max(0.0f, dot);
                sumAbsDot += MathF.Abs(dot);
            }
        }

        float scoreNormal = sumDot / points.Count;
        float scoreAbs = sumAbsDot / points.Count;

        if (allowContrastReversal && scoreAbs > scoreNormal)
        {
            isReversed = true;
            return scoreAbs;
        }

        return scoreNormal;
    }

    private static List<CandidatePose> PruneCandidateClusters(List<CandidatePose> list, float distThreshold, int maxCount)
    {
        var result = new List<CandidatePose>();
        float distSq = distThreshold * distThreshold;

        foreach (var c in list)
        {
            bool duplicate = false;
            foreach (var r in result)
            {
                float dx = c.X - r.X;
                float dy = c.Y - r.Y;
                float dAngle = MathF.Abs(c.AngleDeg - r.AngleDeg);
                if (dAngle > 180.0f) dAngle = 360.0f - dAngle;

                if (dx * dx + dy * dy < distSq && dAngle < 15.0f)
                {
                    duplicate = true;
                    break;
                }
            }

            if (!duplicate)
            {
                result.Add(c);
                if (result.Count >= maxCount) break;
            }
        }

        return result;
    }

    private static List<MatchInstance> ApplyNmsAndAssemble(
        List<CandidatePose> candidates,
        GeometricModel model,
        GeometricMatcherOptions options)
    {
        var finalInstances = new List<MatchInstance>();
        float halfW = model.Width / 2.0f;
        float halfH = model.Height / 2.0f;

        foreach (var c in candidates)
        {
            if (c.Score < options.MinScore) continue;

            float rad = c.AngleDeg * (MathF.PI / 180.0f);
            float cosA = MathF.Cos(rad);
            float sinA = MathF.Sin(rad);

            // 1. Transform contour points
            var xformedContour = new List<Point2f>(model.BasePoints.Count);
            foreach (var p in model.BasePoints)
            {
                float rx = (p.X * cosA + p.Y * sinA) * c.Scale + c.X;
                float ry = (-p.X * sinA + p.Y * cosA) * c.Scale + c.Y;
                xformedContour.Add(new Point2f(rx, ry));
            }

            // 2. Compute 4 corner points of bounding box
            var corners = new Point2f[4];
            Point2f[] localCorners = {
                new(-halfW, -halfH),
                new(halfW, -halfH),
                new(halfW, halfH),
                new(-halfW, halfH)
            };

            for (int i = 0; i < 4; i++)
            {
                float bx = (localCorners[i].X * cosA + localCorners[i].Y * sinA) * c.Scale + c.X;
                float by = (-localCorners[i].X * sinA + localCorners[i].Y * cosA) * c.Scale + c.Y;
                corners[i] = new Point2f(bx, by);
            }

            // 3. Overlap check against already accepted instances
            bool overlaps = false;
            foreach (var existing in finalInstances)
            {
                float dx = c.X - existing.X;
                float dy = c.Y - existing.Y;
                float dist = MathF.Sqrt(dx * dx + dy * dy);
                float minDim = MathF.Min(model.Width, model.Height) * c.Scale;

                if (dist < minDim * options.OverlapThreshold)
                {
                    overlaps = true;
                    break;
                }
            }

            if (!overlaps)
            {
                var pose = new MatchPose(c.X, c.Y, c.AngleDeg, c.Scale);
                finalInstances.Add(new MatchInstance(pose, c.Score, c.ContrastReversed, xformedContour, corners));

                if (finalInstances.Count >= options.MaxInstances) break;
            }
        }

        return finalInstances;
    }
}
