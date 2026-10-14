using VisionStudio.DeepLearning.Evaluation;

namespace VisionStudio.Training.Tests;

public class MetricsTests
{
    [Fact]
    public void ClassificationMetrics_ComputesAccurateConfusionMatrixAndF1()
    {
        var classes = new List<string> { "Cat", "Dog" };
        var actual = new List<string> { "Cat", "Cat", "Dog", "Dog" };
        var preds = new List<string> { "Cat", "Dog", "Dog", "Dog" };

        var metrics = ClassificationMetrics.Compute(classes, actual, preds);

        metrics.Accuracy.Should().Be(0.75); // 3 out of 4 correct
        // Cat: TP=1, FP=0, FN=1 -> Prec = 1.0, Rec = 0.5, F1 = 2/3 = 0.6667
        // Dog: TP=2, FP=1, FN=0 -> Prec = 2/3 = 0.6667, Rec = 1.0, F1 = 0.8
        metrics.PerClassMetrics["Cat"].Precision.Should().BeApproximately(1.0, 0.01);
        metrics.PerClassMetrics["Cat"].Recall.Should().BeApproximately(0.5, 0.01);
        metrics.PerClassMetrics["Dog"].Precision.Should().BeApproximately(0.667, 0.01);
        metrics.PerClassMetrics["Dog"].Recall.Should().BeApproximately(1.0, 0.01);

        metrics.ConfusionMatrix[0, 0].Should().Be(1); // Cat predicted Cat
        metrics.ConfusionMatrix[0, 1].Should().Be(1); // Cat predicted Dog
        metrics.ConfusionMatrix[1, 0].Should().Be(0); // Dog predicted Cat
        metrics.ConfusionMatrix[1, 1].Should().Be(2); // Dog predicted Dog
    }

    [Fact]
    public void AnomalyMetrics_ComputesAurocWithTrapezoidalIntegration()
    {
        // Perfect separator: all anomalies have high scores, normal have low scores
        var groundTruthAnomalies = new List<bool> { false, false, false, true, true, true };
        var scores = new List<double> { 0.1, 0.2, 0.15, 0.8, 0.85, 0.95 };

        var metrics = AnomalyMetrics.Compute(groundTruthAnomalies, scores);

        // AUROC should be 1.0 for perfect ranking
        metrics.Auroc.Should().BeApproximately(1.0, 0.01);
        metrics.OptimalF1.Should().BeApproximately(1.0, 0.01);
        metrics.RocCurve.Should().NotBeEmpty();
    }

    [Fact]
    public void AnomalyMetrics_HandlesImperfectScoresSensibly()
    {
        var groundTruthAnomalies = new List<bool> { false, false, true, true };
        var scores = new List<double> { 0.1, 0.6, 0.4, 0.9 }; // One false positive inversion

        var metrics = AnomalyMetrics.Compute(groundTruthAnomalies, scores);

        metrics.Auroc.Should().BeGreaterThan(0.5);
        metrics.Auroc.Should().BeLessThan(1.0);
        metrics.OptimalThreshold.Should().BeInRange(0.1, 0.9);
    }
}
