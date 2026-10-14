using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using VisionStudio.Core.Ports;

namespace VisionStudio.App.ViewModels;

/// <summary>
/// ViewModel representing an input or output port pin on a pipeline node.
/// </summary>
public partial class PortViewModel : ObservableObject
{
    public NodeViewModel Node { get; }
    public ToolPort Port { get; }

    public string Name => Port.Name;
    public string DisplayName => Port.Name;
    public PortType PortType => Port.Type;
    public PortDirection Direction => Port.Direction;
    public bool IsInput => Port.Direction == PortDirection.Input;
    public bool IsOutput => Port.Direction == PortDirection.Output;

    [ObservableProperty]
    private Point _canvasPosition;

    public Point ConnectionPoint => CanvasPosition;

    partial void OnCanvasPositionChanged(Point value)
    {
        OnPropertyChanged(nameof(ConnectionPoint));
    }

    [ObservableProperty]
    private bool _isConnected;

    public PortViewModel(NodeViewModel node, ToolPort port)
    {
        Node = node ?? throw new ArgumentNullException(nameof(node));
        Port = port ?? throw new ArgumentNullException(nameof(port));
    }

    /// <summary>
    /// Checks whether this port can establish a connection with the specified other port.
    /// </summary>
    public bool CanConnectTo(PortViewModel? other)
    {
        if (other == null) return false;
        if (ReferenceEquals(this, other)) return false;
        if (ReferenceEquals(Node, other.Node)) return false; // No self-connections
        if (IsInput == other.IsInput) return false; // Must connect Output -> Input

        // Type compatibility
        var outputType = IsOutput ? PortType : other.PortType;
        var inputType = IsInput ? PortType : other.PortType;

        if (outputType == PortType.Any || inputType == PortType.Any)
        {
            return true;
        }

        return outputType == inputType;
    }
}
