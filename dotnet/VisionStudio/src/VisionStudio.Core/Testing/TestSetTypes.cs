using System.Text.Json;
using VisionStudio.Core.Recipes;
using VisionStudio.Core.Traceability;

namespace VisionStudio.Core.Testing;

/// <summary>
/// A single golden verification sample within an automated test set.
/// </summary>
public sealed class TestSetItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ImagePath { get; set; } = string.Empty;
    public InspectionVerdict ExpectedVerdict { get; set; } = InspectionVerdict.Pass;
    public Dictionary<string, MeasurementTolerance> ExpectedMeasurements { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string Description { get; set; } = string.Empty;
    public string ExpectedPartType { get; set; } = string.Empty;
}

/// <summary>
/// A collection of regression test images and expected inspection verdicts
/// used for automated quality gates and CI/CD validation.
/// </summary>
public sealed class TestSet
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Golden Inspection Test Set";
    public string Description { get; set; } = string.Empty;
    public string TargetPipelineVersion { get; set; } = "1.0.0";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<TestSetItem> Items { get; set; } = new();

    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });

    public static TestSet FromJson(string json) => JsonSerializer.Deserialize<TestSet>(json)
        ?? throw new InvalidOperationException("Failed to deserialize TestSet JSON.");

    public void Save(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        File.WriteAllText(filePath, ToJson());
    }

    public static TestSet Load(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Test set file not found: {filePath}", filePath);
        }
        return FromJson(File.ReadAllText(filePath));
    }
}

/// <summary>
/// Detailed evaluation result for an individual test set item.
/// </summary>
public sealed record TestItemResult(
    TestSetItem Item,
    InspectionVerdict ActualVerdict,
    Dictionary<string, double> ActualMeasurements,
    bool VerdictMatches,
    bool TolerancesPassed,
    bool Success,
    string? FailureReason,
    double LatencyMs);

/// <summary>
/// Comprehensive summary report of a test set execution run.
/// </summary>
public sealed class TestSetRunReport
{
    public string TestSetName { get; init; } = string.Empty;
    public int TotalItems { get; init; }
    public int PassedItems { get; init; }
    public int FailedItems { get; init; }
    public double PassRate => TotalItems > 0 ? (double)PassedItems / TotalItems : 1.0;
    public bool AllPassed => FailedItems == 0 && TotalItems > 0;
    public double AverageLatencyMs { get; init; }
    public IReadOnlyList<TestItemResult> Details { get; init; } = Array.Empty<TestItemResult>();
}

/// <summary>
/// Row comparing historical inspection verdict/measurements against new pipeline execution.
/// </summary>
public sealed record ReplayRegressionRow(
    string SerialNumber,
    string ImagePath,
    InspectionVerdict HistoricalVerdict,
    InspectionVerdict NewVerdict,
    bool IsVerdictMatch,
    Dictionary<string, (double HistoricalVal, double NewVal, double Drift)> MeasurementDrifts);

public sealed class ReplayRegressionReport
{
    public int TotalReplayed { get; init; }
    public int MatchingVerdicts { get; init; }
    public int RegressionsDetected { get; init; } // Changed from Pass to Fail or vice versa
    public double AgreementRate => TotalReplayed > 0 ? (double)MatchingVerdicts / TotalReplayed : 1.0;
    public IReadOnlyList<ReplayRegressionRow> Rows { get; init; } = Array.Empty<ReplayRegressionRow>();
}
