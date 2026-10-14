using System.IO;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using VisionStudio.Core.Models;

namespace VisionStudio.App.Converters;

/// <summary>
/// High-performance converter between OpenCV Mats/ImageFrames and WPF BitmapSources.
/// </summary>
public static class ImageConversionHelper
{
    /// <summary>
    /// Converts an OpenCV Mat into a frozen, UI-thread safe BitmapSource.
    /// </summary>
    public static BitmapSource? MatToBitmapSource(Mat? mat)
    {
        if (mat == null || mat.IsDisposed || mat.Empty())
        {
            return null;
        }

        try
        {
            var bitmap = BitmapSourceConverter.ToBitmapSource(mat);
            if (bitmap.CanFreeze && !bitmap.IsFrozen)
            {
                bitmap.Freeze();
            }
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Converts an ImageFrame into a frozen BitmapSource.
    /// </summary>
    public static BitmapSource? FrameToBitmapSource(ImageFrame? frame)
    {
        if (frame == null) return null;
        return MatToBitmapSource(frame.Mat);
    }

    /// <summary>
    /// Generates a scaled thumbnail BitmapSource for the inspection strip or node cards.
    /// </summary>
    public static BitmapSource? CreateThumbnail(Mat? mat, int maxDimension = 96)
    {
        if (mat == null || mat.IsDisposed || mat.Empty()) return null;

        try
        {
            var w = mat.Width;
            var h = mat.Height;
            if (w <= 0 || h <= 0) return null;

            var scale = Math.Min((double)maxDimension / w, (double)maxDimension / h);
            if (scale >= 1.0)
            {
                return MatToBitmapSource(mat);
            }

            var tw = Math.Max(1, (int)(w * scale));
            var th = Math.Max(1, (int)(h * scale));

            using var thumbMat = new Mat();
            Cv2.Resize(mat, thumbMat, new OpenCvSharp.Size(tw, th), 0, 0, InterpolationFlags.Area);
            return MatToBitmapSource(thumbMat);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Reads pixel color values (RGB + Gray) at specified image coordinates.
    /// </summary>
    public static (byte R, byte G, byte B, byte Gray, bool IsValid) SamplePixel(Mat? mat, int x, int y)
    {
        if (mat == null || mat.IsDisposed || mat.Empty())
        {
            return (0, 0, 0, 0, false);
        }

        if (x < 0 || x >= mat.Width || y < 0 || y >= mat.Height)
        {
            return (0, 0, 0, 0, false);
        }

        try
        {
            if (mat.Channels() == 1)
            {
                var val = mat.At<byte>(y, x);
                return (val, val, val, val, true);
            }
            if (mat.Channels() == 3)
            {
                var vec = mat.At<Vec3b>(y, x); // BGR
                var gray = (byte)(0.299 * vec.Item2 + 0.587 * vec.Item1 + 0.114 * vec.Item0);
                return (vec.Item2, vec.Item1, vec.Item0, gray, true); // R, G, B
            }
            if (mat.Channels() == 4)
            {
                var vec = mat.At<Vec4b>(y, x); // BGRA
                var gray = (byte)(0.299 * vec.Item2 + 0.587 * vec.Item1 + 0.114 * vec.Item0);
                return (vec.Item2, vec.Item1, vec.Item0, gray, true);
            }

            return (0, 0, 0, 0, false);
        }
        catch
        {
            return (0, 0, 0, 0, false);
        }
    }
}
