using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;

namespace VisionStudio.Core.Tools;

/// <summary>
/// Attribute applied to IVisionTool classes for automatic catalog registration and UI discovery.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class VisionToolAttribute : Attribute
{
    public string Id { get; }
    public string DisplayName { get; }
    public string Category { get; }
    public string? Description { get; set; }

    public VisionToolAttribute(string id, string displayName, string category)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        DisplayName = displayName ?? id;
        Category = category ?? "General";
    }
}

/// <summary>
/// Fundamental contract for all industrial vision processing, inspection, and flow control tools.
/// </summary>
public interface IVisionTool
{
    /// <summary>
    /// Unique catalog identifier (e.g. "Acquisition.CameraGrab", "Filter.Gaussian").
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Human-friendly display label.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Functional category (e.g. "Acquisition", "Filtering", "Geometry / ROI", "Measurement").
    /// </summary>
    string Category { get; }

    /// <summary>
    /// Brief description of what the tool accomplishes.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Mutable parameter definitions configured via the auto-generated property inspector.
    /// </summary>
    IReadOnlyList<ToolParameter> Parameters { get; }

    /// <summary>
    /// Input port definitions for pipeline DAG connection.
    /// </summary>
    IReadOnlyList<ToolPort> Inputs { get; }

    /// <summary>
    /// Output port definitions for downstream pipeline feeding.
    /// </summary>
    IReadOnlyList<ToolPort> Outputs { get; }

    /// <summary>
    /// Executes the tool's computer vision algorithm or logic.
    /// </summary>
    ToolResult Execute(ToolContext ctx, CancellationToken ct);

    /// <summary>
    /// Resets internal accumulated state (for stateful tools like counters, averagers).
    /// </summary>
    void Reset();
}
