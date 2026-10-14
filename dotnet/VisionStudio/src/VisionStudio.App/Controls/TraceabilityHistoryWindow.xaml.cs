using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using OpenCvSharp;
using VisionStudio.Core.Pipeline;
using VisionStudio.Core.Testing;
using VisionStudio.Core.Traceability;
using Window = System.Windows.Window;

namespace VisionStudio.App.Controls;

public sealed record MeasurementDisplayItem(string Key, double Value);

public partial class TraceabilityHistoryWindow : Window
{
    private readonly TraceabilityStore _traceStore;
    private readonly PipelineGraph _graph;
    private IReadOnlyList<InspectionRecord> _currentRecords = Array.Empty<InspectionRecord>();

    public TraceabilityHistoryWindow(TraceabilityStore? store = null, PipelineGraph? graph = null)
    {
        InitializeComponent();
        _traceStore = store ?? new TraceabilityStore();
        _graph = graph ?? new PipelineGraph();

        EnsureSampleRecords();
        SearchRecords();
        VerifyIntegrity();
    }

    private void EnsureSampleRecords()
    {
        if (_traceStore.Records.Count == 0)
        {
            // Seed a few demo records
            string demoImg = Path.Combine(_traceStore.ImagesDirectory, "demo_pass.png");
            if (!File.Exists(demoImg))
            {
                using var mat = new Mat(100, 100, MatType.CV_8UC3, new Scalar(40, 180, 50));
                Cv2.PutText(mat, "PASS", new OpenCvSharp.Point(20, 60), HersheyFonts.HersheySimplex, 1.0, new Scalar(255, 255, 255), 2);
                Cv2.ImWrite(demoImg, mat);
            }

            _traceStore.AppendRecord(new InspectionRecord
            {
                LotNumber = "LOT-2026-X1",
                SerialNumber = "SN-10029",
                PartType = "Cap-500",
                RecipeName = "Chai Tiêu Chuẩn 500ml",
                Verdict = InspectionVerdict.Pass,
                ExecutionTimeMs = 11.8,
                ImagePath = demoImg,
                Measurements = new() { ["Diameter"] = 28.02, ["Roundness"] = 0.99 }
            });

            _traceStore.AppendRecord(new InspectionRecord
            {
                LotNumber = "LOT-2026-X1",
                SerialNumber = "SN-10030",
                PartType = "Cap-500",
                RecipeName = "Chai Tiêu Chuẩn 500ml",
                Verdict = InspectionVerdict.Fail,
                ExecutionTimeMs = 13.2,
                ImagePath = demoImg,
                Measurements = new() { ["Diameter"] = 29.45, ["Roundness"] = 0.81 },
                FailureReasons = new() { "Đường kính vượt giới hạn dung sai (> 28.5mm)" }
            });

            _traceStore.AppendRecord(new InspectionRecord
            {
                LotNumber = "LOT-2026-X2",
                SerialNumber = "SN-10031",
                PartType = "Cap-1500",
                RecipeName = "Chai Lớn 1500ml",
                Verdict = InspectionVerdict.Pass,
                ExecutionTimeMs = 12.1,
                ImagePath = demoImg,
                Measurements = new() { ["Diameter"] = 38.01, ["Roundness"] = 0.98 }
            });
        }
    }

    private void SearchRecords()
    {
        var filter = new TraceFilter
        {
            LotNumber = string.IsNullOrWhiteSpace(TxtFilterLot.Text) ? null : TxtFilterLot.Text.Trim(),
            SerialNumber = string.IsNullOrWhiteSpace(TxtFilterSerial.Text) ? null : TxtFilterSerial.Text.Trim(),
            RecipeId = string.IsNullOrWhiteSpace(TxtFilterRecipe.Text) ? null : TxtFilterRecipe.Text.Trim(),
            Verdict = CmbFilterVerdict.SelectedIndex switch
            {
                1 => InspectionVerdict.Pass,
                2 => InspectionVerdict.Fail,
                3 => InspectionVerdict.Warning,
                _ => null
            }
        };

        _currentRecords = _traceStore.QueryRecords(filter);
        GridRecords.ItemsSource = _currentRecords;
        TxtRecordCount.Text = $"Hiển thị: {_currentRecords.Count} bản ghi";
        TxtStatus.Text = $"Tìm thấy {_currentRecords.Count} bản ghi kiểm định.";

        if (_currentRecords.Count > 0 && GridRecords.SelectedItem == null)
        {
            GridRecords.SelectedIndex = 0;
        }
    }

