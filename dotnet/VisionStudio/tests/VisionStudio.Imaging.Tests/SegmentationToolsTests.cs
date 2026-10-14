using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Tools;
using VisionStudio.Imaging.Segmentation;

namespace VisionStudio.Imaging.Tests;

public class SegmentationToolsTests
{
    private static ImageFrame CreateSyntheticTouchingDisks(int width = 200, int height = 100)
    {
        var mat = new Mat(height, width, MatType.CV_8UC1, new Scalar(40));
        // Draw two touching disks
        Cv2.Circle(mat, new Point(60, 50), 30, new Scalar(210), -1);
        Cv2.Circle(mat, new Point(120, 50), 30, new Scalar(210), -1);
        return new ImageFrame(mat, "disks", 1);
    }

    [Fact]
    public void ThresholdTool_OtsuComputesBimodalThreshold()
    {
        using var frame = CreateSyntheticTouchingDisks();
        var tool = new ThresholdTool();
        tool.Parameters.First(p => p.Name == ThresholdTool.ParamMethod).Value = "Otsu";

        var ctx = new ToolContext(new Dictionary<string, object?> { [ThresholdTool.PortInImage] = frame });
        using var result = tool.Execute(ctx, CancellationToken.None);

        result.Status.Should().Be(ToolStatus.Ok);
        var thresh = result.GetOutput<double>(ThresholdTool.PortCalculatedThreshold);
        thresh.Should().BeGreaterThan(0);
        result.GetImage(ThresholdTool.PortMask).Should().NotBeNull();
    }

    [Fact]
    public void DualThresholdTool_FiltersIntensityRange()
    {
        using var frame = CreateSyntheticTouchingDisks();
        var tool = new DualThresholdTool();
        tool.Parameters.First(p => p.Name == DualThresholdTool.ParamLow).Value = 100.0;
        tool.Parameters.First(p => p.Name == DualThresholdTool.ParamHigh).Value = 255.0;

        var ctx = new ToolContext(new Dictionary<string, object?> { [DualThresholdTool.PortInImage] = frame });
        using var result = tool.Execute(ctx, CancellationToken.None);

        result.Status.Should().Be(ToolStatus.Ok);
        var count = result.GetOutput<int>(DualThresholdTool.PortCount);
        count.Should().BeGreaterThan(1000);
    }

    [Fact]
    public void WatershedTool_SeparatesTouchingDisks()
    {
        using var frame = CreateSyntheticTouchingDisks();
        var tool = new WatershedTool();
        var ctx = new ToolContext(new Dictionary<string, object?> { [WatershedTool.PortInImage] = frame });

        using var result = tool.Execute(ctx, CancellationToken.None);
        result.Status.Should().Be(ToolStatus.Ok);
        var count = result.GetOutput<int>(WatershedTool.PortObjectCount);
        count.Should().Be(2); // Accurately separates the 2 disks!
    }

    [Fact]
    public void KMeansSegmentationTool_ClustersColors()
    {
        using var mat = new Mat(50, 50, MatType.CV_8UC3, new Scalar(200, 0, 0));
        Cv2.Rectangle(mat, new Rect(0, 0, 25, 50), new Scalar(0, 200, 0), -1);
        using var frame = new ImageFrame(mat);

        var tool = new KMeansSegmentationTool();
        tool.Parameters.First(p => p.Name == KMeansSegmentationTool.ParamK).Value = 2;

        var ctx = new ToolContext(new Dictionary<string, object?> { [KMeansSegmentationTool.PortInImage] = frame });
        using var result = tool.Execute(ctx, CancellationToken.None);

        result.Status.Should().Be(ToolStatus.Ok);
        result.GetImage(KMeansSegmentationTool.PortOutImage).Should().NotBeNull();
    }
}
