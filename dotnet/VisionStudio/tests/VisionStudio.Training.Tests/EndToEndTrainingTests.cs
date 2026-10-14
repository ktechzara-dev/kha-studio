using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.DeepLearning.Datasets;
using VisionStudio.DeepLearning.Evaluation;
using VisionStudio.DeepLearning.Model;
using VisionStudio.DeepLearning.Tools;
using VisionStudio.Core.Tools;
using VisionStudio.Training;

namespace VisionStudio.Training.Tests;

public class EndToEndTrainingTests : IDisposable
{
    private readonly string _workDir;

    public EndToEndTrainingTests()
    {
        _workDir = Path.Combine(Path.GetTempPath(), "VS_E2E_Train_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_workDir))
            {
                Directory.Delete(_workDir, true);
            }
        }
        catch { }
    }

    [Fact]
    public async Task EndToEnd_TrainPatchCoreAnomaly_AchievesHighAuroc_AndInfersInVisionTool()
    {
        string datasetDir = Path.Combine(_workDir, "anomaly_dataset");
        string goodDir = Path.Combine(datasetDir, "good");
        string defectDir = Path.Combine(datasetDir, "defect");
        Directory.CreateDirectory(goodDir);
        Directory.CreateDirectory(defectDir);

        // Generate synthetic "good" samples (smooth gradient / uniform center)
        for (int i = 0; i < 15; i++)
        {
            using var mat = new Mat(64, 64, MatType.CV_8UC1, new Scalar(120 + (i % 5)));
            Cv2.Circle(mat, new Point(32, 32), 16, new Scalar(180), -1);
            Cv2.ImWrite(Path.Combine(goodDir, $"good_{i:D2}.png"), mat);
        }

        // Generate synthetic "defect" samples (same background but corrupted with high-contrast cuts/scratches)
        for (int i = 0; i < 10; i++)
        {
            using var mat = new Mat(64, 64, MatType.CV_8UC1, new Scalar(120 + (i % 5)));
            Cv2.Circle(mat, new Point(32, 32), 16, new Scalar(180), -1);
            // Defect anomaly scratch
            Cv2.Line(mat, new Point(5, 5), new Point(55, 55), new Scalar(10), 3);
            Cv2.Circle(mat, new Point(20, 45), 8, new Scalar(255), -1);
            Cv2.ImWrite(Path.Combine(defectDir, $"defect_{i:D2}.png"), mat);
        }

        // 1. Dataset Management
        var datasetMgr = new DatasetManager();
        int imported = datasetMgr.ImportFromFolder(datasetDir);
        imported.Should().Be(25);

        datasetMgr.SplitStratified(trainRatio: 0.6, valRatio: 0.2, testRatio: 0.2, seed: 42);
        string dsHash = datasetMgr.ComputeDatasetVersionHash();
        dsHash.Should().NotBeNullOrEmpty();

        // 2. Train with TrainingSidecarRunner
        var runner = new TrainingSidecarRunner();
        var config = new TrainingConfig
        {
            ModelName = "IndustrialPatchCoreModel",
            TaskType = DeepLearningTaskType.AnomalyDetection,
            InputWidth = 64,
            InputHeight = 64,
            OutputDir = Path.Combine(_workDir, "models"),
            Seed = 42
        };

        var progressUpdates = new List<TrainingProgressUpdate>();
        var progress = new Progress<TrainingProgressUpdate>(u => progressUpdates.Add(u));

        var result = await runner.RunTrainingAsync(config, datasetMgr, progress: progress);

        // Assert Training Results
        result.Success.Should().BeTrue();
        File.Exists(result.ModelPath).Should().BeTrue();
        result.Metrics.Should().BeOfType<AnomalyMetrics>();

        var anomalyMetrics = (AnomalyMetrics)result.Metrics!;
        anomalyMetrics.Auroc.Should().BeGreaterThanOrEqualTo(0.85, "Acceptance criteria requires AUROC > 0.85 on test set");

        // 3. Inference Verification in DeepAnomalyDetectionTool
        var tool = new DeepAnomalyDetectionTool();
        tool.Parameters.First(p => p.Name == DeepAnomalyDetectionTool.ParamModelPath).Value = result.ModelPath;

        // Test inference on a defect image
        string sampleDefectPath = Path.Combine(defectDir, "defect_00.png");
        using var defectMat = Cv2.ImRead(sampleDefectPath, ImreadModes.Grayscale);
        using var testFrame = new ImageFrame(defectMat, "testCam");

        var ctx = new ToolContext(new Dictionary<string, object?>
        {
            [DeepAnomalyDetectionTool.PortImageIn] = testFrame
        });

        var toolResult = tool.Execute(ctx, CancellationToken.None);

        toolResult.Status.Should().Be(ToolStatus.Ok);
        toolResult.Outputs.Should().ContainKey(DeepAnomalyDetectionTool.PortAnomalyScore);
        toolResult.Outputs.Should().ContainKey(DeepAnomalyDetectionTool.PortHeatmapOut);

        double score = Convert.ToDouble(toolResult.Outputs[DeepAnomalyDetectionTool.PortAnomalyScore]);
        score.Should().BeGreaterThan(0.0);
    }

    [Fact]
    public async Task EndToEnd_TrainClassifier_ProducesValidVsModel()
    {
        string datasetDir = Path.Combine(_workDir, "class_dataset");
        string classADir = Path.Combine(datasetDir, "TypeA");
        string classBDir = Path.Combine(datasetDir, "TypeB");
        Directory.CreateDirectory(classADir);
        Directory.CreateDirectory(classBDir);

        // Class A: dark images
        for (int i = 0; i < 15; i++)
        {
            using var mat = new Mat(32, 32, MatType.CV_8UC1, new Scalar(40 + (i % 10)));
            Cv2.ImWrite(Path.Combine(classADir, $"a_{i:D2}.png"), mat);
        }

        // Class B: bright images
        for (int i = 0; i < 15; i++)
        {
            using var mat = new Mat(32, 32, MatType.CV_8UC1, new Scalar(210 + (i % 10)));
            Cv2.ImWrite(Path.Combine(classBDir, $"b_{i:D2}.png"), mat);
        }

        var datasetMgr = new DatasetManager();
        datasetMgr.ImportFromFolder(datasetDir);
        datasetMgr.SplitStratified(0.7, 0.15, 0.15, seed: 123);

        var runner = new TrainingSidecarRunner();
        var config = new TrainingConfig
        {
            ModelName = "PartTypeClassifier",
            TaskType = DeepLearningTaskType.Classification,
            InputWidth = 32,
            InputHeight = 32,
            Epochs = 15,
            LearningRate = 0.05,
            OutputDir = Path.Combine(_workDir, "models"),
            Seed = 123
        };

        var result = await runner.RunTrainingAsync(config, datasetMgr);

        result.Success.Should().BeTrue();
        result.Metrics.Should().BeOfType<ClassificationMetrics>();

        var classMetrics = (ClassificationMetrics)result.Metrics!;
        classMetrics.Accuracy.Should().BeGreaterThanOrEqualTo(0.85);

        // Ensure package can be read
        var (meta, onnxBytes) = ModelPackage.Load(result.ModelPath);
        meta.ModelName.Should().Be("PartTypeClassifier");
        meta.TaskType.Should().Be(DeepLearningTaskType.Classification);
        onnxBytes.Length.Should().BeGreaterThan(0);
    }
}
