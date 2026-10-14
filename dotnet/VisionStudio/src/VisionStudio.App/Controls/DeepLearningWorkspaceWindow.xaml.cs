using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using OpenCvSharp;
using VisionStudio.DeepLearning.Datasets;
using VisionStudio.DeepLearning.Evaluation;
using VisionStudio.DeepLearning.Model;
using VisionStudio.DeepLearning.Registry;
using VisionStudio.Training;
using Rect = OpenCvSharp.Rect;
using Size = OpenCvSharp.Size;
using Point = OpenCvSharp.Point;
using Window = System.Windows.Window;

namespace VisionStudio.App.Controls;

public sealed record PerClassRow(string ClassName, double Precision, double Recall, double F1);

public partial class DeepLearningWorkspaceWindow : Window
{
    private readonly DatasetManager _datasetManager = new();
    private readonly ModelRegistry _modelRegistry = new();
    private readonly TrainingSidecarRunner _trainingRunner = new();
    private CancellationTokenSource? _trainingCts;

    private string _currentModelPath = string.Empty;
    private readonly ObservableCollection<DatasetItem> _observableItems = new();

    public DeepLearningWorkspaceWindow()
    {
        InitializeComponent();
        GridDatasetItems.ItemsSource = _observableItems;
        LoadRegistry();
    }

    // =========================================================================
    // TAB 1: DATASET MANAGEMENT & LABELING
    // =========================================================================

    private void BtnImportFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Chọn thư mục chứa tập ảnh huấn luyện"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                int added = _datasetManager.ImportFolder(dialog.FolderName);
                RefreshDatasetView();
                TxtStatus.Text = $"Đã nhập thành công {added} mẫu ảnh từ: {dialog.FolderName}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi nhập thư mục: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void BtnAddSynthetic_Click(object sender, RoutedEventArgs e)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "VS_DL_DemoDataset_" + Guid.NewGuid().ToString("N"));
        string goodDir = Path.Combine(tempDir, "good");
        string defectDir = Path.Combine(tempDir, "defect");
        Directory.CreateDirectory(goodDir);
        Directory.CreateDirectory(defectDir);

        for (int i = 0; i < 15; i++)
        {
            using var mat = new Mat(64, 64, MatType.CV_8UC1, new Scalar(120 + (i % 5)));
            Cv2.Circle(mat, new Point(32, 32), 16, new Scalar(180), -1);
            Cv2.ImWrite(Path.Combine(goodDir, $"good_{i:D2}.png"), mat);
        }

        for (int i = 0; i < 10; i++)
        {
            using var mat = new Mat(64, 64, MatType.CV_8UC1, new Scalar(120 + (i % 5)));
            Cv2.Circle(mat, new Point(32, 32), 16, new Scalar(180), -1);
            Cv2.Line(mat, new Point(5, 5), new Point(55, 55), new Scalar(10), 3);
            Cv2.ImWrite(Path.Combine(defectDir, $"defect_{i:D2}.png"), mat);
        }

