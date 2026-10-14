using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using VisionStudio.Core.Models;

namespace VisionStudio.DeepLearning.Inference;

/// <summary>
/// High-performance tensor conversion utilities for preprocessing images into ONNX inputs
/// and post-processing model predictions into heatmaps and masks.
/// </summary>
public static class TensorUtils
{
    /// <summary>
    /// Converts an OpenCV Mat into an NCHW DenseTensor[1, C, H, W] with normalization.
    /// </summary>
    public static DenseTensor<float> CreateInputTensor(
        Mat mat,
        int targetWidth,
        int targetHeight,
        bool isBgr = false,
        float[]? mean = null,
        float[]? std = null)
    {
        if (mat == null || mat.Empty())
        {
            throw new ArgumentException("Input mat is null or empty.", nameof(mat));
        }

        mean ??= new[] { 0.485f, 0.456f, 0.406f };
        std ??= new[] { 0.229f, 0.224f, 0.225f };

        // 1. Convert to 3-channel if grayscale
        using var bgrMat = new Mat();
        if (mat.Channels() == 1)
        {
            Cv2.CvtColor(mat, bgrMat, ColorConversionCodes.GRAY2BGR);
        }
        else if (mat.Channels() == 4)
        {
            Cv2.CvtColor(mat, bgrMat, ColorConversionCodes.BGRA2BGR);
        }
        else
        {
            mat.CopyTo(bgrMat);
        }

        // 2. Resize to target network input dimensions
        using var resized = new Mat();
        if (bgrMat.Width != targetWidth || bgrMat.Height != targetHeight)
        {
            Cv2.Resize(bgrMat, resized, new Size(targetWidth, targetHeight), interpolation: InterpolationFlags.Linear);
        }
        else
        {
            bgrMat.CopyTo(resized);
        }

        // 3. Convert color space (BGR -> RGB if model expects RGB)
        using var rgbMat = new Mat();
        if (!isBgr)
        {
            Cv2.CvtColor(resized, rgbMat, ColorConversionCodes.BGR2RGB);
        }
        else
        {
            resized.CopyTo(rgbMat);
        }

        // 4. Populate NCHW dense tensor
        var tensor = new DenseTensor<float>(new[] { 1, 3, targetHeight, targetWidth });
        int h = targetHeight;
        int w = targetWidth;

        unsafe
        {
            byte* ptr = rgbMat.DataPointer;
            int step = (int)rgbMat.Step();

            for (int y = 0; y < h; y++)
            {
                byte* row = ptr + (y * step);
                for (int x = 0; x < w; x++)
                {
                    int pxIdx = x * 3;
                    float c0 = (row[pxIdx + 0] / 255.0f - mean[0]) / std[0];
                    float c1 = (row[pxIdx + 1] / 255.0f - mean[1]) / std[1];
                    float c2 = (row[pxIdx + 2] / 255.0f - mean[2]) / std[2];

                    tensor[0, 0, y, x] = c0;
                    tensor[0, 1, y, x] = c1;
                    tensor[0, 2, y, x] = c2;
                }
            }
        }

        return tensor;
    }

    /// <summary>
    /// Creates a false-color Jet heatmap from a 2D float anomaly confidence matrix [H, W].
    /// Values should be normalized in [0, 1].
    /// </summary>
    public static Mat CreateHeatmapOverlay(float[,] anomalyMap, int origWidth, int origHeight)
    {
        int h = anomalyMap.GetLength(0);
        int w = anomalyMap.GetLength(1);

        using var grayMap = new Mat(h, w, MatType.CV_8UC1);
        unsafe
        {
            byte* ptr = grayMap.DataPointer;
            int step = (int)grayMap.Step();
            for (int y = 0; y < h; y++)
            {
                byte* row = ptr + (y * step);
                for (int x = 0; x < w; x++)
                {
                    float val = Math.Clamp(anomalyMap[y, x], 0.0f, 1.0f);
                    row[x] = (byte)(val * 255.0f);
                }
            }
        }

        // Upscale to original image size
        using var resizedGray = new Mat();
        Cv2.Resize(grayMap, resizedGray, new Size(origWidth, origHeight), interpolation: InterpolationFlags.Linear);

        var colorHeatmap = new Mat();
        Cv2.ApplyColorMap(resizedGray, colorHeatmap, ColormapTypes.Jet);
        return colorHeatmap;
    }

    /// <summary>
    /// Softmax activation function over 1D logits.
    /// </summary>
    public static float[] Softmax(ReadOnlySpan<float> logits)
    {
        float max = float.NegativeInfinity;
        for (int i = 0; i < logits.Length; i++)
        {
            if (logits[i] > max) max = logits[i];
        }

        float sum = 0.0f;
        var exps = new float[logits.Length];
        for (int i = 0; i < logits.Length; i++)
        {
            exps[i] = MathF.Exp(logits[i] - max);
            sum += exps[i];
        }

        for (int i = 0; i < logits.Length; i++)
        {
            exps[i] /= sum;
        }

        return exps;
    }
}
