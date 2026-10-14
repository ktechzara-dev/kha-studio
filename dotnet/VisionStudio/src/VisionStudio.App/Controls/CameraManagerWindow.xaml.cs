using System.Windows;
using VisionStudio.App.ViewModels;

namespace VisionStudio.App.Controls;

public partial class CameraManagerWindow : Window
{
    public CameraManagerViewModel ViewModel { get; }

    public CameraManagerWindow(CameraManagerViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = ViewModel;

        Closed += async (_, _) =>
        {
            await ViewModel.DisposeAsync();
        };
    }
}
