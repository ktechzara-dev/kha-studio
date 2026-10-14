using OpenCvSharp;
using VisionStudio.Imaging;

namespace VisionStudio.Imaging.Tests;

public class ImagingHarnessTests
{
    [Fact]
    public void OpenCvSharp_NativeRuntimeInitializesAndAllocatesMat()
    {
        // Arrange & Act
        using var mat = new Mat(rows: 240, cols: 320, type: MatType.CV_8UC3, s: new Scalar(50, 100, 150));

        // Assert
        mat.Empty().Should().BeFalse();
        mat.Rows.Should().Be(240);
        mat.Cols.Should().Be(320);
        mat.Channels().Should().Be(3);
        ImagingModule.ModuleName.Should().Be("VisionStudio.Imaging");
    }
}