    private void VerifyIntegrity()
    {
        var val = _traceStore.ValidateHashChain();
        if (val.IsValid)
        {
            BrdIntegrityBadge.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(27, 77, 32));
            BrdIntegrityBadge.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(56, 142, 60));
            TxtIntegrityBadge.Text = $"🟢 CHUỖI BĂM TOÀN VẸN ({val.TotalRecords} BẢN GHI TAMPER-FREE)";
            TxtIntegrityBadge.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(160, 255, 160));
        }
        else
        {
            BrdIntegrityBadge.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(90, 20, 20));
            BrdIntegrityBadge.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 40, 40));
            TxtIntegrityBadge.Text = $"🔴 PHÁT HIỆN CAN THIỆP TẠI BẢN GHI #{val.TamperedIndex + 1}!";
            TxtIntegrityBadge.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 120, 120));
        }
    }

    private void BtnSearch_Click(object sender, RoutedEventArgs e)
    {
        SearchRecords();
    }

    private void BtnVerifyChain_Click(object sender, RoutedEventArgs e)
    {
        VerifyIntegrity();
        var val = _traceStore.ValidateHashChain();
        if (val.IsValid)
        {
            MessageBox.Show($"Xác thực thành công! Toàn bộ {val.TotalRecords} bản ghi kiểm tra giữ nguyên tính toàn vẹn mật mã SHA-256.", "Xác Thực Chuỗi Băm", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show($"CẢNH BÁO: Phát hiện dữ liệu bị chỉnh sửa trái phép!\n{val.Message}", "Cảnh Báo Gian Lận", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnExportCsv_Click(object sender, RoutedEventArgs e)
    {
        if (_currentRecords.Count == 0)
        {
            MessageBox.Show("Không có bản ghi nào để xuất.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "CSV File (*.csv)|*.csv",
            FileName = $"InspectionTrace_{DateTime.Now:yyyyMMdd_HHmm}.csv",
            Title = "Xuất báo cáo truy xuất nguồn gốc ra CSV"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                _traceStore.ExportToCsv(dialog.FileName, _currentRecords);
                TxtStatus.Text = $"Đã xuất báo cáo CSV thành công ra: {dialog.FileName}";
                MessageBox.Show($"Báo cáo CSV đã được lưu thành công tại:\n{dialog.FileName}", "Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi xuất CSV: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void BtnReplayBatch_Click(object sender, RoutedEventArgs e)
    {
        if (_currentRecords.Count == 0)
        {
            MessageBox.Show("Không có bản ghi nào để chạy replay.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        BtnReplayBatch.IsEnabled = false;
        TxtStatus.Text = "Đang chạy Replay ngoại tuyến qua Pipeline...";

        try
        {
            var executor = new PipelineExecutor();
            var replayEngine = new OfflineReplayEngine();
            var report = await replayEngine.ReplayTraceBatchAsync(executor, _graph, _currentRecords);

            TxtStatus.Text = $"Hoàn thành Replay! Tỷ lệ đồng thuận: {report.AgreementRate:P1} ({report.MatchingVerdicts}/{report.TotalReplayed}).";
            MessageBox.Show($"Kết quả Replay ngoại tuyến:\n- Tổng số ảnh: {report.TotalReplayed}\n- Kết quả trùng khớp: {report.MatchingVerdicts}\n- Sai lệch phát hiện: {report.RegressionsDetected}\n- Tỷ lệ đồng thuận: {report.AgreementRate:P1}", "Báo Cáo Replay", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi chạy Replay: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BtnReplayBatch.IsEnabled = true;
        }
    }

    private void GridRecords_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridRecords.SelectedItem is InspectionRecord r)
        {
            // Measurements
            var items = r.Measurements.Select(kv => new MeasurementDisplayItem(kv.Key, kv.Value)).ToList();
            GridMeasurements.ItemsSource = items;

            // Hash info
            TxtPrevHash.Text = r.PreviousRecordHash;
            TxtCurrHash.Text = r.RecordHash;

            // Image Preview
            if (!string.IsNullOrEmpty(r.ImagePath) && File.Exists(r.ImagePath))
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(r.ImagePath, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();

                    ImgInspectionPreview.Source = bitmap;
                    TxtNoImage.Visibility = Visibility.Collapsed;
                }
                catch
                {
                    ImgInspectionPreview.Source = null;
                    TxtNoImage.Visibility = Visibility.Visible;
                }
            }
            else
            {
                ImgInspectionPreview.Source = null;
                TxtNoImage.Visibility = Visibility.Visible;
            }
        }
    }
}
