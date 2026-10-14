using System.Globalization;
using Microsoft.Extensions.Logging;
using VisionStudio.Core.Models;
using VisionStudio.Core.Types;

namespace VisionStudio.Scripting.Host;

/// <summary>
/// Runtime implementation of IScriptContext.
/// </summary>
public sealed class ScriptContext : IScriptContext
{
    private readonly Dictionary<string, object?> _outputs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, object?> _parameters = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, object?> _tags = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger? _logger;
    private readonly Action<string, object?>? _tagWriter;

    public IReadOnlyDictionary<string, object?> Inputs { get; }
    public IDictionary<string, object?> Outputs => _outputs;
    public IDictionary<string, object?> Parameters => _parameters;
    public IReadOnlyDictionary<string, object?> Tags => _tags;

    public ScriptContext(
        IReadOnlyDictionary<string, object?> inputs,
        IReadOnlyDictionary<string, object?>? parameters = null,
        IReadOnlyDictionary<string, object?>? tags = null,
        Action<string, object?>? tagWriter = null,
        ILogger? logger = null)
    {
        Inputs = inputs ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        _logger = logger;
        _tagWriter = tagWriter;

        if (parameters != null)
        {
            foreach (var kvp in parameters) _parameters[kvp.Key] = kvp.Value;
        }

        if (tags != null)
        {
            foreach (var kvp in tags) _tags[kvp.Key] = kvp.Value;
        }
    }

    public T Get<T>(string name, T defaultValue = default!)
    {
        if (Inputs.TryGetValue(name, out var val) && val != null)
        {
            if (val is T typed) return typed;
            try
            {
                return (T)Convert.ChangeType(val, typeof(T), CultureInfo.InvariantCulture);
            }
            catch
            {
                return defaultValue;
            }
        }
        return defaultValue;
    }

    public double GetNumber(string name, double defaultValue = 0.0) => Get<double>(name, defaultValue);
    public string GetString(string name, string defaultValue = "") => Get<string>(name, defaultValue);
    public bool GetBool(string name, bool defaultValue = false) => Get<bool>(name, defaultValue);

    public ImageFrame? GetImage(string name)
    {
        if (Inputs.TryGetValue(name, out var val) && val is ImageFrame frame)
        {
            return frame;
        }
        return null;
    }

    public VisionTable? GetTable(string name)
    {
        if (Inputs.TryGetValue(name, out var val) && val is VisionTable tbl)
        {
            return tbl;
        }
        return null;
    }

    public Region? GetRegion(string name)
    {
        if (Inputs.TryGetValue(name, out var val) && val is Region reg)
        {
            return reg;
        }
        return null;
    }

    public Path2D? GetPath(string name)
    {
        if (Inputs.TryGetValue(name, out var val) && val is Path2D path)
        {
            return path;
        }
        return null;
    }

    public void Set(string name, object? value)
    {
        _outputs[name] = value;
    }

    public void Log(string message)
    {
        _logger?.LogInformation("[Script] {Message}", message);
    }

    public void LogWarning(string message)
    {
        _logger?.LogWarning("[Script] {Message}", message);
    }

    public void LogError(string message)
    {
        _logger?.LogError("[Script] {Message}", message);
    }

    public object? ReadTag(string tagName)
    {
        _tags.TryGetValue(tagName, out var val);
        return val;
    }

    public void WriteTag(string tagName, object? value)
    {
        _tags[tagName] = value;
        _tagWriter?.Invoke(tagName, value);
    }
}
