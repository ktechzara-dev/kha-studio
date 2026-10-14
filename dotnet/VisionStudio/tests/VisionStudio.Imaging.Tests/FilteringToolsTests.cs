using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Tools;
using VisionStudio.Imaging.Filtering;

namespace VisionStudio.Imaging.Tests;

public class FilteringToolsTests
{
    private static ImageFrame CreateSyntheticTestPattern(int size = 128)
    {
        var mat = new Mat(size, size, MatType.CV_8UC1, new Scalar(40));
        Cv2.Circle(mat, new Point(size / 2, size / 2), size / 4, new Scalar(220), -1);
        return new ImageFrame(mat, "pattern", 1);
    }

    [Fact]
    public void GaussianFilterTool_SmoothsNoise()
    {
        using var frame = CreateSyntheticTestPattern();
        var tool = new GaussianFilterTool();
        var ctx = new ToolContext(new Dictionary<string, object?> { [GaussianFilterTool.PortInImage] = frame });

        using var result = tool.Execute(ctx, CancellationToken.None);
        result.Status.Should().Be(ToolStatus.Ok);
        result.GetImage(GaussianFilterTool.PortOutImage).Should().NotBeNull();
    }

    [Fact]
    public void MedianFilterTool_ReducesImpulses()
    {
        using var frame = CreateSyntheticTestPattern();
        var tool = new MedianFilterTool();
        var ctx = new ToolContext(new Dictionary<string, object?> { [MedianFilterTool.PortInImage] = frame });

        using var result = tool.Execute(ctx, CancellationToken.None);
        result.Status.Should().Be(ToolStatus.Ok);
        result.GetImage(MedianFilterTool.PortOutImage).Should().NotBeNull();
    }

    [Fact]
    public void BilateralFilterTool_PreservesEdges()
    {
        using var frame = CreateSyntheticTestPattern();
        var tool = new BilateralFilterTool();
        var ctx = new ToolContext(new Dictionary<string, object?> { [BilateralFilterTool.PortInImage] = frame });

        using var result = tool.Execute(ctx, CancellationToken.None);
        result.Status.Should().Be(ToolStatus.Ok);
        result.GetImage(BilateralFilterTool.PortOutImage).Should().NotBeNull();
    }

    [Fact]
    public void BoxFilterTool_BlursUniformly()
    {
        using var frame = CreateSyntheticTestPattern();
        var tool = new BoxFilterTool();
        var ctx = new ToolContext(new Dictionary<string, object?> { [BoxFilterTool.PortInImage] = frame });

        using var result = tool.Execute(ctx, CancellationToken.None);
        result.Status.Should().Be(ToolStatus.Ok);
        result.GetImage(BoxFilterTool.PortOutImage).Should().NotBeNull();
    }

    [Fact]
    public void SharpenTool_EnhancesTransitions()
    {
        using var frame = CreateSyntheticTestPattern();
        var tool = new SharpenTool();
        var ctx = new ToolContext(new Dictionary<string, object?> { [SharpenTool.PortInImage] = frame });

        using var result = tool.Execute(ctx, CancellationToken.None);
        result.Status.Should().Be(ToolStatus.Ok);
        result.GetImage(SharpenTool.PortOutImage).Should().NotBeNull();
    }

    [Fact]
    public void MorphologicalGradientTool_ExtractsOutline()
    {
        using var frame = CreateSyntheticTestPattern();
        var tool = new MorphologicalGradientTool();
        var ctx = new ToolContext(new Dictionary<string, object?> { [MorphologicalGradientTool.PortInImage] = frame });

        using var result = tool.Execute(ctx, CancellationToken.None);
        result.Status.Should().Be(ToolStatus.Ok);
        result.GetImage(MorphologicalGradientTool.PortOutImage).Should().NotBeNull();
    }

    [Fact]
    public void FlatFieldCorrectionTool_EqualizesVignetting()
    {
        using var rawMat = new Mat(100, 100, MatType.CV_8UC1, new Scalar(100));
        using var brightMat = new Mat(100, 100, MatType.CV_8UC1, new Scalar(200));

        using var rawFrame = new ImageFrame(rawMat);
        using var brightFrame = new ImageFrame(brightMat);

        var tool = new FlatFieldCorrectionTool();
        var ctx = new ToolContext(new Dictionary<string, object?>
        {
            [FlatFieldCorrectionTool.PortRaw] = rawFrame,
            [FlatFieldCorrectionTool.PortBright] = brightFrame
        });

        using var result = tool.Execute(ctx, CancellationToken.None);
        result.Status.Should().Be(ToolStatus.Ok);
        var corrected = result.GetImage(FlatFieldCorrectionTool.PortCorrected);
        corrected.Should().NotBeNull();
        // (100 / 200) * 200 = 100
        corrected!.Mat.Get<byte>(50, 50).Should().BeInRange(98, 102);
    }

    [Fact]
    public void FftBandpassTool_ExecutesFourierDomainFiltering()
    {
        using var frame = CreateSyntheticTestPattern(64);
        var tool = new FftBandpassTool();
        var ctx = new ToolContext(new Dictionary<string, object?> { [FftBandpassTool.PortInImage] = frame });

        using var result = tool.Execute(ctx, CancellationToken.None);
        result.Status.Should().Be(ToolStatus.Ok);
        result.GetImage(FftBandpassTool.PortOutImage).Should().NotBeNull();
        result.GetImage(FftBandpassTool.PortMagnitude).Should().NotBeNull();
    }
}
