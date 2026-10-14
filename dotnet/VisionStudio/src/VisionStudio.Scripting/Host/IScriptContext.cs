using VisionStudio.Core.Models;
using VisionStudio.Core.Types;

namespace VisionStudio.Scripting.Host;

/// <summary>
/// Safe industrial scripting context exposed to C# Roslyn scripts.
/// Provides typed access to inputs, outputs, tags, and logging.
/// </summary>
public interface IScriptContext
{
    IReadOnlyDictionary<string, object?> Inputs { get; }
    IDictionary<string, object?> Outputs { get; }
    IDictionary<string, object?> Parameters { get; }
    IReadOnlyDictionary<string, object?> Tags { get; }

    T Get<T>(string name, T defaultValue = default!);
    double GetNumber(string name, double defaultValue = 0.0);
    string GetString(string name, string defaultValue = "");
    bool GetBool(string name, bool defaultValue = false);
    ImageFrame? GetImage(string name);
    VisionTable? GetTable(string name);
    Region? GetRegion(string name);
    Path2D? GetPath(string name);

    void Set(string name, object? value);

    void Log(string message);
    void LogWarning(string message);
    void LogError(string message);

    object? ReadTag(string tagName);
    void WriteTag(string tagName, object? value);
}
