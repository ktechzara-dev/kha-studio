using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Tools;
using VisionStudio.Imaging.Morphology;

namespace VisionStudio.Imaging.Tests;

public class MorphologyToolsTests
{
    [Fact]
    public void MorphologyTool_ErodesAndDilates()
    {
        using var mat = new Mat(50, 50, MatType.CV_8UC1, new Scalar(0));
        Cv2.Rectangle(mat, new Rect(10, 10, 30, 30), new Scalar(255), -1);
        using var frame = new ImageFrame(mat);

        var tool = new MorphologyTool();
        tool.Parameters.First(p => p.Name == MorphologyTool.ParamOperation).Value = "Erode";
        tool.Parameters.First(p => p.Name == MorphologyTool.ParamKernelSize).Value = 5;

        var ctx = new ToolContext(new Dictionary<string, object?> { [MorphologyTool.PortInImage] = frame });
        using var result = tool.Execute(ctx, CancellationToken.None);

        result.Status.Should().Be(ToolStatus.Ok);
        var eroded = result.GetImage(MorphologyTool.PortOutImage);
        Cv2.CountNonZero(eroded!.Mat).Should().BeLessThan(30 * 30);
    }

    [Fact]
    public void FillHolesTool_FillsInternalCavity()
    {
        using var mat = new Mat(100, 100, MatType.CV_8UC1, new Scalar(0));
        // Outer white ring with black hole in center
        Cv2.Circle(mat, new Point(50, 50), 35, new Scalar(255), -1);
        Cv2.Circle(mat, new Point(50, 50), 15, new Scalar(0), -1); // Cavity!
        using var frame = new ImageFrame(mat);

        mat.Get<byte>(50, 50).Should().Be(0); // Hole is black initially

        var tool = new FillHolesTool();
        var ctx = new ToolContext(new Dictionary<string, object?> { [FillHolesTool.PortInImage] = frame });
        using var result = tool.Execute(ctx, CancellationToken.None);

        result.Status.Should().Be(ToolStatus.Ok);
        var outFrame = result.GetImage(FillHolesTool.PortOutImage);
        outFrame!.Mat.Get<byte>(50, 50).Should().Be(255); // Cavity filled to white!
    }

    [Fact]
    public void SkeletonTool_ExtractsCenterline()
    {
        using var mat = new Mat(60, 60, MatType.CV_8UC1, new Scalar(0));
        // Draw 16px wide bar
        Cv2.Rectangle(mat, new Rect(10, 25, 40, 10), new Scalar(255), -1);
        using var frame = new ImageFrame(mat);

        var tool = new SkeletonTool();
        var ctx = new ToolContext(new Dictionary<string, object?> { [SkeletonTool.PortInImage] = frame });
        using var result = tool.Execute(ctx, CancellationToken.None);

        result.Status.Should().Be(ToolStatus.Ok);
        var skeleton = result.GetImage(SkeletonTool.PortOutImage);
        skeleton.Should().NotBeNull();
        // Skeleton has far fewer non-zero pixels than solid 40x10=400 area
        var skelPixels = Cv2.CountNonZero(skeleton!.Mat);
        skelPixels.Should().BeInRange(20, 80);
    }
}
