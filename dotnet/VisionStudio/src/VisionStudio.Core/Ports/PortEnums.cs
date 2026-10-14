namespace VisionStudio.Core.Ports;

/// <summary>
/// Data type of a tool's connection port.
/// </summary>
public enum PortType
{
    Image,
    Number,
    String,
    Boolean,
    Point,
    Region,
    Contour,
    Pose,
    Table,
    Path,
    Array,
    Any
}

/// <summary>
/// Flow direction of the port.
/// </summary>
public enum PortDirection
{
    Input,
    Output
}
