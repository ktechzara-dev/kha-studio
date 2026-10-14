using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;
using OpenCvSharp;

namespace KhaStudio.Modules.ScadaVision.Helpers;

public static class ImageUtils
{
    /// <summary>
    /// Chuyển đổi OpenCV Mat sang BitmapSource cho WPF Image Control.
    /// </summary>
    public static BitmapSource MatToBitmapSource(Mat mat)
    {
        if (mat == null || mat.IsDisposed || mat.Empty())
            return null!;

        // Dùng BitmapSourceConverter hoặc encode JPEG/PNG trong memory stream để an toàn thread
        using var ms = mat.ToMemoryStream(".png");
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = ms;
        image.EndInit();
        image.Freeze(); // Cho phép hiển thị trên UI thread bất kể tạo từ thread nào
        return image;
    }

    /// <summary>
    /// Chuyển đổi GDI+ Bitmap sang WPF BitmapSource.
    /// </summary>
    public static BitmapSource BitmapToBitmapSource(Bitmap bitmap)
    {
        if (bitmap == null) return null!;

        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Bmp);
        ms.Seek(0, SeekOrigin.Begin);

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = ms;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
