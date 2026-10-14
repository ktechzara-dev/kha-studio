using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using VisionStudio.Matching.Engine;
using VisionStudio.Matching.Models;
using VisionStudio.Matching.Training;
using Point = System.Windows.Point;
using Rect = OpenCvSharp.Rect;
using Window = System.Windows.Window;

namespace VisionStudio.App.Controls;

/// <summary>
/// Industrial Geometric Model Editor allowing engineers to train, mask, set origins,
/// inspect extracted contour features, and tune matching thresholds.
/// </summary>
public partial class GeometricModelEditorWindow : Window
{
    private Mat? _sourceMat;
    private Mat? _maskMat;
    private GeometricModel? _model;
    private readonly GeometricMatcher _matcher = new();

    private bool _isDragging;
    private Point _dragStart;

    public GeometricModel? TrainedModel => _model;

    public GeometricModelEditorWindow(Mat? initialMat = null)
    {
        InitializeComponent();

        if (initialMat != null && !initialMat.Empty())
        {
            SetImage(initialMat.Clone());
        }
        else
        {
            // Create a default synthetic part bracket so engineer can immediately test
            SetImage(CreateDefaultBracketMat());
        }
    }

    private static Mat CreateDefaultBracketMat(int size = 280)
    {
        var mat = new Mat(size, size, MatType.CV_8UC1, new Scalar(25));
        OpenCvSharp.Point[] bracket = {
            new(35, 35),
            new(245, 35),
            new(245, 105),
            new(105, 105),
            new(105, 245),
            new(35, 245)
        };
        Cv2.FillPoly(mat, new[] { bracket }, new Scalar(225));
        Cv2.Circle(mat, new OpenCvSharp.Point(70, 70), 20, new Scalar(25), -1);
        Cv2.Circle(mat, new OpenCvSharp.Point(175, 70), 16, new Scalar(25), -1);
        Cv2.Circle(mat, new OpenCvSharp.Point(70, 175), 14, new Scalar(25), -1);
        Cv2.Rectangle(mat, new Rect(95, 35, 20, 25), new Scalar(25), -1);
        return mat;
    }

    public void SetImage(Mat mat)
    {
        _sourceMat?.Dispose();
        _sourceMat = mat;

        _maskMat?.Dispose();
        _maskMat = new Mat(_sourceMat.Rows, _sourceMat.Cols, MatType.CV_8UC1, new Scalar(255));

        // Default ROI in center
        int roiW = Math.Min(180, _sourceMat.Cols - 40);
        int roiH = Math.Min(180, _sourceMat.Rows - 40);
        int roiX = (_sourceMat.Cols - roiW) / 2;
        int roiY = (_sourceMat.Rows - roiH) / 2;

        TxtRoiX.Text = roiX.ToString();
        TxtRoiY.Text = roiY.ToString();
        TxtRoiW.Text = roiW.ToString();
        TxtRoiH.Text = roiH.ToString();

        TxtOriginX.Text = (roiW / 2).ToString();
        TxtOriginY.Text = (roiH / 2).ToString();

        RefreshView();
        TxtStatus.Text = $"Đã nạp ảnh kích thước {_sourceMat.Cols}x{_sourceMat.Rows}. Hãy điều chỉnh ROI và nhấn Huấn luyện.";
    }

    private void RefreshView()
    {
        if (_sourceMat == null) return;

        // Render base image to Wpf Image
        ImgDisplay.Source = _sourceMat.ToBitmapSource();
        OverlayCanvas.Width = _sourceMat.Cols;
        OverlayCanvas.Height = _sourceMat.Rows;

        RedrawOverlays();
    }

    private void RedrawOverlays()
    {
        OverlayCanvas.Children.Clear();
        if (_sourceMat == null) return;

        var roi = GetCurrentRoi();

        // 1. Draw Training ROI Rectangle (Yellow dashed)
        var roiRect = new System.Windows.Shapes.Rectangle
        {
            Width = roi.Width,
            Height = roi.Height,
            Stroke = new SolidColorBrush(Color.FromArgb(240, 255, 215, 0)),
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 4, 2 },
            Fill = new SolidColorBrush(Color.FromArgb(20, 255, 215, 0))
        };
        Canvas.SetLeft(roiRect, roi.X);
        Canvas.SetTop(roiRect, roi.Y);
        OverlayCanvas.Children.Add(roiRect);

