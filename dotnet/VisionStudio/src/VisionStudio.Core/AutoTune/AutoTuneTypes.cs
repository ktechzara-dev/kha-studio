using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Parameters;

namespace VisionStudio.Core.AutoTune;

/// <summary>
/// A labeled training sample for parameter auto-tuning.
/// </summary>
public sealed class AutoTuneSample
{
    public Mat Image { get; }
    public bool IsGood { get; }
    public string Name { get; }
    public ImageFrame Frame { get; }

    public AutoTuneSample(Mat image, bool isGood, string name = "")
    {
        Image = image ?? throw new ArgumentNullException(nameof(image));
        IsGood = isGood;
        Name = string.IsNullOrEmpty(name) ? (isGood ? "Sample_OK" : "Sample_NG") : name;
        Frame = new ImageFrame(image);
    }
}

/// <summary>
/// Search boundary for a tunable parameter.
/// </summary>
public sealed class AutoTuneParameterSpec
{
    public string ParameterName { get; }
    public double MinValue { get; }
    public double MaxValue { get; }
    public double Step { get; }
    public ParameterType Type { get; }

    public AutoTuneParameterSpec(string parameterName, double minValue, double maxValue, double step, ParameterType type = ParameterType.Double)
    {
        ParameterName = parameterName;
        MinValue = minValue;
        MaxValue = maxValue;
        Step = step <= 0 ? (maxValue - minValue) / 10.0 : step;
        Type = type;
    }
}

/// <summary>
/// Metric extraction definition for evaluating candidate parameters.
/// </summary>
public sealed class AutoTuneMetricSpec
{
    public string OutputPortName { get; set; } = "Score";
    public string InputImagePortName { get; set; } = "ImageIn";
    public bool HigherIsBetterForGood { get; set; } = true;

    public AutoTuneMetricSpec(string outputPortName = "Score", string inputImagePortName = "ImageIn", bool higherIsBetter = true)
    {
        OutputPortName = outputPortName;
        InputImagePortName = inputImagePortName;
        HigherIsBetterForGood = higherIsBetter;
    }
}

/// <summary>
/// Detailed evaluation result for a single sample under chosen parameters.
/// </summary>
public sealed record SampleEvaluationResult(
    string SampleName,
    bool GroundTruthIsGood,
    double MetricValue,
    bool PredictedIsGood,
    bool IsCorrect);

/// <summary>
/// Comprehensive before vs after report from an auto-tuning optimization run.
/// </summary>
public sealed class AutoTuneReport
{
    public Dictionary<string, object?> InitialParameters { get; set; } = new();
    public Dictionary<string, object?> TunedParameters { get; set; } = new();

    public double InitialSeparationScore { get; set; }
    public double TunedSeparationScore { get; set; }

    public double InitialSeparationMargin { get; set; }
    public double TunedSeparationMargin { get; set; }

    public double RecommendedThreshold { get; set; }
    public double SafetyMargin { get; set; }

    public int TruePositives { get; set; }
    public int FalsePositives { get; set; }
    public int TrueNegatives { get; set; }
    public int FalseNegatives { get; set; }

    public double Accuracy => (TruePositives + TrueNegatives + FalsePositives + FalseNegatives) > 0
        ? (double)(TruePositives + TrueNegatives) / (TruePositives + TrueNegatives + FalsePositives + FalseNegatives)
        : 0;

    public double Precision => (TruePositives + FalsePositives) > 0
        ? (double)TruePositives / (TruePositives + FalsePositives)
        : 0;

    public double Recall => (TruePositives + FalseNegatives) > 0
        ? (double)TruePositives / (TruePositives + FalseNegatives)
        : 0;

    public double F1Score => (Precision + Recall) > 0
        ? 2.0 * (Precision * Recall) / (Precision + Recall)
        : 0;

    public List<SampleEvaluationResult> SampleDetails { get; set; } = new();

    public string Summary { get; set; } = string.Empty;
}
