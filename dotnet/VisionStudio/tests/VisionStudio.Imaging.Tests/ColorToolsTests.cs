using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Tools;
using VisionStudio.Imaging.Color;

namespace VisionStudio.Imaging.Tests;

public class ColorToolsTests
{
    private static ImageFrame CreateSyntheticColorFrame(int width = 100, int height = 100)
    {
        var mat = new Mat(height, width, MatType.CV_8UC3, new Scalar(100, 150, 200));
        // Draw distinct green square at center
        Cv2.Rectangle(mat, new Rect(30, 30, 40, 40), new Scalar(0, 255, 0), -1);
        return new ImageFrame(mat, "synthetic", 1);
    }

    [Fact]
    public void GrayscaleTool_ConvertsBgrTo1Channel()
    {
        using var frame = CreateSyntheticColorFrame();
        var tool = new GrayscaleTool();
        var ctx = new ToolContext(new Dictionary<string, object?> { [GrayscaleTool.PortInImage] = frame });

        using var result = tool.Execute(ctx, CancellationToken.None);
        result.Status.Should().Be(ToolStatus.Ok);
        var outFrame = result.GetImage(GrayscaleTool.PortOutImage);
        outFrame.Should().NotBeNull();
        outFrame!.Channels.Should().Be(1);
        outFrame.Format.Should().Be(PixelFormat.Mono8);
    }

    [Fact]
    public void ChannelSplitTool_SplitsToBgrChannels()
    {
        using var frame = CreateSyntheticColorFrame();
        var tool = new ChannelSplitTool();
        var ctx = new ToolContext(new Dictionary<string, object?> { [ChannelSplitTool.PortInImage] = frame });

        using var result = tool.Execute(ctx, CancellationToken.None);
        result.Status.Should().Be(ToolStatus.Ok);
        result.GetImage(ChannelSplitTool.PortBlue).Should().NotBeNull();
        result.GetImage(ChannelSplitTool.PortGreen).Should().NotBeNull();
        result.GetImage(ChannelSplitTool.PortRed).Should().NotBeNull();
    }

    [Fact]
    public void ColorSpaceConvertTool_ConvertsToHsv()
    {
        using var frame = CreateSyntheticColorFrame();
        var tool = new ColorSpaceConvertTool();
        tool.Parameters.First(p => p.Name == ColorSpaceConvertTool.ParamSpace).Value = "HSV";
        var ctx = new ToolContext(new Dictionary<string, object?> { [ColorSpaceConvertTool.PortInImage] = frame });

        using var result = tool.Execute(ctx, CancellationToken.None);
        result.Status.Should().Be(ToolStatus.Ok);
        result.GetImage(ColorSpaceConvertTool.PortOutImage).Should().NotBeNull();
        result.GetImage(ColorSpaceConvertTool.PortChannel1).Should().NotBeNull(); // H
    }

    [Fact]
    public void BrightnessContrastTool_AdjustsIntensity()
    {
        using var frame = CreateSyntheticColorFrame();
        var tool = new BrightnessContrastTool();
        tool.Parameters.First(p => p.Name == BrightnessContrastTool.ParamContrast).Value = 1.2;
        tool.Parameters.First(p => p.Name == BrightnessContrastTool.ParamBrightness).Value = 20.0;
        var ctx = new ToolContext(new Dictionary<string, object?> { [BrightnessContrastTool.PortInImage] = frame });

        using var result = tool.Execute(ctx, CancellationToken.None);
        result.Status.Should().Be(ToolStatus.Ok);
        result.GetImage(BrightnessContrastTool.PortOutImage).Should().NotBeNull();
    }

    [Fact]
    public void GammaTool_AppliesLutCurve()
    {
        using var frame = CreateSyntheticColorFrame();
        var tool = new GammaTool();
        tool.Parameters.First(p => p.Name == GammaTool.ParamGamma).Value = 1.8;
        var ctx = new ToolContext(new Dictionary<string, object?> { [GammaTool.PortInImage] = frame });

        using var result = tool.Execute(ctx, CancellationToken.None);
        result.Status.Should().Be(ToolStatus.Ok);
        result.GetImage(GammaTool.PortOutImage).Should().NotBeNull();
    }

    [Fact]
    public void HistogramEqualizeTool_EqualizesAndClahe()
    {
        using var frame = CreateSyntheticColorFrame();
        var tool = new HistogramEqualizeTool();
        tool.Parameters.First(p => p.Name == HistogramEqualizeTool.ParamMethod).Value = "CLAHE";
        var ctx = new ToolContext(new Dictionary<string, object?> { [HistogramEqualizeTool.PortInImage] = frame });

        using var result = tool.Execute(ctx, CancellationToken.None);
        result.Status.Should().Be(ToolStatus.Ok);
        result.GetImage(HistogramEqualizeTool.PortOutImage).Should().NotBeNull();
    }

    [Fact]
    public void NormalizeTool_StretchesDynamicRange()
    {
        using var frame = CreateSyntheticColorFrame();
        var tool = new NormalizeTool();
        var ctx = new ToolContext(new Dictionary<string, object?> { [NormalizeTool.PortInImage] = frame });

        using var result = tool.Execute(ctx, CancellationToken.None);
        result.Status.Should().Be(ToolStatus.Ok);
        result.GetImage(NormalizeTool.PortOutImage).Should().NotBeNull();
    }

    [Fact]
    public void InvertTool_InvertsPixels()
    {
        using var mat = new Mat(10, 10, MatType.CV_8UC1, new Scalar(100));
        using var frame = new ImageFrame(mat);

        var tool = new InvertTool();
        var ctx = new ToolContext(new Dictionary<string, object?> { [InvertTool.PortInImage] = frame });

        using var result = tool.Execute(ctx, CancellationToken.None);
        result.Status.Should().Be(ToolStatus.Ok);
        var outFrame = result.GetImage(InvertTool.PortOutImage);
        outFrame!.Mat.Get<byte>(0, 0).Should().Be(155); // 255 - 100
    }

    [Fact]
    public void ColorThresholdTool_DetectsGreenSquare()
    {
        using var frame = CreateSyntheticColorFrame(100, 100);
        var tool = new ColorThresholdTool();
        tool.Parameters.First(p => p.Name == ColorThresholdTool.ParamSpace).Value = "HSV";
        // Green in HSV: H in [35, 85], S in [50, 255], V in [50, 255]
        tool.Parameters.First(p => p.Name == ColorThresholdTool.ParamMinCh1).Value = 35;
        tool.Parameters.First(p => p.Name == ColorThresholdTool.ParamMaxCh1).Value = 85;

        var ctx = new ToolContext(new Dictionary<string, object?> { [ColorThresholdTool.PortInImage] = frame });
        using var result = tool.Execute(ctx, CancellationToken.None);

        result.Status.Should().Be(ToolStatus.Ok);
        var count = result.GetOutput<int>(ColorThresholdTool.PortPixelCount);
        // The green square is 40x40 = 1600 pixels
        count.Should().Be(1600);
    }
}
