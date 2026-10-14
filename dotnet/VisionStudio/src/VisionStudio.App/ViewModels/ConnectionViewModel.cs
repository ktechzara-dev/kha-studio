using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;

namespace VisionStudio.App.ViewModels;

/// <summary>
/// ViewModel representing a directed connection wire between an output port and an input port.
/// </summary>
public partial class ConnectionViewModel : ObservableObject
{
    public PortViewModel SourcePort { get; }
    public PortViewModel TargetPort { get; }

    public string SourceNodeId => SourcePort.Node.NodeId;
    public string SourcePortName => SourcePort.Name;
    public string TargetNodeId => TargetPort.Node.NodeId;
    public string TargetPortName => TargetPort.Name;

    [ObservableProperty]
    private Point _startPoint;

    [ObservableProperty]
    private Point _endPoint;

    [ObservableProperty]
    private bool _isSelected;

    public ConnectionViewModel(PortViewModel sourcePort, PortViewModel targetPort)
    {
        SourcePort = sourcePort ?? throw new ArgumentNullException(nameof(sourcePort));
        TargetPort = targetPort ?? throw new ArgumentNullException(nameof(targetPort));

        UpdatePoints();
    }

    public void UpdatePoints()
    {
        StartPoint = SourcePort.CanvasPosition;
        EndPoint = TargetPort.CanvasPosition;
    }
}
