using System.Text.Json;
using System.Text.Json.Serialization;

namespace VisionStudio.DeepLearning.Model;

/// <summary>
/// Preprocessing, inference parameters, and metadata for a VisionStudio .vsmodel package.
/// </summary>
public sealed class ModelMetadata
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string ModelName { get; set; } = "IndustrialInspectionModel";
    public DeepLearningTaskType TaskType { get; set; } = DeepLearningTaskType.Classification;
    public string Version { get; set; } = "1.0.0";
    public string Description { get; set; } = "Deep Learning inspection model for automated quality assurance";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Tensor Preprocessing
    public string InputName { get; set; } = "input";
    public int InputWidth { get; set; } = 224;
    public int InputHeight { get; set; } = 224;
    public int InputChannels { get; set; } = 3;
    public bool IsBgr { get; set; } = false; // False = RGB (standard for timm/torchvision/ONNX models), True = BGR
    public float[] Mean { get; set; } = new[] { 0.485f, 0.456f, 0.406f }; // ImageNet default
    public float[] Std { get; set; } = new[] { 0.229f, 0.224f, 0.225f };

    // Outputs & Thresholds
    public List<string> OutputNames { get; set; } = new();
    public List<string> ClassLabels { get; set; } = new();
    public float DefaultConfidenceThreshold { get; set; } = 0.5f;
    public float DefaultAnomalyThreshold { get; set; } = 0.5f;
    public float DefaultNmsThreshold { get; set; } = 0.45f;

    // Traceability & Metrics
    public string? DatasetHash { get; set; }
    public double? AurocScore { get; set; }
    public double? F1Score { get; set; }
    public string? Backbone { get; set; }
    public Dictionary<string, string> CustomProperties { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static ModelMetadata FromJson(string json) =>
        JsonSerializer.Deserialize<ModelMetadata>(json, JsonOptions)
        ?? throw new InvalidOperationException("Failed to deserialize ModelMetadata from JSON.");
}
