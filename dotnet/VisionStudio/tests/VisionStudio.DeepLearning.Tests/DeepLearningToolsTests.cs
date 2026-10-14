using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Tools;
using VisionStudio.Core.Types;
using VisionStudio.DeepLearning.Tools;

namespace VisionStudio.DeepLearning.Tests;

public sealed class DeepLearningToolsTests
{
    private static ImageFrame CreateTestFrame(int width = 120, int height = 120)
    {
        var mat = new Mat(height, width, MatType.CV_8UC3, new Scalar(180, 180, 180));
        // Draw a simulated dark feature in the center
        Cv2.Circle(mat, new Point(width / 2, height / 2), 25, new Scalar(40, 40, 40), -1);
        return new ImageFrame(mat);
    }

    private static void SetParam(IVisionTool tool, string name, object value)
    {
        var p = tool.Parameters.First(param => param.Name == name);
        p.Value = value;
    }

    [Fact]
    public void DeepAnomalyDetectionTool_ExecutesSuccessfullyAndOutputsHeatmapAndRegion()
    {
        var tool = new DeepAnomalyDetectionTool();
        SetParam(tool, DeepAnomalyDetectionTool.ParamThreshold, 0.8);

        var frame = CreateTestFrame();
        var ctx = new ToolContext(
            inputs: new Dictionary<string, object?> { [DeepAnomalyDetectionTool.PortImageIn] = frame });

        var result = tool.Execute(ctx, CancellationToken.None);

        result.Status.Should().Be(ToolStatus.Ok);
        result.Outputs.Should().ContainKey(DeepAnomalyDetectionTool.PortHeatmapOut);
        result.Outputs.Should().ContainKey(DeepAnomalyDetectionTool.PortAnomalyScore);
        result.Outputs.Should().ContainKey(DeepAnomalyDetectionTool.PortDefectRegion);
        result.Outputs.Should().ContainKey(DeepAnomalyDetectionTool.PortPassed);

        var heatmapFrame = result.Outputs[DeepAnomalyDetectionTool.PortHeatmapOut] as ImageFrame;
        heatmapFrame.Should().NotBeNull();
        heatmapFrame!.Mat.Should().NotBeNull();

        var score = (double)result.Outputs[DeepAnomalyDetectionTool.PortAnomalyScore]!;
        score.Should().BeGreaterThanOrEqualTo(0.0);

        var region = result.Outputs[DeepAnomalyDetectionTool.PortDefectRegion] as Region;
        region.Should().NotBeNull();
    }

    [Fact]
    public void DeepClassificationTool_ExecutesSuccessfullyAndOutputsPredictionsTable()
    {
        var tool = new DeepClassificationTool();
        SetParam(tool, DeepClassificationTool.ParamMinConfidence, 0.1);
        SetParam(tool, DeepClassificationTool.ParamTopK, 3);

        var frame = CreateTestFrame();
        var ctx = new ToolContext(
            inputs: new Dictionary<string, object?> { [DeepClassificationTool.PortImageIn] = frame });

        var result = tool.Execute(ctx, CancellationToken.None);

        result.Status.Should().Be(ToolStatus.Ok);
        result.Outputs.Should().ContainKey(DeepClassificationTool.PortTopClass);
        result.Outputs.Should().ContainKey(DeepClassificationTool.PortConfidence);
        result.Outputs.Should().ContainKey(DeepClassificationTool.PortPredictionsTable);
        result.Outputs.Should().ContainKey(DeepClassificationTool.PortPassed);

        var topClass = result.Outputs[DeepClassificationTool.PortTopClass] as string;
        topClass.Should().NotBeNullOrWhiteSpace();

        var table = result.Outputs[DeepClassificationTool.PortPredictionsTable] as VisionTable;
        table.Should().NotBeNull();
        table!.RowCount.Should().BeGreaterThan(0);
        table.Columns.Select(c => c.Name).Should().Contain(new[] { "Rank", "Class", "Confidence" });
    }

