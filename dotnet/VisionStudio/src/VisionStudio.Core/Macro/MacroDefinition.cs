using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using VisionStudio.Core.Pipeline;

namespace VisionStudio.Core.Macro;

/// <summary>
/// Versioned definition of a reusable sub-pipeline macro block.
/// Can be saved to or loaded from a .vsmacro file and published to a shared library.
/// </summary>
public sealed class MacroDefinition
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New Macro";
    public string Category { get; set; } = "Macros";
    public string Description { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public string Author { get; set; } = Environment.UserName;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ModifiedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Serialized inner sub-pipeline graph.
    /// </summary>
    public SerializedPipelineDocument SerializedPipeline { get; set; } = new();

    /// <summary>
    /// Ports exposed to upstream parents as inputs.
    /// </summary>
    public List<MacroPortMapping> ExposedInputs { get; set; } = new();

    /// <summary>
    /// Ports exposed to downstream consumers as outputs.
    /// </summary>
    public List<MacroPortMapping> ExposedOutputs { get; set; } = new();

    /// <summary>
    /// Parameters exposed on the macro node for operator adjustment.
    /// </summary>
    public List<MacroParameterMapping> ExposedParameters { get; set; } = new();

    public string ToJson()
    {
        return JsonSerializer.Serialize(this, JsonOptions);
    }

    public static MacroDefinition FromJson(string json)
    {
        return JsonSerializer.Deserialize<MacroDefinition>(json, JsonOptions)
               ?? throw new InvalidOperationException("Failed to deserialize MacroDefinition from JSON.");
    }

    public void SaveToFile(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        ModifiedAt = DateTimeOffset.UtcNow;
        File.WriteAllText(filePath, ToJson());
    }

    public static MacroDefinition LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Macro file not found: {filePath}", filePath);
        }

        string json = File.ReadAllText(filePath);
        return FromJson(json);
    }
}
