using System.Reflection;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;

namespace VisionStudio.Core.Tools;

/// <summary>
/// Convenient abstract base class for IVisionTool implementations providing parameter and port scaffolding.
/// </summary>
public abstract class VisionToolBase : IVisionTool
{
    private readonly List<ToolParameter> _parameters = new();
    private readonly List<ToolPort> _inputs = new();
    private readonly List<ToolPort> _outputs = new();

    public string Id { get; protected set; }
    public string DisplayName { get; protected set; }
    public string Category { get; protected set; }
    public string Description { get; protected set; }

    public IReadOnlyList<ToolParameter> Parameters => _parameters;
    public IReadOnlyList<ToolPort> Inputs => _inputs;
    public IReadOnlyList<ToolPort> Outputs => _outputs;

    protected VisionToolBase(string id, string displayName, string category, string description = "")
    {
        Id = id;
        DisplayName = displayName;
        Category = category;
        Description = description;

        // Auto-extract attribute metadata if not explicitly provided
        var attr = GetType().GetCustomAttribute<VisionToolAttribute>();
        if (attr != null)
        {
            if (string.IsNullOrEmpty(Id)) Id = attr.Id;
            if (string.IsNullOrEmpty(DisplayName)) DisplayName = attr.DisplayName;
            if (string.IsNullOrEmpty(Category)) Category = attr.Category;
            if (string.IsNullOrEmpty(Description) && attr.Description != null) Description = attr.Description;
        }
    }

    protected ToolParameter AddParameter(ToolParameter parameter)
    {
        _parameters.Add(parameter);
        return parameter;
    }

    protected ToolPort AddInput(ToolPort port)
    {
        _inputs.Add(port);
        return port;
    }

    protected ToolPort AddOutput(ToolPort port)
    {
        _outputs.Add(port);
        return port;
    }

    public abstract ToolResult Execute(ToolContext ctx, CancellationToken ct);

    public virtual void Reset()
    {
        // Default no-op for stateless tools
    }
}