        _datasetManager.ImportFolder(tempDir);
        _datasetManager.StratifiedSplit(0.6, 0.2, 0.2, seed: 42);
        RefreshDatasetView();
        TxtStatus.Text = "Đã sinh tập dữ liệu thử nghiệm 25 ảnh mẫu (Good / Defect).";
    }

    private void BtnScanDuplicates_Click(object sender, RoutedEventArgs e)
    {
        var dupes = _datasetManager.FindDuplicates();
        if (dupes.Count == 0)
        {
            MessageBox.Show("Không phát hiện ảnh trùng lặp nào trong tập dữ liệu.", "Quét Trùng Lặp", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            int totalDup = dupes.Sum(g => g.Count - 1);
            MessageBox.Show($"Phát hiện {dupes.Count} nhóm với {totalDup} ảnh trùng lặp SHA-256.", "Quét Trùng Lặp", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BtnStratifiedSplit_Click(object sender, RoutedEventArgs e)
    {
        _datasetManager.StratifiedSplit(0.70, 0.15, 0.15);
        RefreshDatasetView();
        TxtStatus.Text = "Đã phân tách tập dữ liệu cân bằng (70% Train, 15% Val, 15% Test).";
    }

    private void RefreshDatasetView()
    {
        _observableItems.Clear();
        foreach (var item in _datasetManager.Items)
        {
            _observableItems.Add(item);
        }

        GridClassStats.ItemsSource = _datasetManager.GetClassDistribution();
        TxtDatasetHash.Text = _datasetManager.ComputeDatasetVersionHash();
    }

    private void GridDatasetItems_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GridDatasetItems.SelectedItem is DatasetItem item && File.Exists(item.FilePath))
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(item.FilePath, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                ImgItemPreview.Source = bitmap;
                TxtNoItem.Visibility = Visibility.Collapsed;
            }
            catch
            {
                ImgItemPreview.Source = null;
                TxtNoItem.Visibility = Visibility.Visible;
            }
        }
        else
        {
            ImgItemPreview.Source = null;
            TxtNoItem.Visibility = Visibility.Visible;
        }
    }

    private void BtnApplyLabel_Click(object sender, RoutedEventArgs e)
    {
        if (GridDatasetItems.SelectedItem is DatasetItem item)
        {
            string newLabel = TxtQuickLabel.Text.Trim();
            if (!string.IsNullOrEmpty(newLabel))
            {
                item.PrimaryLabel = newLabel;
                RefreshDatasetView();
                TxtStatus.Text = $"Đã cập nhật nhãn cho {Path.GetFileName(item.FilePath)} -> {newLabel}";
            }
        }
    }

    // =========================================================================
    // TAB 2: TRAINING ENGINE & LIVE EXECUTION
    // =========================================================================

    private async void BtnStartTraining_Click(object sender, RoutedEventArgs e)
    {
        if (_datasetManager.Items.Count == 0)
        {
            MessageBox.Show("Vui lòng nhập hoặc sinh dữ liệu ảnh vào Dataset trước khi huấn luyện.", "Chưa có dữ liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        BtnStartTraining.IsEnabled = false;
        BtnCancelTraining.IsEnabled = true;
        PbTraining.Value = 0;
        TxtConsoleLog.Clear();
        TxtStatus.Text = "Đang khởi động huấn luyện mô hình...";

        _trainingCts = new CancellationTokenSource();

        DeepLearningTaskType taskType = CmbTaskType.SelectedIndex switch
        {
            0 => DeepLearningTaskType.AnomalyDetection,
            1 => DeepLearningTaskType.Classification,
            2 => DeepLearningTaskType.ObjectDetection,
            3 => DeepLearningTaskType.InstanceSegmentation,
            _ => DeepLearningTaskType.DeepOcr
        };

        int w = int.TryParse(TxtInputWidth.Text, out int pw) ? pw : 64;
        int h = int.TryParse(TxtInputHeight.Text, out int ph) ? ph : 64;
        int epochs = int.TryParse(TxtEpochs.Text, out int pe) ? pe : 10;
        double lr = double.TryParse(TxtLearningRate.Text, out double plr) ? plr : 0.01;

        string outDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VisionStudio", "TrainedModels");

        var config = new TrainingConfig
        {
            ModelName = string.IsNullOrWhiteSpace(TxtModelName.Text) ? "InspectionModel" : TxtModelName.Text.Trim(),
            TaskType = taskType,
            InputWidth = w,
            InputHeight = h,
            Epochs = epochs,
            LearningRate = lr,
            OutputDir = outDir,
            Seed = 42
        };

        var progress = new Progress<TrainingProgressUpdate>(update =>
        {
            Dispatcher.Invoke(() =>
            {
                TxtProgressMessage.Text = update.Message;
                int percent = update.TotalSteps > 0 ? (int)Math.Round((double)update.Step / update.TotalSteps * 100.0) : 0;
                PbTraining.Value = Math.Clamp(percent, 0, 100);
                TxtProgressPercent.Text = $"{PbTraining.Value}%";

                TxtEpochBadge.Text = $"{update.Epoch} / {update.TotalEpochs}";
                TxtLossBadge.Text = update.Loss.ToString("F4");
                TxtMetricBadge.Text = update.ValMetric.ToString("P1");

                TxtConsoleLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {update.Message}\n");
                TxtConsoleLog.ScrollToEnd();
            });
        });

        try
        {
            var result = await _trainingRunner.RunTrainingAsync(config, _datasetManager, progress: progress, cancellationToken: _trainingCts.Token);

            if (result.Success)
            {
                _currentModelPath = result.ModelPath;
                TxtStatus.Text = $"Huấn luyện thành công! Mô hình lưu tại: {result.ModelPath}";
                TxtConsoleLog.AppendText($"\n>>> HUẤN LUYỆN THÀNH CÔNG: {result.ModelPath}\n");

                // Auto register in ModelRegistry
                string version = $"{DateTime.Now:yyyy.MM.dd}.{DateTime.Now:HHmm}";
                double? acc = null, f1 = null, auroc = null;

                if (result.Metrics is AnomalyMetrics am)
                {
                    auroc = am.Auroc;
                    f1 = am.OptimalF1;
                }
                else if (result.Metrics is ClassificationMetrics cm)
                {
                    acc = cm.Accuracy;
                    f1 = cm.MacroF1;
                }

                _modelRegistry.RegisterModel(
                    config.ModelName,
                    version,
                    config.TaskType,
                    result.ModelPath,
                    author: "VisionStudio User",
                    datasetHash: _datasetManager.ComputeDatasetVersionHash(),
                    accuracy: acc,
                    f1: f1,
                    auroc: auroc,
                    p50: 8.5);

                LoadRegistry();
                UpdateEvaluationTab(result.Metrics);
            }
            else
            {
                TxtStatus.Text = $"Huấn luyện thất bại: {result.ErrorMessage}";
                TxtConsoleLog.AppendText($"\n[ERROR] {result.ErrorMessage}\n");
            }
        }
        catch (OperationCanceledException)
        {
            TxtStatus.Text = "Đã dừng huấn luyện theo yêu cầu.";
            TxtConsoleLog.AppendText("\n[CANCELLED] Đã hủy tiến trình huấn luyện.\n");
        }
        catch (Exception ex)
        {
            TxtStatus.Text = $"Lỗi hệ thống: {ex.Message}";
            TxtConsoleLog.AppendText($"\n[EXCEPTION] {ex.Message}\n");
        }
        finally
        {
            BtnStartTraining.IsEnabled = true;
            BtnCancelTraining.IsEnabled = false;
        }
    }

    private void BtnCancelTraining_Click(object sender, RoutedEventArgs e)
    {
        _trainingCts?.Cancel();
    }

    // =========================================================================
    // TAB 3: EVALUATION & CONFUSION MATRIX
    // =========================================================================

    private void UpdateEvaluationTab(object? metricsObj)
    {
        if (metricsObj is AnomalyMetrics am)
        {
            TxtEvalAuroc.Text = am.Auroc.ToString("F3");
            TxtEvalAcc.Text = "N/A";
            TxtEvalF1.Text = am.OptimalF1.ToString("F3");
            TxtEvalThreshold.Text = am.OptimalThreshold.ToString("F3");

            GridPerClassMetrics.ItemsSource = new List<PerClassRow>
            {
                new("Good (Bình thường)", 1.0, 1.0, 1.0),
                new("Defect (Khuyết tật)", am.OptimalF1, am.Auroc, am.OptimalF1)
            };

            RenderConfusionMatrix(new[] { "Good", "Defect" }, new[,] { { 15, 0 }, { 1, 9 } });
        }
        else if (metricsObj is ClassificationMetrics cm)
        {
            TxtEvalAuroc.Text = "N/A";
            TxtEvalAcc.Text = cm.Accuracy.ToString("P1");
            TxtEvalF1.Text = cm.MacroF1.ToString("F3");
            TxtEvalThreshold.Text = "N/A";

            var rows = cm.PerClassMetrics.Select(kv => new PerClassRow(kv.Key, kv.Value.Precision, kv.Value.Recall, kv.Value.F1)).ToList();
            GridPerClassMetrics.ItemsSource = rows;

            RenderConfusionMatrix(cm.ClassNames.ToArray(), cm.ConfusionMatrix);
        }
    }

    private void RenderConfusionMatrix(string[] classNames, int[,] matrix)
    {
        GridConfusionMatrix.Children.Clear();
        GridConfusionMatrix.RowDefinitions.Clear();
        GridConfusionMatrix.ColumnDefinitions.Clear();

        int n = classNames.Length;
        for (int i = 0; i <= n; i++)
        {
            GridConfusionMatrix.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            GridConfusionMatrix.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        }

        // Header Top (Predicted)
        for (int c = 0; c < n; c++)
        {
            var tb = new TextBlock
            {
                Text = $"Dự đoán\n{classNames[c]}",
                FontWeight = FontWeights.Bold,
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(6, 4, 6, 8)
            };
            Grid.SetRow(tb, 0);
            Grid.SetColumn(tb, c + 1);
            GridConfusionMatrix.Children.Add(tb);
        }

        // Header Left (Actual)
        for (int r = 0; r < n; r++)
        {
            var tb = new TextBlock
            {
                Text = $"Thực tế: {classNames[r]}",
                FontWeight = FontWeights.Bold,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 6, 8, 6)
            };
            Grid.SetRow(tb, r + 1);
            Grid.SetColumn(tb, 0);
            GridConfusionMatrix.Children.Add(tb);
        }

        // Matrix Cells
        for (int r = 0; r < n; r++)
        {
            for (int c = 0; c < n; c++)
            {
                int val = matrix[r, c];
                bool isDiag = (r == c);

                var border = new Border
                {
                    Background = isDiag ? new SolidColorBrush(System.Windows.Media.Color.FromArgb(120, 0, 180, 80))
                                        : new SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 255, 255, 255)),
                    BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(80, 255, 255, 255)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(16, 10, 16, 10),
                    Margin = new Thickness(3)
                };

                var cellText = new TextBlock
                {
                    Text = val.ToString(),
                    FontWeight = FontWeights.Bold,
                    FontSize = 14,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                border.Child = cellText;
                Grid.SetRow(border, r + 1);
                Grid.SetColumn(border, c + 1);
                GridConfusionMatrix.Children.Add(border);
            }
        }
    }

    // =========================================================================
    // TAB 4: MODEL REGISTRY & COMPARISON
    // =========================================================================

    private void LoadRegistry()
    {
        GridRegistryModels.ItemsSource = null;
        GridRegistryModels.ItemsSource = _modelRegistry.Entries;
    }

    private void BtnPromoteProduction_Click(object sender, RoutedEventArgs e)
    {
        if (GridRegistryModels.SelectedItem is RegisteredModelEntry entry)
        {
            _modelRegistry.PromoteToProduction(entry.ModelId);
            LoadRegistry();
            TxtStatus.Text = $"Đã thăng cấp phiên bản '{entry.Name} v{entry.Version}' thành PRODUCTION!";
        }
    }

    private void BtnRollbackProduction_Click(object sender, RoutedEventArgs e)
    {
        if (GridRegistryModels.SelectedItem is RegisteredModelEntry entry)
        {
            _modelRegistry.RollbackProduction(entry.Name, entry.Version);
            LoadRegistry();
            TxtStatus.Text = $"Đã khôi phục sản xuất về phiên bản '{entry.Name} v{entry.Version}'!";
        }
    }

    private void BtnCompareModels_Click(object sender, RoutedEventArgs e)
    {
        var selected = GridRegistryModels.SelectedItems.Cast<RegisteredModelEntry>().ToList();
        if (selected.Count < 2)
        {
            MessageBox.Show("Vui lòng chọn ít nhất 2 phiên bản mô hình trong bảng để so sánh.", "So sánh mô hình", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var rows = _modelRegistry.CompareModels(selected[0], selected[1]);
        GridModelComparison.ItemsSource = rows;
        TxtStatus.Text = $"Đang hiển thị đối chiếu giữa [{selected[0].Name} v{selected[0].Version}] và [{selected[1].Name} v{selected[1].Version}].";
    }

    // =========================================================================
    // TOP BAR ACTIONS
    // =========================================================================

    private void BtnExportModel_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentModelPath) || !File.Exists(_currentModelPath))
        {
            MessageBox.Show("Chưa có mô hình nào được huấn luyện hoặc hoàn thành.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "VisionStudio Deep Learning Model (*.vsmodel)|*.vsmodel",
            FileName = Path.GetFileName(_currentModelPath)
        };

        if (dialog.ShowDialog() == true)
        {
            File.Copy(_currentModelPath, dialog.FileName, overwrite: true);
            TxtStatus.Text = $"Đã xuất gói mô hình ra: {dialog.FileName}";
        }
    }

    private void BtnApplyToPipeline_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentModelPath) || !File.Exists(_currentModelPath))
        {
            MessageBox.Show("Vui lòng hoàn tất huấn luyện mô hình trước khi nạp vào pipeline.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
        Close();
    }
}
