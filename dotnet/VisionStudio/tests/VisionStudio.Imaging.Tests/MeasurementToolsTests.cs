using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Tools;
using VisionStudio.Imaging.Measurement;

namespace VisionStudio.Imaging.Tests;

public class MeasurementToolsTests
{
    private static ImageFrame CreateSyntheticStepBar()
    {
        var mat = new Mat(150, 300, MatType.CV_8UC1, new Scalar(40));
        // Draw vertical strip 100px wide
        Cv2.Rectangle(mat, new Rect(100, 20, 100, 110), new Scalar(220), -1);
        return new ImageFrame(mat, "strip", 1);
    }

    [Fact]
    public void MultiCaliperTool_MeasuresRakeStatistics()
    {
        using var frame = CreateSyntheticStepBar();
        var tool = new MultiCaliperTool();
        tool.Parameters.First(p => p.Name == MultiCaliperTool.ParamCaliperCount).Value = 4;
        tool.Parameters.First(p => p.Name == MultiCaliperTool.ParamSpacing).Value = 12.0;
        tool.Parameters.First(p => p.Name == MultiCaliperTool.ParamStartX).Value = 50.0;
        tool.Parameters.First(p => p.Name == MultiCaliperTool.ParamStartY).Value = 75.0;
        tool.Parameters.First(p => p.Name == MultiCaliperTool.ParamEndX).Value = 250.0;
        tool.Parameters.First(p => p.Name == MultiCaliperTool.ParamEndY).Value = 75.0;

        var ctx = new ToolContext(new Dictionary<string, object?> { ["ImageIn"] = frame });
        using var result = tool.Execute(ctx, CancellationToken.None);

        result.Status.Should().Be(ToolStatus.Ok);
        var meanW = result.GetOutput<double>(MultiCaliperTool.PortMeanWidth);
        meanW.Should().BeInRange(99.0, 101.0);
    }

    [Fact]
    public void ProfileExtractionTool_ExtractsIntensityProfile()
    {
        using var frame = CreateSyntheticStepBar();
        var tool = new ProfileExtractionTool();
        var ctx = new ToolContext(new Dictionary<string, object?> { ["ImageIn"] = frame });

        using var result = tool.Execute(ctx, CancellationToken.None);
        result.Status.Should().Be(ToolStatus.Ok);
        result.GetOutput<double>(ProfileExtractionTool.PortMax).Should().Be(220.0);
    }

    [Fact]
    public void DimensionMeasureTool_CalibratesToMillimeters()
    {
        var tool = new DimensionMeasureTool();
        tool.Parameters.First(p => p.Name == DimensionMeasureTool.ParamNominal).Value = 10.0;
        tool.Parameters.First(p => p.Name == DimensionMeasureTool.ParamTolerance).Value = 0.5;
        tool.Parameters.First(p => p.Name == DimensionMeasureTool.ParamScaleMm).Value = 0.05; // 200px * 0.05 = 10.0 mm

        var ctx = new ToolContext(new Dictionary<string, object?> { [DimensionMeasureTool.PortPixelValue] = 200.0 });
        using var result = tool.Execute(ctx, CancellationToken.None);

        result.Status.Should().Be(ToolStatus.Ok);
        var dimMm = result.GetOutput<double>(DimensionMeasureTool.PortDimensionMm);
        var isPass = result.GetOutput<bool>(DimensionMeasureTool.PortPass);

        dimMm.Should().Be(10.0);
        isPass.Should().BeTrue();
    }
}
