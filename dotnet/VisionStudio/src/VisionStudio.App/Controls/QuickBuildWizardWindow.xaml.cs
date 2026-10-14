using System.Windows;
using Window = System.Windows.Window;

namespace VisionStudio.App.Controls;

public partial class QuickBuildWizardWindow : Window
{
    public Action<string>? OnWizardSelected { get; set; }

    public QuickBuildWizardWindow()
    {
        InitializeComponent();
    }

    private void BtnBuildPresence_Click(object sender, RoutedEventArgs e)
    {
        OnWizardSelected?.Invoke("PresenceCheck");
        MessageBox.Show("Đã khởi tạo Pipeline mẫu: Kiểm tra Hiện diện Linh kiện (Presence/Absence Check)!", "Quick Build", MessageBoxButton.OK, MessageBoxImage.Information);
        Close();
    }

    private void BtnBuildMeasure_Click(object sender, RoutedEventArgs e)
    {
        OnWizardSelected?.Invoke("MeasureDistance");
        MessageBox.Show("Đã khởi tạo Pipeline mẫu: Đo lường Kích thước & SPC (Measure & SPC)!", "Quick Build", MessageBoxButton.OK, MessageBoxImage.Information);
        Close();
    }

    private void BtnBuildCode_Click(object sender, RoutedEventArgs e)
    {
        OnWizardSelected?.Invoke("ReadCodeAndGrade");
        MessageBox.Show("Đã khởi tạo Pipeline mẫu: Đọc & Chấm điểm Mã vạch ISO/IEC!", "Quick Build", MessageBoxButton.OK, MessageBoxImage.Information);
        Close();
    }

    private void BtnBuildLocate_Click(object sender, RoutedEventArgs e)
    {
        OnWizardSelected?.Invoke("LocateAndPick");
        MessageBox.Show("Đã khởi tạo Pipeline mẫu: Định vị Hình học & Robot Guidance!", "Quick Build", MessageBoxButton.OK, MessageBoxImage.Information);
        Close();
    }

    private void BtnBuildDefect_Click(object sender, RoutedEventArgs e)
    {
        OnWizardSelected?.Invoke("SurfaceDefect");
        MessageBox.Show("Đã khởi tạo Pipeline mẫu: Kiểm tra Khuyết tật Bề mặt (Surface Defect)!", "Quick Build", MessageBoxButton.OK, MessageBoxImage.Information);
        Close();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
