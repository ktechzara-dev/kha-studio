using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Tools;

namespace VisionStudio.Core.AutoTune;

/// <summary>
/// Autonomous parameter tuning engine that explores tool parameter spaces against labeled Good/Bad images,
/// maximizing separation margins and synthesizing before/after reports.
/// </summary>
public sealed class AutoTuneEngine
{
    private readonly ILogger? _logger;

    public AutoTuneEngine(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Executes the auto-tuning search loop on a given vision tool.
    /// </summary>
    public AutoTuneReport Optimize(
        IVisionTool tool,
        IReadOnlyList<AutoTuneSample> samples,
        IReadOnlyList<AutoTuneParameterSpec> parameterSpecs,
        AutoTuneMetricSpec metricSpec,
        CancellationToken ct = default)
    {
        if (samples.Count < 2)
        {
            throw new ArgumentException("At least 2 samples (at least one Good and one Bad) are required for auto-tuning.", nameof(samples));
        }

        var goodSamples = samples.Where(s => s.IsGood).ToList();
        var badSamples = samples.Where(s => !s.IsGood).ToList();

        if (goodSamples.Count == 0 || badSamples.Count == 0)
        {
            throw new ArgumentException("Both Good and Bad samples must be provided for discriminative auto-tuning.", nameof(samples));
        }

        // 1. Record initial parameter values
        var initialParams = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in tool.Parameters)
        {
            initialParams[p.Name] = p.Value;
        }

        // 2. Evaluate baseline performance under initial parameters
        var initialEval = EvaluateTool(tool, samples, metricSpec, ct);

        // 3. Generate candidate grid combinations
        var candidateGrid = GenerateCandidateCombinations(parameterSpecs);
        _logger?.LogInformation("AutoTune: Generated {Count} parameter combinations to evaluate.", candidateGrid.Count);

        double bestScore = -1e9;
        Dictionary<string, object?> bestParams = new(initialParams, StringComparer.OrdinalIgnoreCase);
        EvaluationResult bestEval = initialEval;

        foreach (var candidate in candidateGrid)
        {
            ct.ThrowIfCancellationRequested();

            // Apply candidate parameters to tool
            ApplyParameters(tool, candidate);

            // Evaluate on all samples
            var eval = EvaluateTool(tool, samples, metricSpec, ct);

            // Objective function: Prioritize F1 score and positive separation margin
            double objective = eval.F1 * 1000.0;
            if (eval.Margin > 0)
            {
                objective += 500.0 + eval.Margin * 100.0;
            }
            else
            {
                objective += eval.Margin * 100.0; // penalize negative margin
            }
            objective += eval.FisherSeparation;

            if (objective > bestScore)
            {
                bestScore = objective;
                bestEval = eval;
                bestParams = candidate.ToDictionary(k => k.Key, v => v.Value, StringComparer.OrdinalIgnoreCase);
            }
        }

        // 4. Apply optimal parameters back to tool
        ApplyParameters(tool, bestParams);

        // 5. Construct comprehensive report
        var report = new AutoTuneReport
        {
            InitialParameters = initialParams,
            TunedParameters = bestParams,
            InitialSeparationScore = initialEval.FisherSeparation,
            TunedSeparationScore = bestEval.FisherSeparation,
            InitialSeparationMargin = initialEval.Margin,
            TunedSeparationMargin = bestEval.Margin,
            RecommendedThreshold = bestEval.OptimalThreshold,
            SafetyMargin = bestEval.SafetyMargin,
            TruePositives = bestEval.TruePositives,
            FalsePositives = bestEval.FalsePositives,
            TrueNegatives = bestEval.TrueNegatives,
            FalseNegatives = bestEval.FalseNegatives,
            SampleDetails = bestEval.Details
        };

        report.Summary = BuildSummary(report, metricSpec);
        return report;
    }

    private static EvaluationResult EvaluateTool(
        IVisionTool tool,
        IReadOnlyList<AutoTuneSample> samples,
        AutoTuneMetricSpec metricSpec,
        CancellationToken ct)
    {
        var goods = new List<double>();
        var bads = new List<double>();
        var sampleVals = new List<(AutoTuneSample Sample, double Val)>();

        foreach (var s in samples)
        {
            ct.ThrowIfCancellationRequested();

            var inputs = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                [metricSpec.InputImagePortName] = s.Frame
            };

            var ctx = new ToolContext(inputs, ct);
            var result = tool.Execute(ctx, ct);

            double metricVal = 0;
            if (result.Outputs.TryGetValue(metricSpec.OutputPortName, out var v) && v != null)
            {
                if (v is double d) metricVal = d;
                else if (v is float f) metricVal = f;
                else if (v is int i) metricVal = i;
                else if (v is bool b) metricVal = b ? 1.0 : 0.0;
                else double.TryParse(v.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out metricVal);
            }

            sampleVals.Add((s, metricVal));

            if (s.IsGood)
            {
                goods.Add(metricVal);
            }
            else
            {
                bads.Add(metricVal);
            }
        }

        double goodMean = goods.Average();
        double badMean = bads.Average();
        double goodStd = CalculateStd(goods);
        double badStd = CalculateStd(bads);

        double signedDiff = metricSpec.HigherIsBetterForGood
            ? (goodMean - badMean)
            : (badMean - goodMean);

        double denom = goodStd + badStd + 0.001;
        double fisher = signedDiff > 0 ? (signedDiff / denom) : 0.0;

        double margin;
        double optimalThreshold;

