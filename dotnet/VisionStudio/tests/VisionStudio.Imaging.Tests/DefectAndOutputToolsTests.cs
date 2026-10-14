using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Tools;
using VisionStudio.Imaging.Defect;
using VisionStudio.Imaging.Output;

namespace VisionStudio.Imaging.Tests;

public class DefectAndOutputToolsTests
{
    [Fact]
    public void GoldenTemplateDiffTool_IsolatesDefectPixelAnomaly()
    {
        using var goldenMat = new Mat(100, 100, MatType.CV_8UC1, new Scalar(100));
        using var liveMat = goldenMat.Clone();
        // Add artificial dark pit defect
        Cv2.Circle(liveMat, new Point(50, 50), 6, new Scalar(20), -1);

        using var goldenFrame = new ImageFrame(goldenMat);
        using var liveFrame = new ImageFrame(liveMat);

        var tool = new GoldenTemplateDiffTool();
        var ctx = new ToolContext(new Dictionary<string, object?>
        {
            [GoldenTemplateDiffTool.PortLive] = liveFrame,
            [GoldenTemplateDiffTool.PortGolden] = goldenFrame
        });

        using var result = tool.Execute(ctx, CancellationToken.None);
        result.Status.Should().Be(ToolStatus.Ok);
        var defectArea = result.GetOutput<int>(GoldenTemplateDiffTool.PortDefectArea);
        defectArea.Should().BeGreaterThan(50);
        result.Overlays.Should().NotBeEmpty();
    }

    [Fact]
    public void ScratchDetectionTool_DetectsElongatedLineScratch()
    {
        using var mat = new Mat(120, 120, MatType.CV_8UC1, new Scalar(150));
        // Scratch: dark hairline 40px long
        Cv2.Line(mat, new Point(20, 60), new Point(80, 60), new Scalar(30), 2);
        using var frame = new ImageFrame(mat);

        var tool = new ScratchDetectionTool();
        var ctx = new ToolContext(new Dictionary<string, object?> { [ScratchDetectionTool.PortInImage] = frame });

        using var result = tool.Execute(ctx, CancellationToken.None);
        result.Status.Should().Be(ToolStatus.Ok);
        var count = result.GetOutput<int>(ScratchDetectionTool.PortScratchCount);
        count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void SurfaceAnomalyTool_DetectsLocalDeviationWithoutGolden()
    {
        using var mat = new Mat(120, 120, MatType.CV_8UC1, new Scalar(120));
        // Add foreign particle spot
        Cv2.Circle(mat, new Point(60, 60), 8, new Scalar(230), -1);
        using var frame = new ImageFrame(mat);

        var tool = new SurfaceAnomalyTool();
        var ctx = new ToolContext(new Dictionary<string, object?> { [SurfaceAnomalyTool.PortInImage] = frame });

        using var result = tool.Execute(ctx, CancellationToken.None);
        result.Status.Should().Be(ToolStatus.Ok);
        var count = result.GetOutput<int>(SurfaceAnomalyTool.PortAnomalyCount);
        count.Should().Be(1);
    }

    [Fact]
    public void SaveImageTool_And_LogToCsvTool_PerformIOOperationsSuccessfully()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"vs_io_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);
            using var mat = new Mat(50, 50, MatType.CV_8UC3, new Scalar(0, 255, 0));
            using var frame = new ImageFrame(mat);

            // Test SaveImage
            var saveTool = new SaveImageTool();
            saveTool.Parameters.First(p => p.Name == SaveImageTool.ParamBaseFolder).Value = tempDir;

            var saveCtx = new ToolContext(new Dictionary<string, object?>
            {
                [SaveImageTool.PortInImage] = frame,
                [SaveImageTool.PortIsOk] = true
            });

            using var saveResult = saveTool.Execute(saveCtx, CancellationToken.None);
            saveResult.Status.Should().Be(ToolStatus.Ok);
            var savedFile = saveResult.GetOutput<string>(SaveImageTool.PortSavedPath);
            File.Exists(savedFile).Should().BeTrue();

            // Test LogToCsv
            var csvPath = Path.Combine(tempDir, "test_log.csv");
            var logTool = new LogToCsvTool();
            logTool.Parameters.First(p => p.Name == LogToCsvTool.ParamFilePath).Value = csvPath;

            var logCtx = new ToolContext(new Dictionary<string, object?>
            {
                [LogToCsvTool.PortValue1] = 12.345,
                [LogToCsvTool.PortVerdict] = "OK"
            }, executionIndex: 101);

            using var logResult = logTool.Execute(logCtx, CancellationToken.None);
            logResult.Status.Should().Be(ToolStatus.Ok);
            File.Exists(csvPath).Should().BeTrue();
            var content = File.ReadAllText(csvPath);
            content.Should().Contain("101");
            content.Should().Contain("12.345");
            content.Should().Contain("OK");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }
    }
}
