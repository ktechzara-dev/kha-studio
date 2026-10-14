using VisionStudio.DeepLearning.Model;

namespace VisionStudio.Training;

public sealed class TrainingConfig
{
    public string ModelName { get; set; } = "IndustrialInspectionModel";
    public DeepLearningTaskType TaskType { get; set; } = DeepLearningTaskType.Classification;
    public string Backbone { get; set; } = "resnet18";
    public int InputWidth { get; set; } = 64;
    public int InputHeight { get; set; } = 64;
    public int BatchSize { get; set; } = 16;
    public int Epochs { get; set; } = 10;
    public double LearningRate { get; set; } = 0.001;
    public int Seed { get; set; } = 42;
    public int GpuDevice { get; set; } = -1; // -1 for CPU
    public string DatasetPath { get; set; } = string.Empty;
    public string OutputDir { get; set; } = string.Empty;
    public List<string> ClassLabels { get; set; } = new();
}

public sealed record TrainingProgressUpdate(
    int Epoch,
    int TotalEpochs,
    int Step,
    int TotalSteps,
    double Loss,
    double ValMetric, // F1 or AUROC
    string Message);
