using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VisionStudio.App.Converters;
using VisionStudio.App.ViewModels;

namespace VisionStudio.App.Controls;

public partial class ImageViewerControl : UserControl
{
    private Point _lastPanPoint;
    private bool _isPanning;
    private Matrix _matrix = Matrix.Identity;

    // Interactive drawing states
    private bool _isInteracting;
    private Point _startImgPt;
    private Point _currentImgPt;
    private readonly List<Point> _polygonPoints = new();
    private bool _isSyncingMode;

    public ImageViewerControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        FitToWindow();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is INotifyPropertyChanged oldVm)
        {
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (e.NewValue is INotifyPropertyChanged newVm)
        {
            newVm.PropertyChanged += OnViewModelPropertyChanged;
            SyncModeFromViewModel();
            RenderOverlays();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DesignViewModel.DisplayedImage))
        {
            Dispatcher.InvokeAsync(() =>
            {
                RenderOverlays();
            });
        }
        else if (e.PropertyName == nameof(DesignViewModel.DisplayedOverlays))
        {
            Dispatcher.InvokeAsync(RenderOverlays);
        }
        else if (e.PropertyName == nameof(DesignViewModel.ActiveInteractionMode))
        {
            Dispatcher.InvokeAsync(SyncModeFromViewModel);
        }
    }

    private void SyncModeFromViewModel()
    {
        if (DataContext is not DesignViewModel vm || _isSyncingMode) return;

        try
        {
            _isSyncingMode = true;
            switch (vm.ActiveInteractionMode)
            {
                case InteractiveToolMode.RectangleRoi:
                    if (ToolRoiBtn != null) ToolRoiBtn.IsChecked = true;
                    ViewportBorder.Cursor = Cursors.Cross;
                    break;
                case InteractiveToolMode.LineCaliper:
                    if (ToolLineBtn != null) ToolLineBtn.IsChecked = true;
                    ViewportBorder.Cursor = Cursors.Pen;
                    break;
                case InteractiveToolMode.PointPicker:
                    if (ToolPointBtn != null) ToolPointBtn.IsChecked = true;
                    ViewportBorder.Cursor = Cursors.Cross;
                    break;
                case InteractiveToolMode.PolygonRoi:
                    if (ToolPolygonBtn != null) ToolPolygonBtn.IsChecked = true;
                    ViewportBorder.Cursor = Cursors.Cross;
                    break;
                default:
                    if (ToolPanBtn != null) ToolPanBtn.IsChecked = true;
                    ViewportBorder.Cursor = Cursors.Arrow;
                    break;
            }
        }
        finally
        {
            _isSyncingMode = false;
        }
    }

    private void OnInteractiveToolChanged(object sender, RoutedEventArgs e)
    {
        if (_isSyncingMode || DataContext is not DesignViewModel vm) return;

        if (ToolRoiBtn?.IsChecked == true)
        {
            vm.SetInteractionMode(InteractiveToolMode.RectangleRoi);
        }
        else if (ToolLineBtn?.IsChecked == true)
        {
            vm.SetInteractionMode(InteractiveToolMode.LineCaliper);
        }
        else if (ToolPointBtn?.IsChecked == true)
        {
            vm.SetInteractionMode(InteractiveToolMode.PointPicker);
        }
        else if (ToolPolygonBtn?.IsChecked == true)
        {
            vm.SetInteractionMode(InteractiveToolMode.PolygonRoi);
        }
        else
        {
            vm.SetInteractionMode(InteractiveToolMode.PanZoom);
        }

        ClearPreviews();
    }

    private void ClearPreviews()
    {
        if (RoiPreviewRect != null) RoiPreviewRect.Visibility = Visibility.Collapsed;
        if (LinePreviewCaliper != null) LinePreviewCaliper.Visibility = Visibility.Collapsed;
        if (LineStartMarker != null) LineStartMarker.Visibility = Visibility.Collapsed;
        if (LineEndMarker != null) LineEndMarker.Visibility = Visibility.Collapsed;
        if (PointReticleCircle != null) PointReticleCircle.Visibility = Visibility.Collapsed;
        if (PointReticleH != null) PointReticleH.Visibility = Visibility.Collapsed;
        if (PointReticleV != null) PointReticleV.Visibility = Visibility.Collapsed;
        if (PolygonPreview != null) PolygonPreview.Visibility = Visibility.Collapsed;
        if (DimensionBadge != null) DimensionBadge.Visibility = Visibility.Collapsed;
        _polygonPoints.Clear();
    }

    private void RenderOverlays()
    {
        if (OverlayHost == null) return;

        using var dc = OverlayHost.DrawingVisual.RenderOpen();

        if (ShowOverlaysCheck?.IsChecked == true && DataContext is DesignViewModel vm && vm.DisplayedOverlays != null)
        {
            var zoom = _matrix.M11;
            OverlayDrawingHelper.RenderOverlays(dc, vm.DisplayedOverlays, zoom);
        }
    }

    private void OnOverlaysCheckChanged(object sender, RoutedEventArgs e)
    {
        RenderOverlays();
    }

    #region Zoom & Pan & Interactive Drawing

    private void OnViewportMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var mousePos = e.GetPosition(ViewportBorder);
        var zoomFactor = e.Delta > 0 ? 1.2 : (1.0 / 1.2);
        ApplyZoom(zoomFactor, mousePos);
    }

    private void ApplyZoom(double factor, Point center)
    {
        var currentZoom = _matrix.M11;
        var newZoom = currentZoom * factor;
        if (newZoom < 0.05 || newZoom > 50.0) return;

        _matrix.ScaleAt(factor, factor, center.X, center.Y);
        CanvasTransform.Matrix = _matrix;

        UpdateZoomText();
        RenderOverlays();
    }

    private void UpdateZoomText()
    {
        if (ZoomText != null)
        {
            ZoomText.Text = $"{(_matrix.M11 * 100):F0}%";
        }
    }

    private Point ScreenToImagePoint(Point screenPos, BitmapSource? bmp)
    {
        var invMatrix = _matrix;
        if (invMatrix.HasInverse)
        {
            invMatrix.Invert();
            var imgPt = invMatrix.Transform(screenPos);
            if (bmp != null)
            {
                imgPt.X = Math.Clamp(imgPt.X, 0, bmp.PixelWidth);
                imgPt.Y = Math.Clamp(imgPt.Y, 0, bmp.PixelHeight);
            }
            return imgPt;
        }
        return screenPos;
    }

    private void OnViewportMouseDown(object sender, MouseButtonEventArgs e)
    {
        var screenPoint = e.GetPosition(ViewportBorder);
        var bmp = MainImage.Source as BitmapSource;
        var imgPt = ScreenToImagePoint(screenPoint, bmp);
        var vm = DataContext as DesignViewModel;

        // Middle button or Space+Left click: Always Pan
        if (e.ChangedButton == MouseButton.Middle || (e.ChangedButton == MouseButton.Left && Keyboard.IsKeyDown(Key.Space)))
        {
            _isPanning = true;
            _lastPanPoint = screenPoint;
            ViewportBorder.CaptureMouse();
            ViewportBorder.Cursor = Cursors.SizeAll;
            return;
        }

        if (e.ChangedButton == MouseButton.Left && vm != null)
        {
            switch (vm.ActiveInteractionMode)
            {
                case InteractiveToolMode.RectangleRoi:
                    _isInteracting = true;
                    _startImgPt = imgPt;
                    _currentImgPt = imgPt;
                    RoiPreviewRect.Visibility = Visibility.Visible;
                    Canvas.SetLeft(RoiPreviewRect, imgPt.X);
                    Canvas.SetTop(RoiPreviewRect, imgPt.Y);
                    RoiPreviewRect.Width = 0;
                    RoiPreviewRect.Height = 0;
                    DimensionBadge.Visibility = Visibility.Visible;
                    DimensionBadgeText.Text = $"ROI: [{(int)imgPt.X}, {(int)imgPt.Y}]";
                    Canvas.SetLeft(DimensionBadge, imgPt.X);
                    Canvas.SetTop(DimensionBadge, Math.Max(0, imgPt.Y - 26));
                    ViewportBorder.CaptureMouse();
                    break;

                case InteractiveToolMode.LineCaliper:
                    _isInteracting = true;
                    _startImgPt = imgPt;
                    _currentImgPt = imgPt;
                    LinePreviewCaliper.Visibility = Visibility.Visible;
                    LinePreviewCaliper.X1 = imgPt.X;
                    LinePreviewCaliper.Y1 = imgPt.Y;
                    LinePreviewCaliper.X2 = imgPt.X;
                    LinePreviewCaliper.Y2 = imgPt.Y;
                    LineStartMarker.Visibility = Visibility.Visible;
                    LineEndMarker.Visibility = Visibility.Visible;
                    Canvas.SetLeft(LineStartMarker, imgPt.X - 4);
                    Canvas.SetTop(LineStartMarker, imgPt.Y - 4);
                    Canvas.SetLeft(LineEndMarker, imgPt.X - 4);
                    Canvas.SetTop(LineEndMarker, imgPt.Y - 4);
                    DimensionBadge.Visibility = Visibility.Visible;
                    DimensionBadgeText.Text = "Caliper: 0.0 px";
                    Canvas.SetLeft(DimensionBadge, imgPt.X + 8);
                    Canvas.SetTop(DimensionBadge, Math.Max(0, imgPt.Y - 26));
                    ViewportBorder.CaptureMouse();
                    break;

                case InteractiveToolMode.PointPicker:
                    ShowPointReticle(imgPt);
                    vm.ApplyInteractivePoint(imgPt.X, imgPt.Y);
                    break;

                case InteractiveToolMode.PolygonRoi:
                    if (e.ClickCount >= 2 && _polygonPoints.Count >= 3)
                    {
                        // Double click completes polygon
                        vm.ApplyInteractivePolygon(_polygonPoints);
                        _polygonPoints.Clear();
                        PolygonPreview.Visibility = Visibility.Collapsed;
                        DimensionBadge.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        _polygonPoints.Add(imgPt);
                        PolygonPreview.Points = new PointCollection(_polygonPoints);
                        PolygonPreview.Visibility = Visibility.Visible;
                        DimensionBadge.Visibility = Visibility.Visible;
                        DimensionBadgeText.Text = $"Đa giác: {_polygonPoints.Count} đỉnh";
                        Canvas.SetLeft(DimensionBadge, imgPt.X);
                        Canvas.SetTop(DimensionBadge, Math.Max(0, imgPt.Y - 26));
                    }
                    break;

                default: // PanZoom
                    _isPanning = true;
                    _lastPanPoint = screenPoint;
                    ViewportBorder.CaptureMouse();
                    ViewportBorder.Cursor = Cursors.SizeAll;
                    break;
            }
        }
    }

    private void ShowPointReticle(Point pt)
    {
        PointReticleCircle.Visibility = Visibility.Visible;
        PointReticleH.Visibility = Visibility.Visible;
        PointReticleV.Visibility = Visibility.Visible;

        Canvas.SetLeft(PointReticleCircle, pt.X - 10);
        Canvas.SetTop(PointReticleCircle, pt.Y - 10);

        PointReticleH.X1 = pt.X - 18;
        PointReticleH.Y1 = pt.Y;
        PointReticleH.X2 = pt.X + 18;
        PointReticleH.Y2 = pt.Y;

        PointReticleV.X1 = pt.X;
        PointReticleV.Y1 = pt.Y - 18;
        PointReticleV.X2 = pt.X;
        PointReticleV.Y2 = pt.Y + 18;

        DimensionBadge.Visibility = Visibility.Visible;
        DimensionBadgeText.Text = $"Tọa độ: [{(int)pt.X}, {(int)pt.Y}]";
        Canvas.SetLeft(DimensionBadge, pt.X + 12);
        Canvas.SetTop(DimensionBadge, Math.Max(0, pt.Y - 26));
    }

    private void OnViewportMouseMove(object sender, MouseEventArgs e)
    {
        var screenPoint = e.GetPosition(ViewportBorder);
        var bmp = MainImage.Source as BitmapSource;
        var vm = DataContext as DesignViewModel;

        if (_isPanning)
        {
            var delta = screenPoint - _lastPanPoint;
            _matrix.Translate(delta.X, delta.Y);
            CanvasTransform.Matrix = _matrix;
            _lastPanPoint = screenPoint;
            return;
        }

        // Always sample pixel coordinate at cursor
        if (vm != null && bmp != null)
        {
            var imgPt = ScreenToImagePoint(screenPoint, bmp);
            vm.SamplePixelAt((int)Math.Floor(imgPt.X), (int)Math.Floor(imgPt.Y));

            if (_isInteracting)
            {
                _currentImgPt = imgPt;

                if (vm.ActiveInteractionMode == InteractiveToolMode.RectangleRoi)
                {
                    var rx = Math.Min(_startImgPt.X, imgPt.X);
                    var ry = Math.Min(_startImgPt.Y, imgPt.Y);
                    var rw = Math.Abs(imgPt.X - _startImgPt.X);
                    var rh = Math.Abs(imgPt.Y - _startImgPt.Y);

                    Canvas.SetLeft(RoiPreviewRect, rx);
                    Canvas.SetTop(RoiPreviewRect, ry);
                    RoiPreviewRect.Width = rw;
                    RoiPreviewRect.Height = rh;

                    Canvas.SetLeft(DimensionBadge, rx);
                    Canvas.SetTop(DimensionBadge, Math.Max(0, ry - 26));
                    DimensionBadgeText.Text = $"ROI: {(int)rw} x {(int)rh} px [X:{(int)rx}, Y:{(int)ry}]";
                }
                else if (vm.ActiveInteractionMode == InteractiveToolMode.LineCaliper)
                {
                    LinePreviewCaliper.X2 = imgPt.X;
                    LinePreviewCaliper.Y2 = imgPt.Y;
                    Canvas.SetLeft(LineEndMarker, imgPt.X - 4);
                    Canvas.SetTop(LineEndMarker, imgPt.Y - 4);

                    var dx = imgPt.X - _startImgPt.X;
                    var dy = imgPt.Y - _startImgPt.Y;
                    var len = Math.Sqrt(dx * dx + dy * dy);

                    var midX = (_startImgPt.X + imgPt.X) / 2.0;
                    var midY = (_startImgPt.Y + imgPt.Y) / 2.0;
                    Canvas.SetLeft(DimensionBadge, midX + 8);
                    Canvas.SetTop(DimensionBadge, Math.Max(0, midY - 26));
                    DimensionBadgeText.Text = $"Caliper: {len:F1} px";
                }
            }
        }
    }

    private void OnViewportMouseUp(object sender, MouseButtonEventArgs e)
    {
        var vm = DataContext as DesignViewModel;

        if (_isPanning)
        {
            _isPanning = false;
            ViewportBorder.ReleaseMouseCapture();
            SyncModeFromViewModel();
            return;
        }

        if (_isInteracting && vm != null)
        {
            _isInteracting = false;
            ViewportBorder.ReleaseMouseCapture();

            if (vm.ActiveInteractionMode == InteractiveToolMode.RectangleRoi)
            {
                var rx = Math.Min(_startImgPt.X, _currentImgPt.X);
                var ry = Math.Min(_startImgPt.Y, _currentImgPt.Y);
                var rw = Math.Abs(_currentImgPt.X - _startImgPt.X);
                var rh = Math.Abs(_currentImgPt.Y - _startImgPt.Y);

                if (rw >= 4 && rh >= 4)
                {
                    vm.ApplyInteractiveRoi(rx, ry, rw, rh);
                }
            }
            else if (vm.ActiveInteractionMode == InteractiveToolMode.LineCaliper)
            {
                var dx = _currentImgPt.X - _startImgPt.X;
                var dy = _currentImgPt.Y - _startImgPt.Y;
                var len = Math.Sqrt(dx * dx + dy * dy);

                if (len >= 3)
                {
                    vm.ApplyInteractiveLine(_startImgPt.X, _startImgPt.Y, _currentImgPt.X, _currentImgPt.Y);
                }
            }
        }
    }

    public void FitToWindow()
    {
        if (MainImage.Source is not BitmapSource bmp || ViewportBorder.ActualWidth <= 0 || ViewportBorder.ActualHeight <= 0)
        {
            _matrix = Matrix.Identity;
            CanvasTransform.Matrix = _matrix;
            UpdateZoomText();
            return;
        }

        var w = bmp.PixelWidth;
        var h = bmp.PixelHeight;

        var scaleX = (ViewportBorder.ActualWidth - 40) / w;
        var scaleY = (ViewportBorder.ActualHeight - 60) / h;
        var scale = Math.Min(scaleX, scaleY);
        if (scale <= 0) scale = 1.0;

        var offsetX = (ViewportBorder.ActualWidth - (w * scale)) / 2.0;
        var offsetY = 40 + (ViewportBorder.ActualHeight - 40 - (h * scale)) / 2.0;

        _matrix = new Matrix(scale, 0, 0, scale, offsetX, offsetY);
        CanvasTransform.Matrix = _matrix;
        UpdateZoomText();
        RenderOverlays();
    }

    public void ActualSize()
    {
        if (MainImage.Source is not BitmapSource bmp)
        {
            _matrix = Matrix.Identity;
        }
        else
        {
            var offsetX = (ViewportBorder.ActualWidth - bmp.PixelWidth) / 2.0;
            var offsetY = 40 + (ViewportBorder.ActualHeight - 40 - bmp.PixelHeight) / 2.0;
            _matrix = new Matrix(1.0, 0, 0, 1.0, Math.Max(10, offsetX), Math.Max(40, offsetY));
        }

        CanvasTransform.Matrix = _matrix;
        UpdateZoomText();
        RenderOverlays();
    }

    private void OnFitClick(object sender, RoutedEventArgs e) => FitToWindow();
    private void OnActualSizeClick(object sender, RoutedEventArgs e) => ActualSize();
    private void OnZoomInClick(object sender, RoutedEventArgs e) => ApplyZoom(1.25, new Point(ViewportBorder.ActualWidth / 2.0, ViewportBorder.ActualHeight / 2.0));
    private void OnZoomOutClick(object sender, RoutedEventArgs e) => ApplyZoom(1.0 / 1.25, new Point(ViewportBorder.ActualWidth / 2.0, ViewportBorder.ActualHeight / 2.0));

    #endregion
}
