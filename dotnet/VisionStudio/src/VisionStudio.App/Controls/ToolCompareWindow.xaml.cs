using System.Windows;
using Window = System.Windows.Window;

namespace VisionStudio.App.Controls;

public partial class ToolCompareWindow : Window
{
    public ToolCompareWindow()
    {
        InitializeComponent();
    }

    private void BtnRunCompare_Click(object sender, RoutedEventArgs e)
    {
        TxtPreviewA.Text = "📷 Rendered: 2 defects detected, Score 94.5%";
        TxtPreviewB.Text = "📷 Rendered: 0 defects detected, Score 98.2%";

        TxtMetricsA.Text = "Thời gian: 14.2 ms | Số lỗi: 2 | Độ tách biệt: 94.5%";
        TxtMetricsB.Text = "Thời gian: 9.8 ms | Số lỗi: 0 | Độ tách biệt: 98.2%";

        TxtDiffSummary.Text = "Chênh lệch: Cấu hình B nhanh hơn 4.4 ms (-31%) • Độ ổn định cao hơn • Giảm báo giả 100%.";
    }

    private void BtnApplyB_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Đã áp dụng thông số cấu hình B vào Node hiện tại!", "Áp Dụng", MessageBoxButton.OK, MessageBoxImage.Information);
        Close();
    }
}