    [Fact]
    public void DeepObjectDetectionTool_ExecutesSuccessfullyAndOutputsDetectionsTable()
    {
        var tool = new DeepObjectDetectionTool();
        SetParam(tool, DeepObjectDetectionTool.ParamConfidence, 0.3);
        SetParam(tool, DeepObjectDetectionTool.ParamMinCount, 1);
        SetParam(tool, DeepObjectDetectionTool.ParamMaxCount, 10);

        var frame = CreateTestFrame();
        var ctx = new ToolContext(
            inputs: new Dictionary<string, object?> { [DeepObjectDetectionTool.PortImageIn] = frame });

        var result = tool.Execute(ctx, CancellationToken.None);

        result.Status.Should().Be(ToolStatus.Ok);
        result.Outputs.Should().ContainKey(DeepObjectDetectionTool.PortAnnotatedImage);
        result.Outputs.Should().ContainKey(DeepObjectDetectionTool.PortDetectionsTable);
        result.Outputs.Should().ContainKey(DeepObjectDetectionTool.PortTotalCount);
        result.Outputs.Should().ContainKey(DeepObjectDetectionTool.PortPassed);

        int count = Convert.ToInt32(result.Outputs[DeepObjectDetectionTool.PortTotalCount]);
        count.Should().BeGreaterThan(0);

        var table = result.Outputs[DeepObjectDetectionTool.PortDetectionsTable] as VisionTable;
        table.Should().NotBeNull();
        table!.RowCount.Should().Be(count);
    }

    [Fact]
    public void DeepSegmentationTool_ExecutesSuccessfullyAndOutputsRegionGeometry()
    {
        var tool = new DeepSegmentationTool();
        SetParam(tool, DeepSegmentationTool.ParamMinArea, 10);
        SetParam(tool, DeepSegmentationTool.ParamMaxArea, 500000);

        var frame = CreateTestFrame();
        var ctx = new ToolContext(
            inputs: new Dictionary<string, object?> { [DeepSegmentationTool.PortImageIn] = frame });

        var result = tool.Execute(ctx, CancellationToken.None);

        result.Status.Should().Be(ToolStatus.Ok);
        result.Outputs.Should().ContainKey(DeepSegmentationTool.PortMaskOverlay);
        result.Outputs.Should().ContainKey(DeepSegmentationTool.PortSegmentedRegion);
        result.Outputs.Should().ContainKey(DeepSegmentationTool.PortTotalArea);
        result.Outputs.Should().ContainKey(DeepSegmentationTool.PortPassed);

        var region = result.Outputs[DeepSegmentationTool.PortSegmentedRegion] as Region;
        region.Should().NotBeNull();
        region!.Area.Should().BeGreaterThan(0);

        int totalArea = Convert.ToInt32(result.Outputs[DeepSegmentationTool.PortTotalArea]);
        totalArea.Should().Be((int)region.Area);
    }

    [Fact]
    public void DeepOcrTool_ExecutesSuccessfullyAndVerifiesTextPattern()
    {
        var tool = new DeepOcrTool();
        SetParam(tool, DeepOcrTool.ParamPattern, "SN-*");
        SetParam(tool, DeepOcrTool.ParamMinConfidence, 0.5);

        var frame = CreateTestFrame(width: 100, height: 32);
        var ctx = new ToolContext(
            inputs: new Dictionary<string, object?> { [DeepOcrTool.PortImageIn] = frame });

        var result = tool.Execute(ctx, CancellationToken.None);

        result.Status.Should().Be(ToolStatus.Ok);
        result.Outputs.Should().ContainKey(DeepOcrTool.PortRecognizedText);
        result.Outputs.Should().ContainKey(DeepOcrTool.PortConfidence);
        result.Outputs.Should().ContainKey(DeepOcrTool.PortPassed);

        string text = (string)result.Outputs[DeepOcrTool.PortRecognizedText]!;
        text.Should().StartWith("SN-");

        bool passed = (bool)result.Outputs[DeepOcrTool.PortPassed]!;
        passed.Should().BeTrue();
    }
}
