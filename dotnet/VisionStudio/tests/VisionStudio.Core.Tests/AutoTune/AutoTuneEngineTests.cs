using OpenCvSharp;
using VisionStudio.Core.AutoTune;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Core.Tests.AutoTune;

/// <summary>
/// Simple vision tool for auto-tuning unit tests.
/// Computes normalized pixel percentage above a tunable Threshold parameter.
/// </summary>
public sealed class SyntheticInspectorTool : VisionToolBase
{
    public const string ParamThreshold = "Threshold";
    public const string PortInImage = "ImageIn";
    public const string PortOutScore = "Score";

    private readonly ToolParameter _pThreshold;

    public SyntheticInspectorTool(double initialThreshold = 50.0)
        : base("Test.SyntheticInspector", "Synthetic Inspector", "Testing")
    {
        _pThreshold = AddParameter(new ToolParameter(ParamThreshold, "Threshold", ParameterType.Double, initialThreshold, min: 0.0, max: 255.0, step: 10.0));
        AddInput(new ToolPort(PortInImage, "Image In", PortType.Image, PortDirection.Input));
        AddOutput(new ToolPort(PortOutScore, "Score", PortType.Number, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var frame = ctx.GetImage(PortInImage);
        if (frame == null) return ToolResult.Failed("Missing image.");

        double thresh = _pThreshold.GetValue<double>();
        using var gray = frame.Mat.Channels() > 1 ? frame.Mat.CvtColor(ColorConversionCodes.BGR2GRAY) : frame.Mat.Clone();
        using var bin = new Mat();
        Cv2.Threshold(gray, bin, thresh, 255, ThresholdTypes.Binary);

        int nonZero = Cv2.CountNonZero(bin);
        double ratio = (double)nonZero / (gray.Width * gray.Height);

        return ToolResult.Ok(new Dictionary<string, object?>
        {
            [PortOutScore] = ratio
        });
    }
}

public sealed class AutoTuneEngineTests
{
    [Fact]
    public void AutoTune_FindsOptimalParameter_And_AchievesFullSeparation()
    {
        // 1. Prepare synthetic dataset:
        // Good images: contain a prominent bright disk (pixels at 220) -> high ratio when threshold is balanced
        // Bad images: background noise only (pixels around 30-70)
        var samples = new List<AutoTuneSample>();

        // 3 Good samples
        for (int i = 0; i < 3; i++)
        {
            var mat = new Mat(100, 100, MatType.CV_8UC1, Scalar.All(20));
            Cv2.Circle(mat, new Point(50, 50), 30, Scalar.All(220), -1);
            samples.Add(new AutoTuneSample(mat, isGood: true, name: $"Good_{i}"));
        }

        // 3 Bad samples (noise/defect with values 40, 60, 80)
        for (int i = 0; i < 3; i++)
        {
            var mat = new Mat(100, 100, MatType.CV_8UC1, Scalar.All(40 + i * 20));
            samples.Add(new AutoTuneSample(mat, isGood: false, name: $"Bad_{i}"));
        }

        // 2. Initialize tool with poor initial parameter
        var tool = new SyntheticInspectorTool(initialThreshold: 20.0);

        // 3. Define tuning search bounds
        var paramSpecs = new List<AutoTuneParameterSpec>
        {
            new(SyntheticInspectorTool.ParamThreshold, minValue: 10.0, maxValue: 180.0, step: 20.0, ParameterType.Double)
        };

        var metricSpec = new AutoTuneMetricSpec(
            outputPortName: SyntheticInspectorTool.PortOutScore,
            inputImagePortName: SyntheticInspectorTool.PortInImage,
            higherIsBetter: true);

        // 4. Run AutoTune
        var engine = new AutoTuneEngine();
        var report = engine.Optimize(tool, samples, paramSpecs, metricSpec);

        // 5. Assertions
        report.Accuracy.Should().Be(1.0);
        report.F1Score.Should().Be(1.0);
        report.TunedSeparationMargin.Should().BeGreaterThan(0.0);
        report.RecommendedThreshold.Should().BeGreaterThan(0.0);

        // Tool's tuned threshold should have shifted away from initial 20.0 to separate 220 vs 60
        double tunedThreshold = Convert.ToDouble(report.TunedParameters[SyntheticInspectorTool.ParamThreshold]);
        tunedThreshold.Should().BeGreaterThanOrEqualTo(70.0);

        report.Summary.Should().Contain("AUTO-TUNE REPORT");
        report.Summary.Should().Contain("(Accuracy): 100.0%");

        // Clean up mats
        foreach (var s in samples)
        {
            s.Image.Dispose();
        }
    }
}
