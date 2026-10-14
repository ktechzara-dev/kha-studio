using System.Text.Json;

namespace VisionStudio.Core.Recipes;

/// <summary>
/// Product-specific inspection recipe (Job).
/// Encapsulates tool parameter overrides, model/template bindings, and tolerance limits
/// layered on top of an invariant pipeline DAG.
/// </summary>
public sealed class Recipe
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Default Recipe";
    public string ProductCode { get; set; } = "SKU-001";
    public string PipelineVersion { get; set; } = "1.0.0";
    public string Description { get; set; } = string.Empty;
    public string Author { get; set; } = "Engineer";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastModified { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Parameter overrides mapped by [NodeId -> [ParameterName -> Value]].
    /// </summary>
    public Dictionary<string, Dictionary<string, object?>> ParameterOverrides { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Model or template bindings mapped by [NodeId -> ModelFilePath].
    /// </summary>
    public Dictionary<string, string> ModelBindings { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Upper/Lower measurement acceptance limits mapped by [MeasurementKey -> MeasurementTolerance].
    /// </summary>
    public Dictionary<string, MeasurementTolerance> Tolerances { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public void SetParameter(string nodeId, string parameterName, object? value)
    {
        if (!ParameterOverrides.TryGetValue(nodeId, out var nodeParams))
        {
            nodeParams = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            ParameterOverrides[nodeId] = nodeParams;
        }

        nodeParams[parameterName] = value;
        LastModified = DateTimeOffset.UtcNow;
    }

    public object? GetParameter(string nodeId, string parameterName)
    {
        if (ParameterOverrides.TryGetValue(nodeId, out var nodeParams) &&
            nodeParams.TryGetValue(parameterName, out var val))
        {
            if (val is JsonElement je)
            {
                if (je.ValueKind == JsonValueKind.Number)
                {
                    if (je.TryGetDouble(out double d)) return d;
                }
                else if (je.ValueKind == JsonValueKind.True || je.ValueKind == JsonValueKind.False)
                {
                    return je.GetBoolean();
                }
                else if (je.ValueKind == JsonValueKind.String)
                {
                    return je.GetString();
                }
            }
            return val;
        }
        return null;
    }

    public T? GetParameter<T>(string nodeId, string parameterName)
    {
        var raw = GetParameter(nodeId, parameterName);
        if (raw == null) return default;
        if (raw is T typed) return typed;
        try
        {
            return (T)Convert.ChangeType(raw, typeof(T));
        }
        catch
        {
            return default;
        }
    }

    public Recipe Clone(string newName, string? newProductCode = null)
    {
        string json = ToJson();
        var copy = FromJson(json);
        copy.Id = Guid.NewGuid().ToString("N");
        copy.Name = newName;
        copy.ProductCode = newProductCode ?? copy.ProductCode;
        copy.CreatedAt = DateTimeOffset.UtcNow;
        copy.LastModified = DateTimeOffset.UtcNow;
        return copy;
    }

    public string ToJson()
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    public static Recipe FromJson(string json)
    {
        return JsonSerializer.Deserialize<Recipe>(json)
            ?? throw new InvalidOperationException("Failed to deserialize Recipe JSON.");
    }

    public void Save(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        File.WriteAllText(filePath, ToJson());
    }

    public static Recipe Load(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Recipe file not found: {filePath}", filePath);
        }
        return FromJson(File.ReadAllText(filePath));
    }
}
