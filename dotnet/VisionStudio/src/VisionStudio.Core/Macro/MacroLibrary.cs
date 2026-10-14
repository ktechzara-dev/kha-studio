using System.IO;
using Microsoft.Extensions.Logging;
using VisionStudio.Core.Tools;

namespace VisionStudio.Core.Macro;

/// <summary>
/// Manages a searchable repository of versioned .vsmacro blocks across local project folders
/// and shared network shares.
/// </summary>
public sealed class MacroLibrary
{
    private readonly List<string> _searchPaths = new();
    private readonly Dictionary<string, MacroDefinition> _macrosById = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _macroFilePaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger? _logger;

    public IReadOnlyCollection<MacroDefinition> Macros => _macrosById.Values;
    public IReadOnlyList<string> SearchPaths => _searchPaths;

    public MacroLibrary(string? localPath = null, string? networkPath = null, ILogger? logger = null)
    {
        _logger = logger;

        if (!string.IsNullOrEmpty(localPath))
        {
            AddSearchPath(localPath);
        }

        if (!string.IsNullOrEmpty(networkPath))
        {
            AddSearchPath(networkPath);
        }
    }

    public void AddSearchPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        if (!_searchPaths.Contains(path, StringComparer.OrdinalIgnoreCase))
        {
            _searchPaths.Add(path);
            if (!Directory.Exists(path))
            {
                try
                {
                    Directory.CreateDirectory(path);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning("Could not create macro library directory '{Path}': {Error}", path, ex.Message);
                }
            }
        }
    }

    public void Refresh()
    {
        _macrosById.Clear();
        _macroFilePaths.Clear();

        foreach (var dir in _searchPaths)
        {
            if (!Directory.Exists(dir)) continue;

            try
            {
                var files = Directory.GetFiles(dir, "*.vsmacro", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    try
                    {
                        var macro = MacroDefinition.LoadFromFile(file);
                        _macrosById[macro.Id] = macro;
                        _macroFilePaths[macro.Id] = file;
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning("Failed to load macro file '{File}': {Error}", file, ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError("Error scanning macro folder '{Dir}': {Error}", dir, ex.Message);
            }
        }
    }

    public MacroDefinition? GetById(string id)
    {
        _macrosById.TryGetValue(id, out var macro);
        return macro;
    }

    public MacroDefinition? GetByName(string name)
    {
        return _macrosById.Values.FirstOrDefault(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public void SaveMacro(MacroDefinition macro, string? destinationDir = null)
    {
        string dir = destinationDir ?? _searchPaths.FirstOrDefault() ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Macros");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string safeName = string.Join("_", macro.Name.Split(Path.GetInvalidFileNameChars()));
        string fileName = $"{safeName}_{macro.Id[..Math.Min(8, macro.Id.Length)]}.vsmacro";
        string filePath = Path.Combine(dir, fileName);

        macro.SaveToFile(filePath);
        _macrosById[macro.Id] = macro;
        _macroFilePaths[macro.Id] = filePath;
    }

    public bool DeleteMacro(string id)
    {
        if (_macroFilePaths.TryGetValue(id, out var filePath) && File.Exists(filePath))
        {
            try
            {
                File.Delete(filePath);
            }
            catch (Exception ex)
            {
                _logger?.LogError("Failed to delete macro file '{File}': {Error}", filePath, ex.Message);
                return false;
            }
        }

        _macrosById.Remove(id);
        _macroFilePaths.Remove(id);
        return true;
    }

    /// <summary>
    /// Registers all discovered macros into a ToolRegistry so they can be placed in pipelines.
    /// </summary>
    public void RegisterAllToRegistry(ToolRegistry registry)
    {
        foreach (var macro in _macrosById.Values)
        {
            string toolId = $"Macro.{macro.Id}";
            var def = macro; // capture
            registry.Register(toolId, () => new MacroTool(def, registry));
        }
    }
}
