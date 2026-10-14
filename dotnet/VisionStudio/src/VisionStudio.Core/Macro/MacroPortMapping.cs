using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;

namespace VisionStudio.Core.Macro;

/// <summary>
/// Defines an exposed port terminal on a Macro block mapped to an internal child node's port.
/// </summary>
public sealed class MacroPortMapping
{
    public string ExposedPortId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public PortType Type { get; set; } = PortType.Any;
    public PortDirection Direction { get; set; } = PortDirection.Input;
    public string InternalNodeId { get; set; } = string.Empty;
    public string InternalPortId { get; set; } = string.Empty;
    public bool IsRequired { get; set; } = true;
    public object? DefaultValue { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// Defines an exposed tunable parameter on a Macro block forwarded to an internal child node.
/// </summary>
public sealed class MacroParameterMapping
{
    public string ExposedParamName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public ParameterType Type { get; set; } = ParameterType.Double;
    public string InternalNodeId { get; set; } = string.Empty;
    public string InternalParamName { get; set; } = string.Empty;
    public object? DefaultValue { get; set; }
    public double? Min { get; set; }
    public double? Max { get; set; }
    public double? Step { get; set; }
    public string[]? EnumOptions { get; set; }
    public string? Description { get; set; }
}
