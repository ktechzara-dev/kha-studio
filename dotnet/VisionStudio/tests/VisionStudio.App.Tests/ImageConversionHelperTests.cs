using OpenCvSharp;
using VisionStudio.App.Converters;
using VisionStudio.Core.Models;

namespace VisionStudio.App.Tests;

public class ImageConversionHelperTests
{
    [Fact]
    public void SamplePixel_ReturnsAccurateColorValues()
    {
        // 100x100 BGR image with a known color at (50, 50)
        using var mat = new Mat(100, 100, MatType.CV_8UC3, new Scalar(10, 20, 30));
        mat.Set(50, 50, new Vec3b(255, 128, 64)); // B=255, G=128, R=64

        var (r, g, b, gray, ok) = ImageConversionHelper.SamplePixel(mat, 50, 50);

        ok.Should().BeTrue();
        r.Should().Be(64);
        g.Should().Be(128);
        b.Should().Be(255);

        // Out of bounds
        var outOfBounds = ImageConversionHelper.SamplePixel(mat, -1, 50);
        outOfBounds.IsValid.Should().BeFalse();
    }

    [Fact]
    public void SamplePixel_GrayscaleImage_ReturnsConsistentValues()
    {
        using var mat = new Mat(50, 50, MatType.CV_8UC1, new Scalar(175));

        var (r, g, b, gray, ok) = ImageConversionHelper.SamplePixel(mat, 25, 25);

        ok.Should().BeTrue();
        gray.Should().Be(175);
        r.Should().Be(175);
        g.Should().Be(175);
        b.Should().Be(175);
    }

    [Fact]
    public void MatToBitmapSource_ProducesFrozenValidBitmap()
    {
        // OpenCV Mat(rows, cols): rows = Height = 48, cols = Width = 64
        using var mat = new Mat(48, 64, MatType.CV_8UC3, new Scalar(0, 255, 0));

        var bmp = ImageConversionHelper.MatToBitmapSource(mat);

        bmp.Should().NotBeNull();
        bmp!.PixelWidth.Should().Be(64);
        bmp.PixelHeight.Should().Be(48);
        bmp.IsFrozen.Should().BeTrue();
    }

    [Fact]
    public void CreateThumbnail_ScalesWithinMaxDimension()
    {
        using var mat = new Mat(400, 200, MatType.CV_8UC1, new Scalar(100)); // Height 400, Width 200

        var thumb = ImageConversionHelper.CreateThumbnail(mat, maxDimension: 100);

        thumb.Should().NotBeNull();
        thumb!.PixelHeight.Should().Be(100);
        thumb.PixelWidth.Should().Be(50);
    }
}
