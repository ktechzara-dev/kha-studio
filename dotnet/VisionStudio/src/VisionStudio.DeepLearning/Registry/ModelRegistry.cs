using System.Text.Json;
using VisionStudio.DeepLearning.Model;

namespace VisionStudio.DeepLearning.Registry;

public sealed class RegisteredModelEntry
{
    public string ModelId { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public DeepLearningTaskType TaskType { get; set; }
    public string Author { get; set; } = "Engineer";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string DatasetHash { get; set; } = string.Empty;
    public double? Accuracy { get; set; }
    public double? F1Score { get; set; }
    public double? Auroc { get; set; }
    public double? LatencyP50Ms { get; set; }
    public bool IsProduction { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public Dictionary<string, string> Tags { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public override string ToString() => $"{Name} v{Version} {(IsProduction ? "[PRODUCTION]" : "")}";
}

public sealed record ModelComparisonRow(
    string Property,
    string ModelAValue,
    string ModelBValue,
    string Winner);

/// <summary>
/// Industrial Model Registry managing versioned models, production promotion,
/// rollbacks, and side-by-side performance comparisons.
/// </summary>
public sealed class ModelRegistry
{
    private readonly string _registryDir;
    private readonly string _manifestPath;
    private readonly List<RegisteredModelEntry> _entries = new();

    public IReadOnlyList<RegisteredModelEntry> Entries => _entries;

    public ModelRegistry(string? registryDir = null)
    {
        _registryDir = registryDir ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VisionStudio", "ModelRegistry");
        if (!Directory.Exists(_registryDir))
        {
            Directory.CreateDirectory(_registryDir);
        }

        _manifestPath = Path.Combine(_registryDir, "registry_manifest.json");
        LoadManifest();
    }

    private void LoadManifest()
    {
        if (File.Exists(_manifestPath))
        {
            try
            {
                string json = File.ReadAllText(_manifestPath);
                var loaded = JsonSerializer.Deserialize<List<RegisteredModelEntry>>(json);
                if (loaded != null)
                {
                    _entries.Clear();
                    _entries.AddRange(loaded);
                }
            }
            catch
            {
                // Fall back to clean registry
            }
        }
    }

    public void SaveManifest()
    {
        string json = JsonSerializer.Serialize(_entries, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_manifestPath, json);
    }

    public RegisteredModelEntry RegisterModel(
        string name,
        string version,
        DeepLearningTaskType taskType,
        string vsModelFilePath,
        string author = "Engineer",
        string datasetHash = "",
        double? accuracy = null,
        double? f1 = null,
        double? auroc = null,
        double? p50 = null)
    {
        string destPath = Path.Combine(_registryDir, $"{name}_v{version}.vsmodel");
        if (File.Exists(vsModelFilePath) && vsModelFilePath != destPath)
        {
            File.Copy(vsModelFilePath, destPath, overwrite: true);
        }

        var entry = new RegisteredModelEntry
        {
            Name = name,
            Version = version,
            TaskType = taskType,
            Author = author,
            DatasetHash = datasetHash,
            Accuracy = accuracy,
            F1Score = f1,
            Auroc = auroc,
            LatencyP50Ms = p50,
            FilePath = destPath
        };

        _entries.Add(entry);
        SaveManifest();
        return entry;
    }

    /// <summary>
    /// Promotes a model version to Production, marking all other versions of the same model name as non-production.
    /// </summary>
    public void PromoteToProduction(string modelId)
    {
        var target = _entries.FirstOrDefault(m => m.ModelId == modelId)
            ?? throw new KeyNotFoundException($"Model with ID '{modelId}' not found in registry.");

        foreach (var m in _entries.Where(m => string.Equals(m.Name, target.Name, StringComparison.OrdinalIgnoreCase)))
        {
            m.IsProduction = (m.ModelId == target.ModelId);
        }

        SaveManifest();
    }

    /// <summary>
    /// Rolls back production designation to a specific previous model version.
    /// </summary>
    public void RollbackProduction(string modelName, string targetVersion)
    {
        var target = _entries.FirstOrDefault(m => string.Equals(m.Name, modelName, StringComparison.OrdinalIgnoreCase) && m.Version == targetVersion)
            ?? throw new KeyNotFoundException($"Model '{modelName}' version '{targetVersion}' not found.");

        PromoteToProduction(target.ModelId);
    }

    public RegisteredModelEntry? GetProductionModel(string modelName)
    {
        return _entries.FirstOrDefault(m => string.Equals(m.Name, modelName, StringComparison.OrdinalIgnoreCase) && m.IsProduction);
    }

    /// <summary>
    /// Compares two models side-by-side on metrics, latency, dataset hash, and creation dates.
    /// </summary>
    public IReadOnlyList<ModelComparisonRow> CompareModels(RegisteredModelEntry a, RegisteredModelEntry b)
    {
        var rows = new List<ModelComparisonRow>
        {
            new("Tên mô hình", a.Name, b.Name, "-"),
            new("Phiên bản", a.Version, b.Version, "-"),
            new("Tác vụ (Task)", a.TaskType.ToString(), b.TaskType.ToString(), "-"),
            new("Độ chính xác (Accuracy)", a.Accuracy?.ToString("P1") ?? "N/A", b.Accuracy?.ToString("P1") ?? "N/A",
                (a.Accuracy ?? 0) >= (b.Accuracy ?? 0) ? "Mô hình A" : "Mô hình B"),
            new("Điểm F1-Score", a.F1Score?.ToString("F3") ?? "N/A", b.F1Score?.ToString("F3") ?? "N/A",
                (a.F1Score ?? 0) >= (b.F1Score ?? 0) ? "Mô hình A" : "Mô hình B"),
            new("Điểm AUROC", a.Auroc?.ToString("F3") ?? "N/A", b.Auroc?.ToString("F3") ?? "N/A",
                (a.Auroc ?? 0) >= (b.Auroc ?? 0) ? "Mô hình A" : "Mô hình B"),
            new("Độ trễ (Latency p50)", a.LatencyP50Ms.HasValue ? $"{a.LatencyP50Ms.Value:F1} ms" : "N/A",
                b.LatencyP50Ms.HasValue ? $"{b.LatencyP50Ms.Value:F1} ms" : "N/A",
                (a.LatencyP50Ms ?? 999) <= (b.LatencyP50Ms ?? 999) ? "Mô hình A" : "Mô hình B"),
            new("Mã băm Dataset", a.DatasetHash, b.DatasetHash, a.DatasetHash == b.DatasetHash ? "Cùng Dataset" : "Khác Dataset"),
            new("Ngày tạo", a.CreatedAt.ToString("g"), b.CreatedAt.ToString("g"), "-")
        };

        return rows;
    }
}