        if (metricSpec.HigherIsBetterForGood)
        {
            margin = goods.Min() - bads.Max();
            optimalThreshold = (goods.Min() + bads.Max()) / 2.0;
        }
        else
        {
            margin = bads.Min() - goods.Max();
            optimalThreshold = (goods.Max() + bads.Min()) / 2.0;
        }

        double safetyMargin = Math.Max(0, Math.Abs(margin) / 2.0);

        // Classification stats at optimalThreshold
        int tp = 0, fp = 0, tn = 0, fn = 0;
        var details = new List<SampleEvaluationResult>();

        foreach (var (sample, val) in sampleVals)
        {
            bool predictedGood = metricSpec.HigherIsBetterForGood ? val >= optimalThreshold : val <= optimalThreshold;
            bool correct = predictedGood == sample.IsGood;

            if (sample.IsGood && predictedGood) tp++;
            else if (!sample.IsGood && predictedGood) fp++;
            else if (!sample.IsGood && !predictedGood) tn++;
            else if (sample.IsGood && !predictedGood) fn++;

            details.Add(new SampleEvaluationResult(sample.Name, sample.IsGood, val, predictedGood, correct));
        }

        double prec = (tp + fp) > 0 ? (double)tp / (tp + fp) : 0;
        double rec = (tp + fn) > 0 ? (double)tp / (tp + fn) : 0;
        double f1 = (prec + rec) > 0 ? 2.0 * (prec * rec) / (prec + rec) : 0;

        return new EvaluationResult(
            FisherSeparation: fisher,
            Margin: margin,
            OptimalThreshold: optimalThreshold,
            SafetyMargin: safetyMargin,
            F1: f1,
            TruePositives: tp,
            FalsePositives: fp,
            TrueNegatives: tn,
            FalseNegatives: fn,
            Details: details);
    }

    private static double CalculateStd(List<double> vals)
    {
        if (vals.Count < 2) return 0.0;
        double avg = vals.Average();
        double sumSq = vals.Sum(v => (v - avg) * (v - avg));
        return Math.Sqrt(sumSq / (vals.Count - 1));
    }

    private static List<Dictionary<string, object?>> GenerateCandidateCombinations(IReadOnlyList<AutoTuneParameterSpec> specs)
    {
        var result = new List<Dictionary<string, object?>>();
        if (specs.Count == 0) return result;

        // Generate values per parameter
        var paramValues = new List<(string Name, ParameterType Type, List<object> Values)>();
        foreach (var s in specs)
        {
            var vals = new List<object>();
            for (double v = s.MinValue; v <= s.MaxValue + 1e-9; v += s.Step)
            {
                if (s.Type == ParameterType.Integer)
                {
                    vals.Add((int)Math.Round(v));
                }
                else
                {
                    vals.Add(v);
                }
            }
            if (vals.Count == 0) vals.Add(s.MinValue);
            paramValues.Add((s.ParameterName, s.Type, vals.Distinct().ToList()));
        }

        // Cartesian product
        void Backtrack(int index, Dictionary<string, object?> current)
        {
            if (index == paramValues.Count)
            {
                result.Add(new Dictionary<string, object?>(current, StringComparer.OrdinalIgnoreCase));
                return;
            }

            var (pName, _, vals) = paramValues[index];
            foreach (var val in vals)
            {
                current[pName] = val;
                Backtrack(index + 1, current);
            }
        }

        Backtrack(0, new Dictionary<string, object?>());
        return result;
    }

    private static void ApplyParameters(IVisionTool tool, Dictionary<string, object?> parameters)
    {
        foreach (var (pName, val) in parameters)
        {
            var target = tool.Parameters.FirstOrDefault(p => p.Name.Equals(pName, StringComparison.OrdinalIgnoreCase));
            if (target != null)
            {
                target.Value = val;
            }
        }
    }

    private static string BuildSummary(AutoTuneReport report, AutoTuneMetricSpec metricSpec)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== BÁO CÁO TỐI ƯU HÓA TỰ ĐỘNG (AUTO-TUNE REPORT) ===");
        sb.AppendLine($"• Thước đo đánh giá: {metricSpec.OutputPortName}");
        sb.AppendLine($"• Độ chính xác (Accuracy): {report.Accuracy * 100:F1}%");
        sb.AppendLine($"• F1-Score: {report.F1Score * 100:F1}%");
        sb.AppendLine($"• Khoảng cách tách biệt ban đầu (Initial Margin): {report.InitialSeparationMargin:F2}");
        sb.AppendLine($"• Khoảng cách tách biệt tối ưu (Tuned Margin): {report.TunedSeparationMargin:F2}");
        sb.AppendLine($"• Ngưỡng phân loại đề xuất (Recommended Threshold): {report.RecommendedThreshold:F2} (Biên an toàn: ±{report.SafetyMargin:F2})");
        sb.AppendLine($"• Ma trận nhầm lẫn: TP={report.TruePositives}, FP={report.FalsePositives}, TN={report.TrueNegatives}, FN={report.FalseNegatives}");
        sb.AppendLine();
        sb.AppendLine("--- THAY ĐỔI THAM SỐ (BEFORE vs AFTER) ---");
        foreach (var (pName, tunedVal) in report.TunedParameters)
        {
            report.InitialParameters.TryGetValue(pName, out var initVal);
            sb.AppendLine($"• {pName}: {initVal} ➜ {tunedVal}");
        }

        return sb.ToString();
    }

    private sealed record EvaluationResult(
        double FisherSeparation,
        double Margin,
        double OptimalThreshold,
        double SafetyMargin,
        double F1,
        int TruePositives,
        int FalsePositives,
        int TrueNegatives,
        int FalseNegatives,
        List<SampleEvaluationResult> Details);
}
