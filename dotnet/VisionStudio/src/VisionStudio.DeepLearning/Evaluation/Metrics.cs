namespace VisionStudio.DeepLearning.Evaluation;

/// <summary>
/// Evaluation metrics for multiclass image classification.
/// </summary>
public sealed class ClassificationMetrics
{
    public IReadOnlyList<string> ClassNames { get; }
    public int[,] ConfusionMatrix { get; } // [Actual, Predicted]
    public double Accuracy { get; }
    public double MacroPrecision { get; }
    public double MacroRecall { get; }
    public double MacroF1 { get; }
    public Dictionary<string, (double Precision, double Recall, double F1)> PerClassMetrics { get; }

    public ClassificationMetrics(
        IReadOnlyList<string> classNames,
        int[,] confusionMatrix,
        double accuracy,
        double macroPrecision,
        double macroRecall,
        double macroF1,
        Dictionary<string, (double Precision, double Recall, double F1)> perClassMetrics)
    {
        ClassNames = classNames;
        ConfusionMatrix = confusionMatrix;
        Accuracy = accuracy;
        MacroPrecision = macroPrecision;
        MacroRecall = macroRecall;
        MacroF1 = macroF1;
        PerClassMetrics = perClassMetrics;
    }

    public static ClassificationMetrics Compute(
        IReadOnlyList<string> classNames,
        IReadOnlyList<string> groundTruth,
        IReadOnlyList<string> predictions)
    {
        int n = classNames.Count;
        var nameMap = classNames.Select((name, idx) => (name, idx)).ToDictionary(x => x.name, x => x.idx, StringComparer.OrdinalIgnoreCase);
        int[,] matrix = new int[n, n];

        int total = Math.Min(groundTruth.Count, predictions.Count);
        int correct = 0;

        for (int i = 0; i < total; i++)
        {
            if (nameMap.TryGetValue(groundTruth[i], out int actualIdx) &&
                nameMap.TryGetValue(predictions[i], out int predIdx))
            {
                matrix[actualIdx, predIdx]++;
                if (actualIdx == predIdx) correct++;
            }
        }

        double accuracy = total > 0 ? (double)correct / total : 0.0;
        var perClass = new Dictionary<string, (double, double, double)>(StringComparer.OrdinalIgnoreCase);

        double sumP = 0, sumR = 0, sumF1 = 0;

        for (int c = 0; c < n; c++)
        {
            int tp = matrix[c, c];
            int fn = 0;
            int fp = 0;

            for (int j = 0; j < n; j++)
            {
                if (j != c)
                {
                    fn += matrix[c, j];
                    fp += matrix[j, c];
                }
            }

            double p = (tp + fp) > 0 ? (double)tp / (tp + fp) : 0.0;
            double r = (tp + fn) > 0 ? (double)tp / (tp + fn) : 0.0;
            double f1 = (p + r) > 0 ? 2 * (p * r) / (p + r) : 0.0;

            perClass[classNames[c]] = (Math.Round(p, 4), Math.Round(r, 4), Math.Round(f1, 4));
            sumP += p;
            sumR += r;
            sumF1 += f1;
        }

        return new ClassificationMetrics(
            classNames,
            matrix,
            Math.Round(accuracy, 4),
            Math.Round(sumP / n, 4),
            Math.Round(sumR / n, 4),
            Math.Round(sumF1 / n, 4),
            perClass);
    }
}

/// <summary>
/// Point on a Receiver Operating Characteristic (ROC) curve.
/// </summary>
public sealed record RocPoint(double FalsePositiveRate, double TruePositiveRate, double Threshold);

/// <summary>
/// Evaluation metrics for unsupervised anomaly detection.
/// </summary>
public sealed class AnomalyMetrics
{
    public double Auroc { get; }
    public IReadOnlyList<RocPoint> RocCurve { get; }
    public double OptimalThreshold { get; }
    public double OptimalF1 { get; }

    public AnomalyMetrics(double auroc, IReadOnlyList<RocPoint> rocCurve, double optimalThreshold, double optimalF1)
    {
        Auroc = auroc;
        RocCurve = rocCurve;
        OptimalThreshold = optimalThreshold;
        OptimalF1 = optimalF1;
    }

    /// <summary>
    /// Computes Area Under the ROC Curve (AUROC) and optimal threshold from binary ground truth and continuous anomaly scores.
    /// </summary>
    public static AnomalyMetrics Compute(IReadOnlyList<bool> isAnomalyGroundTruth, IReadOnlyList<double> anomalyScores)
    {
        if (isAnomalyGroundTruth.Count == 0 || anomalyScores.Count == 0)
        {
            return new AnomalyMetrics(0.5, Array.Empty<RocPoint>(), 0.5, 0.0);
        }

        int totalP = isAnomalyGroundTruth.Count(x => x);
        int totalN = isAnomalyGroundTruth.Count(x => !x);

        if (totalP == 0 || totalN == 0)
        {
            return new AnomalyMetrics(1.0, Array.Empty<RocPoint>(), 0.5, 1.0);
        }

        // Pair up and sort by descending score
        var pairs = isAnomalyGroundTruth.Zip(anomalyScores, (truth, score) => (truth, score))
            .OrderByDescending(p => p.score)
            .ToList();

        var rocPoints = new List<RocPoint>();
        int tp = 0;
        int fp = 0;

        rocPoints.Add(new RocPoint(0.0, 0.0, 1.0));

        double bestF1 = 0.0;
        double bestThreshold = 0.5;

        for (int i = 0; i < pairs.Count; i++)
        {
            if (pairs[i].truth) tp++;
            else fp++;

            double tpr = (double)tp / totalP;
            double fpr = (double)fp / totalN;
            double threshold = pairs[i].score;

            rocPoints.Add(new RocPoint(fpr, tpr, threshold));

            // Compute F1 at this threshold
            int fn = totalP - tp;
            double precision = (tp + fp) > 0 ? (double)tp / (tp + fp) : 0.0;
            double recall = tpr;
            double f1 = (precision + recall) > 0 ? 2 * (precision * recall) / (precision + recall) : 0.0;

            if (f1 > bestF1)
            {
                bestF1 = f1;
                bestThreshold = threshold;
            }
        }

        // Integrate ROC curve via Trapezoidal rule to obtain AUROC
        double auroc = 0.0;
        for (int i = 1; i < rocPoints.Count; i++)
        {
            double dx = rocPoints[i].FalsePositiveRate - rocPoints[i - 1].FalsePositiveRate;
            double avgY = (rocPoints[i].TruePositiveRate + rocPoints[i - 1].TruePositiveRate) / 2.0;
            auroc += dx * avgY;
        }

        return new AnomalyMetrics(
            Math.Round(Math.Clamp(auroc, 0.0, 1.0), 4),
            rocPoints,
            Math.Round(bestThreshold, 4),
            Math.Round(bestF1, 4));
    }
}
