using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using VisionStudio.Core.Tools;

namespace VisionStudio.App.ViewModels;

/// <summary>
/// ViewModel representing an individual inspection thumbnail card in the visual strip.
/// </summary>
public partial class InspectionItemViewModel : ObservableObject
{
    public NodeViewModel Node { get; }
    public string NodeId => Node.NodeId;
    public string DisplayName => Node.DisplayName;
    public string Category => Node.Category;

    public BitmapSource? Thumbnail => Node.Thumbnail;
    public ToolStatus? Status => Node.Status;
    public double ExecutionTimeMs => Node.ExecutionTimeMs;

    [ObservableProperty]
    private bool _isSelected;

    public InspectionItemViewModel(NodeViewModel node)
    {
        Node = node ?? throw new ArgumentNullException(nameof(node));
    }

    public void Refresh()
    {
        OnPropertyChanged(nameof(Thumbnail));
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(ExecutionTimeMs));
    }
}
