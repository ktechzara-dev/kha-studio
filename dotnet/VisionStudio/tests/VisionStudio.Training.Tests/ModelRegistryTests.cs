using VisionStudio.DeepLearning.Model;
using VisionStudio.DeepLearning.Registry;

namespace VisionStudio.Training.Tests;

public class ModelRegistryTests : IDisposable
{
    private readonly string _regDir;

    public ModelRegistryTests()
    {
        _regDir = Path.Combine(Path.GetTempPath(), "VS_RegistryTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_regDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_regDir))
            {
                Directory.Delete(_regDir, true);
            }
        }
        catch { }
    }

    [Fact]
    public void RegisterModel_SavesEntryAndLoadsPersistently()
    {
        var dummyModel = Path.Combine(_regDir, "temp.vsmodel");
        File.WriteAllText(dummyModel, "mock-vsmodel-content");

        var reg = new ModelRegistry(_regDir);
        var entry = reg.RegisterModel(
            name: "BottleInspector",
            version: "1.0.0",
            taskType: DeepLearningTaskType.AnomalyDetection,
            vsModelFilePath: dummyModel,
            author: "QA",
            datasetHash: "hash123",
            auroc: 0.96,
            f1: 0.94,
            p50: 12.5);

        entry.ModelId.Should().NotBeNullOrEmpty();
        entry.Name.Should().Be("BottleInspector");
        entry.Version.Should().Be("1.0.0");
        entry.Auroc.Should().Be(0.96);

        // Reload fresh registry from same folder
        var reloaded = new ModelRegistry(_regDir);
        reloaded.Entries.Should().HaveCount(1);
        reloaded.Entries[0].Name.Should().Be("BottleInspector");
    }

    [Fact]
    public void PromoteToProduction_AndRollback_UpdatesFlagsCorrectly()
    {
        var dummyModel = Path.Combine(_regDir, "temp.vsmodel");
        File.WriteAllText(dummyModel, "mock");

        var reg = new ModelRegistry(_regDir);
        var v1 = reg.RegisterModel("CapClassifier", "1.0.0", DeepLearningTaskType.Classification, dummyModel, f1: 0.88);
        var v2 = reg.RegisterModel("CapClassifier", "2.0.0", DeepLearningTaskType.Classification, dummyModel, f1: 0.95);

        // Promote v2
        reg.PromoteToProduction(v2.ModelId);

        reg.GetProductionModel("CapClassifier")!.Version.Should().Be("2.0.0");
        reg.Entries.First(m => m.Version == "1.0.0").IsProduction.Should().BeFalse();
        reg.Entries.First(m => m.Version == "2.0.0").IsProduction.Should().BeTrue();

        // Rollback to v1
        reg.RollbackProduction("CapClassifier", "1.0.0");

        reg.GetProductionModel("CapClassifier")!.Version.Should().Be("1.0.0");
        reg.Entries.First(m => m.Version == "1.0.0").IsProduction.Should().BeTrue();
        reg.Entries.First(m => m.Version == "2.0.0").IsProduction.Should().BeFalse();
    }

    [Fact]
    public void CompareModels_ProducesSideBySideComparisonRows()
    {
        var dummyModel = Path.Combine(_regDir, "temp.vsmodel");
        File.WriteAllText(dummyModel, "mock");

        var reg = new ModelRegistry(_regDir);
        var v1 = reg.RegisterModel("OcrReader", "1.0.0", DeepLearningTaskType.DeepOcr, dummyModel, f1: 0.85, p50: 30.0);
        var v2 = reg.RegisterModel("OcrReader", "2.0.0", DeepLearningTaskType.DeepOcr, dummyModel, f1: 0.92, p50: 18.0);

        var rows = reg.CompareModels(v1, v2);

        rows.Should().NotBeEmpty();
        var f1Row = rows.First(r => r.Property.Contains("F1"));
        f1Row.Winner.Should().Be("Mô hình B"); // v2 has higher F1
        var latencyRow = rows.First(r => r.Property.Contains("Latency"));
        latencyRow.Winner.Should().Be("Mô hình B"); // v2 has lower latency
    }
}
