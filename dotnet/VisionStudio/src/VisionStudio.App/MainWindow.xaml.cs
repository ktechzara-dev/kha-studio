using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VisionStudio.App.ViewModels;

namespace VisionStudio.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        PreviewKeyDown += OnWindowPreviewKeyDown;
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete || e.Key == Key.Back)
        {
            var focused = FocusManager.GetFocusedElement(this);
            if (focused is not TextBox)
            {
                if (DataContext is MainViewModel vm && (vm.DesignVm.SelectedNode != null || vm.DesignVm.SelectedConnection != null))
                {
                    vm.DesignVm.DeleteSelected();
                    e.Handled = true;
                }
            }
        }
    }
}