        // 2. Draw Trained Points if model exists
        if (_model != null && _model.BasePoints.Count > 0)
        {
            float ox = roi.X + GetOriginOffset().X;
            float oy = roi.Y + GetOriginOffset().Y;

            foreach (var p in _model.BasePoints)
            {
                float px = ox + p.X;
                float py = oy + p.Y;

                var ptDot = new Ellipse
                {
                    Width = 3,
                    Height = 3,
                    Fill = Brushes.Cyan
                };
                Canvas.SetLeft(ptDot, px - 1.5);
                Canvas.SetTop(ptDot, py - 1.5);
                OverlayCanvas.Children.Add(ptDot);
            }
        }

        // 3. Draw Origin Marker (Magenta cross with Red X axis and Green Y axis)
        float originGlobalX = roi.X + GetOriginOffset().X;
        float originGlobalY = roi.Y + GetOriginOffset().Y;

        var originDot = new Ellipse
        {
            Width = 8,
            Height = 8,
            Fill = Brushes.Magenta,
            Stroke = Brushes.White,
            StrokeThickness = 1
        };
        Canvas.SetLeft(originDot, originGlobalX - 4);
        Canvas.SetTop(originDot, originGlobalY - 4);
        OverlayCanvas.Children.Add(originDot);

        // X Axis arrow (Red)
        var lineX = new Line
        {
            X1 = originGlobalX,
            Y1 = originGlobalY,
            X2 = originGlobalX + 25,
            Y2 = originGlobalY,
            Stroke = Brushes.Red,
            StrokeThickness = 2
        };
        OverlayCanvas.Children.Add(lineX);

