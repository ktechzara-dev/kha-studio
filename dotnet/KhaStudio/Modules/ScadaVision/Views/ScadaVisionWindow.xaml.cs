using System;
using System.Windows;
using KhaStudio.Modules.ScadaVision.ViewModels;

namespace KhaStudio.Modules.ScadaVision.Views;

public partial class ScadaVisionWindow : Window
{
    public ScadaVisionWindow()
    {
        InitializeComponent();
        Closed += OnWindowClosed;
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (DataContext is ScadaVisionMainViewModel vm)
        {
            vm.VisionTest.Dispose();
        }
    }
}
