using OpenCvSharp;
using VisionStudio.Core.Models;

namespace VisionStudio.Core.Tests.Models;

public class ImageFrameTests
{
    [Fact]
    public void ImageFrame_InitializesMetadataAndDimensionsCorrectly()
    {
        using var mat = new Mat(100, 200, MatType.CV_8UC3, new Scalar(10, 20, 30));
        using var frame = new ImageFrame(mat, cameraId: "Cam_01", frameId: 101);

        frame.Width.Should().Be(200);
        frame.Height.Should().Be(100);
        frame.Channels.Should().Be(3);
        frame.CameraId.Should().Be("Cam_01");
        frame.FrameId.Should().Be(101);
        frame.Format.Should().Be(PixelFormat.Bgr24);
        frame.IsDisposed.Should().BeFalse();
    }

    [Fact]
    public void Clone_CreatesIndependentDeepCopyOfNativeMat()
    {
        using var mat = new Mat(50, 50, MatType.CV_8UC1, new Scalar(128));
        using var original = new ImageFrame(mat, cameraId: "Source");
        using var clone = original.Clone();

        clone.Should().NotBeSameAs(original);
        clone.Mat.CvPtr.Should().NotBe(original.Mat.CvPtr);
        clone.Width.Should().Be(50);
        clone.Height.Should().Be(50);

        // Modify original mat pixel to ensure independence
        original.Mat.Set(0, 0, (byte)255);
        clone.Mat.Get<byte>(0, 0).Should().Be(128);
    }

    [Fact]
    public void Dispose_ReleasesMatAndThrowsOnSubsequentAccess()
    {
        var mat = new Mat(10, 10, MatType.CV_8UC1);
        var frame = new ImageFrame(mat);

        frame.Dispose();

        frame.IsDisposed.Should().BeTrue();
        var act = () => _ = frame.Mat;
        act.Should().Throw<ObjectDisposedException>();
    }
}
