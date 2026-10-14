using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Tools;
using VisionStudio.Imaging.Blob;

namespace VisionStudio.Imaging.Tests;

public class BlobAnalysisToolsTests
{
    private static ImageFrame CreateSyntheticBlobScene(int width = 300, int height = 240)
    {
        var mat = new Mat(height, width, MatType.CV_8UC1, new Scalar(0));

        // 1. Circle at (80, 80) with radius 20 (Area ~ PI * 20^2 = 1256.6, Circularity ~ 1.0)
        Cv2.Circle(mat, new Point(80, 80), 20, new Scalar(255), -1);

        // 2. Long rectangle at (180, 140) of size 60x20 (Area = 1200, low circularity ~ 0.58)
        Cv2.Rectangle(mat, new Rect(180, 140, 60, 20), new Scalar(255), -1);

        return new ImageFrame(mat, "blob-scene", 1);
    }

    [Fact]
    public void BlobAnalysisTool_MeasuresAreaCircularityAndCentroids()
    {
        using var frame = CreateSyntheticBlobScene();
        var tool = new BlobAnalysisTool();
        tool.Parameters.First(p => p.Name == BlobAnalysisTool.ParamMinArea).Value = 100.0;
        tool.Parameters.First(p => p.Name == BlobAnalysisTool.ParamSortBy).Value = "AreaDescending";

        var ctx = new ToolContext(new Dictionary<string, object?> { [BlobAnalysisTool.PortInImage] = frame });
        using var result = tool.Execute(ctx, CancellationToken.None);

        result.Status.Should().Be(ToolStatus.Ok);
        var count = result.GetOutput<int>(BlobAnalysisTool.PortBlobCount);
        count.Should().Be(2);

        var primaryArea = result.GetOutput<double>(BlobAnalysisTool.PortPrimaryArea);
        // Largest is circle (~1256)
        primaryArea.Should().BeInRange(1200.0, 1300.0);

        var primaryX = result.GetOutput<double>(BlobAnalysisTool.PortPrimaryCenterX);
        var primaryY = result.GetOutput<double>(BlobAnalysisTool.PortPrimaryCenterY);
        primaryX.Should().BeInRange(78.0, 82.0);
        primaryY.Should().BeInRange(78.0, 82.0);

        result.Overlays.Should().NotBeEmpty();
        result.GetImage(BlobAnalysisTool.PortLabeledImage).Should().NotBeNull();
    }

    [Fact]
    public void BlobAnalysisTool_FiltersByCircularityFeature()
    {
        using var frame = CreateSyntheticBlobScene();
        var tool = new BlobAnalysisTool();
        tool.Parameters.First(p => p.Name == BlobAnalysisTool.ParamMinArea).Value = 100.0;
        // Require high circularity (circle passes, rectangle is filtered out)
        tool.Parameters.First(p => p.Name == BlobAnalysisTool.ParamMinCircularity).Value = 0.85;

        var ctx = new ToolContext(new Dictionary<string, object?> { [BlobAnalysisTool.PortInImage] = frame });
        using var result = tool.Execute(ctx, CancellationToken.None);

        result.Status.Should().Be(ToolStatus.Ok);
        var count = result.GetOutput<int>(BlobAnalysisTool.PortBlobCount);
        count.Should().Be(1); // Only the circle passed!
    }
}
