namespace VisionStudio.Core.Ports;

/// <summary>
/// Defines a typed input or output terminal on a vision tool.
/// </summary>
public sealed record ToolPort(
    string Id,
    string Name,
    PortType Type,
    PortDirection Direction,
    bool IsRequired = true,
    object? DefaultValue = null,
    string? Description = null)
{
    /// <summary>
    /// Checks whether an upstream output port can connect to this downstream port.
    /// </summary>
    public bool IsCompatibleWith(ToolPort other)
    {
        if (Direction == other.Direction)
        {
            return false;
        }

        if (Type == PortType.Any || other.Type == PortType.Any)
        {
            return true;
        }

        return Type == other.Type;
    }
}
