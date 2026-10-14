using System.IO;
using System.Windows;
using System.Windows.Controls;
using OpenCvSharp;
using VisionStudio.Core.AutoTune;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Pipeline;
using Window = System.Windows.Window;

namespace VisionStudio.App.Controls;

public partial class AutoTuneWindow : Window
{
    private readonly PipelineGraph _graph;
    private PipelineNode? _selectedNode;
    private AutoTuneReport? _lastReport;

    public AutoTuneWindow(PipelineGraph graph, string? preselectedNodeId = null)
    {
        InitializeComponent();
        _graph = graph;

        PopulateNodes(preselectedNodeId);
    }

    private void PopulateNodes(string? preselectedNodeId)
    {
        CmbTargetNodes.ItemsSource = _graph.Nodes.ToList();
        CmbTargetNodes.DisplayMemberPath = "Title";

        if (!string.IsNullOrEmpty(preselectedNodeId))
        {
            var match = _graph.Nodes.FirstOrDefault(n => n.Id.Equals(preselectedNodeId, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                CmbTargetNodes.SelectedItem = match;
                return;
            }
        }

        if (_graph.Nodes.Count > 0)
        {
            CmbTargetNodes.SelectedIndex = 0;
        }
    }

    private void OnTargetNodeChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedNode = CmbTargetNodes.SelectedItem as PipelineNode;
        if (_selectedNode == null)
        {
            CmbTargetParam.ItemsSource = null;
            return;
        }

        CmbTargetParam.ItemsSource = _selectedNode.Tool.Parameters.ToList();
        CmbTargetParam.DisplayMemberPath = "DisplayName";

        if (_selectedNode.Tool.Parameters.Count > 0)
        {
            CmbTargetParam.SelectedIndex = 0;
        }
    }

    private void OnTargetParamChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbTargetParam.SelectedItem is ToolParameter p)
        {
            TxtMin.Text = (p.Min ?? 0.0).ToString();
            TxtMax.Text = (p.Max ?? 255.0).ToString();
            TxtStep.Text = (p.Step ?? 10.0).ToString();
        }
    }

    private void OnBrowseGoodClick(object sender, RoutedEventArgs e)
    {
        var ofd = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Chọn thư mục chứa ảnh mẫu ĐẠT (Good / OK)"
        };
        if (ofd.ShowDialog() == true)
        {
            TxtGoodPath.Text = ofd.FolderName;
        }
    }

    private void OnBrowseBadClick(object sender, RoutedEventArgs e)
    {
        var ofd = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Chọn thư mục chứa ảnh mẫu LỖI (Bad / NG)"
        };
        if (ofd.ShowDialog() == true)
        {
            TxtBadPath.Text = ofd.FolderName;
        }
    }

    private async void OnRunAutoTuneClick(object sender, RoutedEventArgs e)
    {
        if (_selectedNode == null)
        {
            MessageBox.Show("Vui lòng chọn khối công cụ mục tiêu.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (CmbTargetParam.SelectedItem is not ToolParameter param)
        {
            MessageBox.Show("Vui lòng chọn tham số cần tối ưu hóa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string goodDir = TxtGoodPath.Text;
        string badDir = TxtBadPath.Text;

        if (!Directory.Exists(goodDir) || !Directory.Exists(badDir))
        {
            MessageBox.Show("Vui lòng chọn thư mục ảnh mẫu Good và Bad hợp lệ.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!double.TryParse(TxtMin.Text, out double minVal) ||
            !double.TryParse(TxtMax.Text, out double maxVal) ||
            !double.TryParse(TxtStep.Text, out double stepVal))
        {
            MessageBox.Show("Giá trị khoảng tìm kiếm (Min, Max, Step) không hợp lệ.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        BtnRunAutoTune.IsEnabled = false;
        TxtStatus.Text = "Đang nạp ảnh mẫu và khám phá không gian tham số...";

        try
        {
            var samples = await Task.Run(() =>
            {
                var list = new List<AutoTuneSample>();
                var exts = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp" };

                foreach (var ext in exts)
                {
                    foreach (var f in Directory.GetFiles(goodDir, ext))
                    {
                        var mat = Cv2.ImRead(f, ImreadModes.Unchanged);
                        if (!mat.Empty()) list.Add(new AutoTuneSample(mat, isGood: true, name: Path.GetFileName(f)));
                    }
                    foreach (var f in Directory.GetFiles(badDir, ext))
                    {
                        var mat = Cv2.ImRead(f, ImreadModes.Unchanged);
                        if (!mat.Empty()) list.Add(new AutoTuneSample(mat, isGood: false, name: Path.GetFileName(f)));
                    }
                }
                return list;
            });

            if (samples.Count(s => s.IsGood) == 0 || samples.Count(s => !s.IsGood) == 0)
            {
                MessageBox.Show("Mỗi thư mục phải có ít nhất 1 tệp ảnh hợp lệ để học phân biệt.", "Thiếu dữ liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var paramSpec = new AutoTuneParameterSpec(param.Name, minVal, maxVal, stepVal, param.Type);
            var metricSpec = new AutoTuneMetricSpec(
                outputPortName: TxtMetricPort.Text.Trim(),
                higherIsBetter: true);

            var engine = new AutoTuneEngine();
            var report = await Task.Run(() => engine.Optimize(_selectedNode.Tool, samples, new[] { paramSpec }, metricSpec));

            _lastReport = report;

            // Update UI badges
            TxtAccuracy.Text = $"{report.Accuracy * 100:F1}%";
            TxtF1.Text = $"{report.F1Score * 100:F1}%";
            TxtMargin.Text = $"{report.TunedSeparationMargin:F2}";
            TxtThreshold.Text = $"{report.RecommendedThreshold:F2}";

            TxtReport.Text = report.Summary;
            BtnApplyToNode.IsEnabled = true;
            TxtStatus.Text = $"Hoàn tất Auto-Tune! Đã tìm thấy điểm tối ưu (F1={report.F1Score * 100:F1}%).";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi trong quá trình Auto-Tune: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            TxtStatus.Text = "Auto-Tune thất bại.";
        }
        finally
        {
            BtnRunAutoTune.IsEnabled = true;
        }
    }

    private void OnApplyClick(object sender, RoutedEventArgs e)
    {
        if (_selectedNode == null || _lastReport == null) return;

        foreach (var (pName, tunedVal) in _lastReport.TunedParameters)
        {
            var targetParam = _selectedNode.Tool.Parameters.FirstOrDefault(p => p.Name.Equals(pName, StringComparison.OrdinalIgnoreCase));
            if (targetParam != null)
            {
                targetParam.Value = tunedVal;
            }
        }

        _selectedNode.MarkDirty();
        MessageBox.Show($"Đã áp dụng các tham số tối ưu vào khối '{_selectedNode.Title}' thành công!", "Đã áp dụng", MessageBoxButton.OK, MessageBoxImage.Information);
        Close();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
