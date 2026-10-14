using OpenCvSharp;
using VisionStudio.DeepLearning.Datasets;

namespace VisionStudio.Training.Tests;

public class DatasetManagerTests : IDisposable
{
    private readonly string _testDir;

    public DatasetManagerTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "VS_DatasetTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, true);
            }
        }
        catch { }
    }

    [Fact]
    public void ImportFromFolder_IdentifiesClassesAndItems()
    {
        // Setup class subfolders
        CreateDummyImage(Path.Combine(_testDir, "classA", "img1.png"), 100);
        CreateDummyImage(Path.Combine(_testDir, "classA", "img2.png"), 150);
        CreateDummyImage(Path.Combine(_testDir, "classB", "img3.png"), 200);

        var mgr = new DatasetManager();
        int imported = mgr.ImportFromFolder(_testDir);

        imported.Should().Be(3);
        mgr.Items.Count.Should().Be(3);
        mgr.GetClasses().Should().BeEquivalentTo(new[] { "classA", "classB" });

        var stats = mgr.GetClassDistribution();
        stats.First(x => x.Label == "classA").TotalCount.Should().Be(2);
        stats.First(x => x.Label == "classB").TotalCount.Should().Be(1);
    }

    [Fact]
    public void FindDuplicates_DetectsIdenticalFilesBySha256()
    {
        // Two identical files with value 128
        CreateDummyImage(Path.Combine(_testDir, "good", "orig.png"), 128);
        CreateDummyImage(Path.Combine(_testDir, "good", "dup.png"), 128);
        // Different file
        CreateDummyImage(Path.Combine(_testDir, "good", "other.png"), 250);

        var mgr = new DatasetManager();
        mgr.ImportFromFolder(_testDir);

        var dupes = mgr.FindDuplicates();
        dupes.Should().HaveCount(1);
        dupes[0].Count.Should().Be(2);
    }

    [Fact]
    public void SplitStratified_PreservesClassRatiosAcrossSplits()
    {
        // Create 20 items for classA and 20 for classB
        for (int i = 0; i < 20; i++)
        {
            CreateDummyImage(Path.Combine(_testDir, "pass", $"img_{i}.png"), (byte)(i * 5));
            CreateDummyImage(Path.Combine(_testDir, "fail", $"img_{i}.png"), (byte)(100 + i * 5));
        }

        var mgr = new DatasetManager();
        mgr.ImportFromFolder(_testDir);
        mgr.Items.Count.Should().Be(40);

        mgr.SplitStratified(trainRatio: 0.70, valRatio: 0.15, testRatio: 0.15, seed: 42);

        var passItems = mgr.Items.Where(x => x.PrimaryLabel == "pass").ToList();
        var failItems = mgr.Items.Where(x => x.PrimaryLabel == "fail").ToList();

        // 70% of 20 = 14 train
        passItems.Count(x => x.Split == DatasetSplit.Train).Should().Be(14);
        failItems.Count(x => x.Split == DatasetSplit.Train).Should().Be(14);

        // 15% of 20 = 3 val
        passItems.Count(x => x.Split == DatasetSplit.Validation).Should().Be(3);
        failItems.Count(x => x.Split == DatasetSplit.Validation).Should().Be(3);

        // remaining = 3 test
        passItems.Count(x => x.Split == DatasetSplit.Test).Should().Be(3);
        failItems.Count(x => x.Split == DatasetSplit.Test).Should().Be(3);
    }

    [Fact]
    public void ComputeDatasetVersionHash_IsDeterministicAndMatchesContent()
    {
        CreateDummyImage(Path.Combine(_testDir, "c1", "a.png"), 50);
        CreateDummyImage(Path.Combine(_testDir, "c1", "b.png"), 150);

        var mgr = new DatasetManager();
        mgr.ImportFromFolder(_testDir);

        string hash1 = mgr.ComputeDatasetVersionHash();
        string hash2 = mgr.ComputeDatasetVersionHash();

        hash1.Should().NotBeNullOrEmpty();
        hash1.Should().Be(hash2);

        // Altering an annotation or split changes the hash
        mgr.Items[0].Split = DatasetSplit.Test;
        string hash3 = mgr.ComputeDatasetVersionHash();
        hash3.Should().NotBe(hash1);
    }

    private static void CreateDummyImage(string path, byte fillValue)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var mat = new Mat(32, 32, MatType.CV_8UC1, new Scalar(fillValue));
        Cv2.ImWrite(path, mat);
    }
}
