using CommunityToolkit.Mvvm.ComponentModel;

namespace KhaStudio.Modules.ScadaVision.ViewModels;

public partial class ScadaVisionMainViewModel : ObservableObject
{
    [ObservableProperty]
    private ScadaDesignViewModel _scadaDesign = new();

    [ObservableProperty]
    private VisionTestViewModel _visionTest = new();

    [ObservableProperty]
    private int _selectedTabIndex = 0;
}
