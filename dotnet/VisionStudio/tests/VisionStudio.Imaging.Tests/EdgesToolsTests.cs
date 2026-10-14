using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Tools;
using VisionStudio.Imaging.Edges;

namespace VisionStudio.Imaging.Tests;

public class EdgesToolsTests
{
    private static ImageFrame CreateSyntheticSquare(int size = 100)
    {
        var mat = new Mat(size, size, MatType.CV_8UC1, new Scalar(0));
        Cv2.Rectangle(mat, new Rect(25, 25, 50, 50), new Scalar(255), -1);
        return new ImageFrame(mat, "square", 1);
    }

    [Fact]
    public void CannyEdgeTool_DetectsPerimeterPixels()
    {
        using var frame = CreateSyntheticSquare();
        var tool = new CannyEdgeTool();
        var ctx = new ToolContext(new Dictionary<string, object?> { [CannyEdgeTool.PortInImage] = frame });

        using var result = tool.Execute(ctx, CancellationToken.None);
        result.Status.Should().Be(ToolStatus.Ok);
        var count = result.GetOutput<int>(CannyEdgeTool.PortEdgeCount);
        // Perimeter of 50x50 is approx 200 px
        count.Should().BeInRange(180, 220);
    }

    [Fact]
    public void SobelScharrTool_CalculatesGradients()
    {
        using var frame = CreateSyntheticSquare();
        var tool = new SobelScharrTool();
        var ctx = new ToolContext(new Dictionary<string, object?> { [SobelScharrTool.PortInImage] = frame });

        using var result = tool.Execute(ctx, CancellationToken.None);
        result.Status.Should().Be(ToolStatus.Ok);
        result.GetImage(SobelScharrTool.PortMagnitude).Should().NotBeNull();
    }

    [Fact]
    public void LaplacianTool_ExtractsSecondDerivative()
    {
        using var frame = CreateSyntheticSquare();
        var tool = new LaplacianTool();
        var ctx = new ToolContext(new Dictionary<string, object?> { [LaplacianTool.PortInImage] = frame });

        using var result = tool.Execute(ctx, CancellationToken.None);
        result.Status.Should().Be(ToolStatus.Ok);
        result.GetImage(LaplacianTool.PortOutImage).Should().NotBeNull();
    }

    [Fact]
    public void FindContoursTool_ExtractsHierarchyAndConvexHull()
    {
        using var frame = CreateSyntheticSquare();
        var tool = new FindContoursTool();
        var ctx = new ToolContext(new Dictionary<string, object?> { [FindContoursTool.PortInImage] = frame });

        using var result = tool.Execute(ctx, CancellationToken.None);
        result.Status.Should().Be(ToolStatus.Ok);
        var count = result.GetOutput<int>(FindContoursTool.PortContourCount);
        count.Should().Be(1);
        result.Overlays.Should().NotBeEmpty();
    }

    [Fact]
    public void SubPixelEdgeTool_DetectsSubPixelPoints()
    {
        using var frame = CreateSyntheticSquare();
        var tool = new SubPixelEdgeTool();
        var ctx = new ToolContext(new Dictionary<string, object?> { [SubPixelEdgeTool.PortInImage] = frame });

        using var result = tool.Execute(ctx, CancellationToken.None);
        result.Status.Should().Be(ToolStatus.Ok);
        var count = result.GetOutput<int>(SubPixelEdgeTool.PortPointCount);
        count.Should().BeGreaterThan(20);
    }
}
