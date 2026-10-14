using VisionStudio.DeepLearning.Model;

namespace VisionStudio.DeepLearning.Tests;

public sealed class ModelPackageTests
{
    [Fact]
    public void ModelPackage_SavesAndLoadsVsModelArchive_Accurately()
    {
        var meta = new ModelMetadata
        {
            ModelName = "DefectInspectionResNet",
            TaskType = DeepLearningTaskType.Classification,
            Version = "2.1.0",
            InputName = "images",
            InputWidth = 320,
            InputHeight = 320,
            ClassLabels = new List<string> { "OK", "Dent", "Scratch", "Burr" },
            DefaultConfidenceThreshold = 0.85f,
            DatasetHash = "sha256:abcd1234ef5678",
            AurocScore = 0.985,
            F1Score = 0.942
        };

        byte[] fakeOnnx = new byte[] { 0x08, 0x07, 0x12, 0x0C, 0x56, 0x69, 0x73, 0x69, 0x6F, 0x6E, 0x53, 0x74, 0x75, 0x64 };

        string tempPath = Path.Combine(Path.GetTempPath(), $"test_model_{Guid.NewGuid():N}.vsmodel");
        try
        {
            ModelPackage.Save(tempPath, meta, fakeOnnx);
            File.Exists(tempPath).Should().BeTrue();

            var (loadedMeta, loadedBytes) = ModelPackage.Load(tempPath);

            loadedMeta.ModelName.Should().Be("DefectInspectionResNet");
            loadedMeta.TaskType.Should().Be(DeepLearningTaskType.Classification);
            loadedMeta.Version.Should().Be("2.1.0");
            loadedMeta.InputWidth.Should().Be(320);
            loadedMeta.InputHeight.Should().Be(320);
            loadedMeta.ClassLabels.Should().ContainInOrder("OK", "Dent", "Scratch", "Burr");
            loadedMeta.DefaultConfidenceThreshold.Should().Be(0.85f);
            loadedMeta.DatasetHash.Should().Be("sha256:abcd1234ef5678");
            loadedMeta.AurocScore.Should().Be(0.985);
            loadedMeta.F1Score.Should().Be(0.942);

            loadedBytes.Should().Equal(fakeOnnx);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }
}
