using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Tools;
using VisionStudio.Imaging.Stitching;
using Xunit;

namespace VisionStudio.Imaging.Tests;

public class ImageStitchingTests
{
    [Fact]
    public void ImageStitcher_AccumulateLines_BuildsFullFrame()
    {
        var strips = new List<Mat>();
        for (int i = 0; i < 5; i++)
        {
            var strip = new Mat(20, 100, MatType.CV_8UC1, new Scalar(i * 40));
            strips.Add(strip);
        }

        var fullFrame = ImageStitcher.AccumulateLines(strips, 100, 100);
        Assert.Equal(100, fullFrame.Width);
        Assert.Equal(100, fullFrame.Height);

        // Verify content of first and second strips
        Assert.Equal(0, fullFrame.At<byte>(10, 50));
        Assert.Equal(40, fullFrame.At<byte>(30, 50));

        foreach (var s in strips) s.Dispose();
        fullFrame.Dispose();
    }

    [Fact]
    public void ImageStitchingTool_ExecutesAndOutputsComposite()
    {
        using var imgA = new Mat(100, 100, MatType.CV_8UC1, new Scalar(50));
        using var imgB = new Mat(100, 100, MatType.CV_8UC1, new Scalar(100));

        var tool = new ImageStitchingTool();
        var context = new ToolContext(new Dictionary<string, object?>
        {
            [ImageStitchingTool.PortInImageA] = new ImageFrame(imgA),
            [ImageStitchingTool.PortInImageB] = new ImageFrame(imgB)
        });

        using var result = tool.Execute(context, CancellationToken.None);
        Assert.Equal(ToolStatus.Ok, result.Status);

        var stitched = result.GetOutput<ImageFrame>(ImageStitchingTool.PortOutStitched);
        Assert.NotNull(stitched);
        Assert.True(stitched.Width >= 100);
    }
}