        // Y Axis arrow (Green)
        var lineY = new Line
        {
            X1 = originGlobalX,
            Y1 = originGlobalY,
            X2 = originGlobalX,
            Y2 = originGlobalY + 25,
            Stroke = Brushes.LimeGreen,
            StrokeThickness = 2
        };
        OverlayCanvas.Children.Add(lineY);
    }

    private Rect GetCurrentRoi()
    {
        int.TryParse(TxtRoiX.Text, out int x);
        int.TryParse(TxtRoiY.Text, out int y);
        int.TryParse(TxtRoiW.Text, out int w);
        int.TryParse(TxtRoiH.Text, out int h);

        if (_sourceMat != null)
        {
            w = Math.Clamp(w, 8, _sourceMat.Cols);
            h = Math.Clamp(h, 8, _sourceMat.Rows);
            x = Math.Clamp(x, 0, _sourceMat.Cols - w);
            y = Math.Clamp(y, 0, _sourceMat.Rows - h);
        }

        return new Rect(x, y, w, h);
    }

    private Point2f GetOriginOffset()
    {
        float.TryParse(TxtOriginX.Text, out float ox);
        float.TryParse(TxtOriginY.Text, out float oy);
        return new Point2f(ox, oy);
    }

    private void BtnTrain_Click(object sender, RoutedEventArgs e)
    {
        if (_sourceMat == null) return;

        try
        {
            var roi = GetCurrentRoi();
            var origin = GetOriginOffset();

            var options = new GeometricModelTrainerOptions
            {
                MinContrast = (float)SliderMinContrast.Value,
                HighContrast = (float)SliderMinContrast.Value * 2.2f,
                MaxPoints = (int)SliderMaxPoints.Value,
                PyramidLevels = CmbPyramids.SelectedIndex + 1,
                CustomOrigin = origin
            };

            var sw = Stopwatch.StartNew();
            _model = GeometricModelTrainer.Train(_sourceMat, roi, _maskMat, options, TxtModelName.Text);
            sw.Stop();

            string stats = $"✅ Huấn luyện thành công trong {sw.Elapsed.TotalMilliseconds:F1} ms\n" +
                           $"• Tổng số điểm Level 0: {_model.PointCount} pts\n";

            for (int i = 1; i < _model.PyramidLevels.Count; i++)
            {
                stats += $"• Level {i} (1/{1 << i} scale): {_model.PyramidLevels[i].Points.Count} pts\n";
            }
            stats += $"• Gốc toạ độ: ({_model.OriginX:F1}, {_model.OriginY:F1}) tương đối ROI";

            TxtModelStats.Text = stats;
            TxtStatus.Text = $"Mô hình '{_model.Name}' được huấn luyện thành công ({_model.PointCount} điểm).";
            RedrawOverlays();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi huấn luyện mô hình: {ex.Message}", "Lỗi Huấn Luyện", MessageBoxButton.OK, MessageBoxImage.Error);
            TxtStatus.Text = $"Lỗi: {ex.Message}";
        }
    }

    private void BtnTestMatch_Click(object sender, RoutedEventArgs e)
    {
        if (_sourceMat == null || _model == null)
        {
            MessageBox.Show("Vui lòng huấn luyện mô hình trước khi thử nghiệm kiểm tra khớp mẫu.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            float.TryParse(TxtAngleMin.Text, out float aMin);
            float.TryParse(TxtAngleMax.Text, out float aMax);

            var options = new GeometricMatcherOptions
            {
                MinScore = (float)SliderMinScore.Value,
                MaxInstances = 5,
                AngleMinDeg = aMin,
                AngleMaxDeg = aMax,
                AllowContrastReversal = ChkContrastReversal.IsChecked == true,
                SubPixelRefinement = ChkSubPixel.IsChecked == true
            };

            var report = _matcher.Match(_sourceMat, _model, options);

            RedrawOverlays();

            if (report.Success)
            {
                foreach (var inst in report.Instances)
                {
                    // Render instance bounding corners
                    var poly = new Polygon
                    {
                        Stroke = Brushes.Lime,
                        StrokeThickness = 2.5,
                        Fill = new SolidColorBrush(Color.FromArgb(30, 0, 255, 0))
                    };
                    foreach (var c in inst.BoundingCorners)
                    {
                        poly.Points.Add(new Point(c.X, c.Y));
                    }
                    OverlayCanvas.Children.Add(poly);

                    // Instance center cross
                    var marker = new Ellipse
                    {
                        Width = 10,
                        Height = 10,
                        Fill = Brushes.Yellow,
                        Stroke = Brushes.Black,
                        StrokeThickness = 1.5
                    };
                    Canvas.SetLeft(marker, inst.X - 5);
                    Canvas.SetTop(marker, inst.Y - 5);
                    OverlayCanvas.Children.Add(marker);

                    // Score label
                    var lbl = new TextBlock
                    {
                        Text = $"{inst.Score:P1} ({inst.AngleDeg:F1}°)",
                        FontWeight = FontWeights.Bold,
                        FontSize = 12,
                        Foreground = Brushes.Yellow,
                        Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                        Padding = new Thickness(4, 1, 4, 1)
                    };
                    Canvas.SetLeft(lbl, inst.BoundingCorners[0].X);
                    Canvas.SetTop(lbl, inst.BoundingCorners[0].Y - 20);
                    OverlayCanvas.Children.Add(lbl);
                }

                TxtStatus.Text = $"Tìm thấy {report.MatchCount} cá thể! Điểm cao nhất: {report.BestMatch?.Score:P1} tại ({report.BestMatch?.X:F1}, {report.BestMatch?.Y:F1}, {report.BestMatch?.AngleDeg:F1}°). Thời gian: {report.ExecutionTimeMs:F1} ms.";
            }
            else
            {
                TxtStatus.Text = $"Không tìm thấy mẫu thoả mãn ngưỡng {options.MinScore:P0} trong thời gian {report.ExecutionTimeMs:F1} ms.";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi kiểm thử khớp mẫu: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnSaveModel_Click(object sender, RoutedEventArgs e)
    {
        if (_model == null)
        {
            MessageBox.Show("Chưa có mô hình nào được huấn luyện để lưu.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var sfd = new SaveFileDialog
        {
            Filter = "VisionStudio Geometric Model (*.vsgeom)|*.vsgeom|All Files (*.*)|*.*",
            FileName = $"{_model.Name}.vsgeom",
            DefaultExt = ".vsgeom"
        };

        if (sfd.ShowDialog() == true)
        {
            _model.Name = TxtModelName.Text;
            _model.SaveToFile(sfd.FileName);
            TxtStatus.Text = $"Đã lưu mô hình thành công: {sfd.FileName}";
        }
    }

    private void BtnLoadModel_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog
        {
            Filter = "VisionStudio Geometric Model (*.vsgeom)|*.vsgeom|All Files (*.*)|*.*"
        };

        if (ofd.ShowDialog() == true)
        {
            try
            {
                _model = GeometricModel.LoadFromFile(ofd.FileName);
                TxtModelName.Text = _model.Name;
                TxtRoiW.Text = ((int)_model.Width).ToString();
                TxtRoiH.Text = ((int)_model.Height).ToString();
                TxtOriginX.Text = _model.OriginX.ToString("F1");
                TxtOriginY.Text = _model.OriginY.ToString("F1");

                TxtModelStats.Text = $"📥 Nạp thành công tệp: {System.IO.Path.GetFileName(ofd.FileName)}\n" +
                                     $"• Tên: {_model.Name}\n" +
                                     $"• Điểm Level 0: {_model.PointCount} pts\n" +
                                     $"• Gốc toạ độ: ({_model.OriginX:F1}, {_model.OriginY:F1})";

                TxtStatus.Text = $"Đã nạp mô hình {_model.Name} ({_model.PointCount} điểm).";
                RedrawOverlays();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi nạp mô hình: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void BtnOpenImage_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog
        {
            Filter = "Ảnh công nghiệp (*.bmp;*.png;*.jpg;*.jpeg;*.tif)|*.bmp;*.png;*.jpg;*.jpeg;*.tif|All Files (*.*)|*.*"
        };

        if (ofd.ShowDialog() == true)
        {
            try
            {
                using var loaded = Cv2.ImRead(ofd.FileName, ImreadModes.Color);
                if (loaded.Empty())
                {
                    MessageBox.Show("Không thể giải mã tệp ảnh.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                SetImage(loaded.Clone());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi mở ảnh: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void BtnCenterOrigin_Click(object sender, RoutedEventArgs e)
    {
        var roi = GetCurrentRoi();
        TxtOriginX.Text = (roi.Width / 2.0f).ToString("F1");
        TxtOriginY.Text = (roi.Height / 2.0f).ToString("F1");
        RedrawOverlays();
    }

    private void BtnClearMask_Click(object sender, RoutedEventArgs e)
    {
        if (_sourceMat != null)
        {
            _maskMat?.Dispose();
            _maskMat = new Mat(_sourceMat.Rows, _sourceMat.Cols, MatType.CV_8UC1, new Scalar(255));
            TxtStatus.Text = "Đã làm trống mặt nạ loại trừ (Mask).";
            RedrawOverlays();
        }
    }

    private void BtnResetView_Click(object sender, RoutedEventArgs e)
    {
        RedrawOverlays();
    }

    private void SliderMinContrast_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (LblMinContrast != null) LblMinContrast.Text = e.NewValue.ToString("F1");
    }

    private void SliderMaxPoints_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (LblMaxPoints != null) LblMaxPoints.Text = ((int)e.NewValue).ToString();
    }

    private void SliderMinScore_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (LblMinScore != null) LblMinScore.Text = $"{e.NewValue:P0}";
    }

    private void Viewport_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_sourceMat == null) return;
        var pt = e.GetPosition(OverlayCanvas);

        if (RbSetOrigin.IsChecked == true)
        {
            var roi = GetCurrentRoi();
            float ox = (float)(pt.X - roi.X);
            float oy = (float)(pt.Y - roi.Y);
            TxtOriginX.Text = ox.ToString("F1");
            TxtOriginY.Text = oy.ToString("F1");
            RedrawOverlays();
            return;
        }

        if (RbDrawRoi.IsChecked == true || RbDrawMask.IsChecked == true)
        {
            _isDragging = true;
            _dragStart = pt;
        }
    }

    private void Viewport_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging || _sourceMat == null) return;
        var pt = e.GetPosition(OverlayCanvas);

        if (RbDrawRoi.IsChecked == true)
        {
            int x = (int)Math.Min(_dragStart.X, pt.X);
            int y = (int)Math.Min(_dragStart.Y, pt.Y);
            int w = (int)Math.Abs(pt.X - _dragStart.X);
            int h = (int)Math.Abs(pt.Y - _dragStart.Y);

            if (w > 8 && h > 8)
            {
                TxtRoiX.Text = x.ToString();
                TxtRoiY.Text = y.ToString();
                TxtRoiW.Text = w.ToString();
                TxtRoiH.Text = h.ToString();
                RedrawOverlays();
            }
        }
        else if (RbDrawMask.IsChecked == true && _maskMat != null)
        {
            // Paint mask with circular eraser
            Cv2.Circle(_maskMat, new OpenCvSharp.Point((int)pt.X, (int)pt.Y), 16, new Scalar(0), -1);
            RedrawOverlays();
        }
    }

    private void Viewport_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        RedrawOverlays();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _sourceMat?.Dispose();
        _maskMat?.Dispose();
    }
}
