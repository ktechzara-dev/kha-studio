using System.Text.Json;
using System.Text.Json.Serialization;

namespace VisionStudio.Runtime.Packaging;

public sealed class StationMappingEntry
{
    public string StationId { get; set; } = "Station_01";
    public string PipelineFile { get; set; } = "pipeline.json";
    public string? DefaultRecipe { get; set; }
}

public sealed class ModelEntry
{
    public string ModelId { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string ModelType { get; set; } = "Classifier";
    public string Version { get; set; } = "1.0.0";
}

public sealed class CalibrationEntry
{
    public string CalibrationId { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public double PixelsPerMm { get; set; } = 10.0;
}

/// <summary>
/// Manifest descriptor for a .vspkg industrial vision deployment archive.
/// </summary>
public sealed class PackageManifest
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "InspectionProject";
    public string Version { get; set; } = "1.0.0";
    public string Description { get; set; } = "Industrial Vision Deployment Package";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string Author { get; set; } = "Kha Studio";
    public string ChecksumSha256 { get; set; } = string.Empty;
    public string? Signature { get; set; }

    public List<StationMappingEntry> Stations { get; set; } = new();
    public List<ModelEntry> Models { get; set; } = new();
    public List<CalibrationEntry> Calibrations { get; set; } = new();
    public Dictionary<string, string> Metadata { get; set; } = new();

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static PackageManifest? FromJson(string json) =>
        JsonSerializer.Deserialize<PackageManifest>(json, JsonOptions);
}
