using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VisionStudio.App.ViewModels;

namespace VisionStudio.App.Controls;

public partial class InspectionStripControl : UserControl
{
    public InspectionStripControl()
    {
        InitializeComponent();
    }

    private void OnCardMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is InspectionItemViewModel item)
        {
            if (DataContext is DesignViewModel vm)
            {
                vm.SelectedNode = item.Node;
            }
        }
    }
}
