using System.Text.Json;
using System.Text.Json.Serialization;

namespace VisionStudio.Matching.Models;

/// <summary>
/// A level in the multi-resolution geometric pyramid.
/// </summary>
public sealed class GeometricPyramidLevel
{
    public int Level { get; set; }
    public float ScaleFactor { get; set; } = 1.0f;
    public List<GeometricPoint> Points { get; set; } = new();

    public GeometricPyramidLevel() { }

    public GeometricPyramidLevel(int level, float scaleFactor, IEnumerable<GeometricPoint> points)
    {
        Level = level;
        ScaleFactor = scaleFactor;
        Points = points.ToList();
    }
}

/// <summary>
/// Trainable geometric contour model learned from edge gradients rather than pixel brightness.
/// Robust against illumination shifts, contrast reversals, background clutter, and occlusion.
/// </summary>
public sealed class GeometricModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "GeometricModel";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public float OriginX { get; set; }
    public float OriginY { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }

    /// <summary>
    /// Multi-scale pyramid levels. Level 0 is full resolution.
    /// </summary>
    public List<GeometricPyramidLevel> PyramidLevels { get; set; } = new();

    [JsonIgnore]
    public int PointCount => PyramidLevels.Count > 0 ? PyramidLevels[0].Points.Count : 0;

    [JsonIgnore]
    public IReadOnlyList<GeometricPoint> BasePoints => PyramidLevels.Count > 0 ? PyramidLevels[0].Points : Array.Empty<GeometricPoint>();

    public GeometricPyramidLevel? GetLevel(int level)
    {
        return PyramidLevels.FirstOrDefault(l => l.Level == level);
    }

    public string ToJson()
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
    }

    public static GeometricModel FromJson(string json)
    {
        return JsonSerializer.Deserialize<GeometricModel>(json) ?? throw new InvalidOperationException("Failed to deserialize GeometricModel from JSON.");
    }

    public void SaveToFile(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        File.WriteAllText(filePath, ToJson());
    }

    public static GeometricModel LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Model file not found: {filePath}", filePath);
        }
        var json = File.ReadAllText(filePath);
        return FromJson(json);
    }
}
