using System.Collections.Concurrent;
using System.Reflection;

namespace VisionStudio.Core.Tools;

/// <summary>
/// Metadata descriptor describing an available tool in the catalog.
/// </summary>
public sealed record ToolDescriptor(
    string Id,
    string DisplayName,
    string Category,
    string Description,
    Type ToolType);

/// <summary>
/// Central registry and factory for vision tools loaded from built-in assemblies and external plugins.
/// </summary>
public sealed class ToolRegistry
{
    public static ToolRegistry Shared { get; } = new();

    private readonly ConcurrentDictionary<string, ToolDescriptor> _registry = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Func<IVisionTool>> _factories = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers a tool by type.
    /// </summary>
    public void RegisterTool(Type toolType)
    {
        if (!typeof(IVisionTool).IsAssignableFrom(toolType) || toolType.IsAbstract || toolType.IsInterface)
        {
            throw new ArgumentException($"Type {toolType.FullName} must be a concrete implementation of IVisionTool.", nameof(toolType));
        }

        var attr = toolType.GetCustomAttribute<VisionToolAttribute>();
        var id = attr?.Id ?? toolType.Name;
        var name = attr?.DisplayName ?? toolType.Name;
        var category = attr?.Category ?? "General";
        var desc = attr?.Description ?? string.Empty;

        var descriptor = new ToolDescriptor(id, name, category, desc, toolType);
        _registry[id] = descriptor;
    }

    /// <summary>
    /// Registers a tool by generic type.
    /// </summary>
    public void RegisterTool<T>() where T : class, IVisionTool, new()
    {
        RegisterTool(typeof(T));
    }

    /// <summary>
    /// Scans an assembly for all concrete IVisionTool types decorated with VisionToolAttribute.
    /// </summary>
    public int RegisterAssembly(Assembly assembly)
    {
        var count = 0;
        var types = assembly.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface && typeof(IVisionTool).IsAssignableFrom(t));

        foreach (var type in types)
        {
            RegisterTool(type);
            count++;
        }

        return count;
    }

    /// <summary>
    /// Registers a custom tool factory (used by Macros and dynamic plugins).
    /// </summary>
    public void RegisterFactory(string id, string name, string category, string description, Func<IVisionTool> factory)
    {
        _factories[id] = factory;
        _registry[id] = new ToolDescriptor(id, name, category, description, typeof(IVisionTool));
    }

    /// <summary>
    /// Register alias for custom factory lambda.
    /// </summary>
    public void Register(string id, Func<IVisionTool> factory)
    {
        RegisterFactory(id, id, "Custom", string.Empty, factory);
    }

    /// <summary>
    /// Returns all registered tool descriptors ordered by category and name.
    /// </summary>
    public IReadOnlyList<ToolDescriptor> GetAvailableTools()
    {
        return _registry.Values
            .OrderBy(d => d.Category)
            .ThenBy(d => d.DisplayName)
            .ToList();
    }

    /// <summary>
    /// Checks if a tool with given id is registered.
    /// </summary>
    public bool HasTool(string toolId) => _registry.ContainsKey(toolId) || _factories.ContainsKey(toolId);

    /// <summary>
    /// Creates a fresh instance of the requested tool.
    /// </summary>
    public IVisionTool CreateInstance(string toolId)
    {
        if (_factories.TryGetValue(toolId, out var factory))
        {
            return factory();
        }

        if (!_registry.TryGetValue(toolId, out var descriptor))
        {
            throw new KeyNotFoundException($"Tool with ID '{toolId}' is not registered in the ToolRegistry.");
        }

        var instance = Activator.CreateInstance(descriptor.ToolType) as IVisionTool;
        if (instance == null)
        {
            throw new InvalidOperationException($"Failed to activate tool instance for type {descriptor.ToolType.FullName}.");
        }

        return instance;
    }
}
