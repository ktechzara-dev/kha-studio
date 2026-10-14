using FluentAssertions;
using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Pipeline;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Testing;
using VisionStudio.Core.Tools;
using VisionStudio.Core.Traceability;
using Xunit;

namespace VisionStudio.Core.Tests.Testing;

public sealed class BrightnessCheckTool : VisionToolBase
{
    public ToolParameter MinMean { get; }

    public BrightnessCheckTool() : base("Test.BrightnessCheck", "Brightness Check", "Test")
    {
        MinMean = AddParameter(new ToolParameter("MinMean", "Minimum Mean Brightness", ParameterType.Double, 100.0));

        AddInput(new ToolPort("ImageIn", "Input Image", PortType.Image, PortDirection.Input));
        AddOutput(new ToolPort("MeanBrightness", "Mean Brightness", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort("Passed", "Passed", PortType.Boolean, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var frame = ctx.GetImage("ImageIn");
        if (frame == null || frame.Mat.Empty())
        {
            return ToolResult.Failed("Missing input image.");
        }

        var meanScalar = Cv2.Mean(frame.Mat);
        double meanVal = meanScalar.Val0;
        double minReq = MinMean.GetValue<double>();
        bool passed = meanVal >= minReq;

        var outputs = new Dictionary<string, object?>
        {
            ["MeanBrightness"] = meanVal,
            ["Passed"] = passed
        };

        return ToolResult.Ok(outputs);
    }
}

public class OfflineReplayTests : IDisposable
{
    private readonly string _testDir;

    public OfflineReplayTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "VS_ReplayTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir)) Directory.Delete(_testDir, true);
        }
        catch { }
    }

    [Fact]
    public async Task OfflineReplayEngine_RunsTestSet_AndValidatesVerdicts()
    {
        // 1. Create synthetic test images
        string passImgPath = Path.Combine(_testDir, "pass_img.png");
        string failImgPath = Path.Combine(_testDir, "fail_img.png");

        using (var matPass = new Mat(50, 50, MatType.CV_8UC1, new Scalar(150))) // Mean 150 > 100 -> Pass
        {
            Cv2.ImWrite(passImgPath, matPass);
        }

        using (var matFail = new Mat(50, 50, MatType.CV_8UC1, new Scalar(50)))  // Mean 50 < 100 -> Fail
        {
            Cv2.ImWrite(failImgPath, matFail);
        }

        // 2. Setup Pipeline Graph
        var graph = new PipelineGraph();
        var tool = new BrightnessCheckTool();
        var node = new PipelineNode("BrightnessNode", tool);
        graph.AddNode(node);

        var executor = new PipelineExecutor();

        // 3. Define Golden Test Set
        var testSet = new TestSet
        {
            Name = "Brightness Acceptance Suite",
            Items = new List<TestSetItem>
            {
                new()
                {
                    ImagePath = passImgPath,
                    ExpectedVerdict = InspectionVerdict.Pass,
                    ExpectedMeasurements = new()
                    {
                        ["BrightnessNode.MeanBrightness"] = new("Brightness", 150.0, 140.0, 160.0)
                    }
                },
                new()
                {
                    ImagePath = failImgPath,
                    ExpectedVerdict = InspectionVerdict.Fail
                }
            }
        };

        // 4. Run Offline Replay
        var replayEngine = new OfflineReplayEngine();
        var report = await replayEngine.RunTestSetAsync(executor, graph, testSet);

        // 5. Assertions
        report.TotalItems.Should().Be(2);
        report.PassedItems.Should().Be(2);
        report.FailedItems.Should().Be(0);
        report.AllPassed.Should().BeTrue();
        report.PassRate.Should().Be(1.0);
    }

    [Fact]
    public async Task OfflineReplayEngine_ReplayTraceBatch_DetectsAgreement()
    {
        string sampleImg = Path.Combine(_testDir, "sample.png");
        using (var mat = new Mat(32, 32, MatType.CV_8UC1, new Scalar(120)))
        {
            Cv2.ImWrite(sampleImg, mat);
        }

        var graph = new PipelineGraph();
        var tool = new BrightnessCheckTool();
        graph.AddNode(new PipelineNode("Node1", tool));

        var executor = new PipelineExecutor();

        var records = new List<InspectionRecord>
        {
            new()
            {
                SerialNumber = "SN-A",
                ImagePath = sampleImg,
                Verdict = InspectionVerdict.Pass,
                Measurements = new() { ["Node1.MeanBrightness"] = 120.0 }
            }
        };

        var replayEngine = new OfflineReplayEngine();
        var report = await replayEngine.ReplayTraceBatchAsync(executor, graph, records);

        report.TotalReplayed.Should().Be(1);
        report.MatchingVerdicts.Should().Be(1);
        report.RegressionsDetected.Should().Be(0);
        report.AgreementRate.Should().Be(1.0);
    }
}
