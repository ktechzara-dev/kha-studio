using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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

    private enum LineDragMode
    {
        None,
        DragStart,
        DragEnd,
        DragBody
    }
    private LineDragMode _activeLineDragMode = LineDragMode.None;
    private Point _lineDragFixedPt;
    private Point _lineDragP1Orig;
    private Point _lineDragP2Orig;
    private Point _lineDragStartMouse;

    public ImageViewerControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
        VideoTimelineSlider.AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler(OnSliderDragStarted));
        VideoTimelineSlider.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(OnSliderDragCompleted));
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

    private void UpdateCursorForMode(InteractiveToolMode mode)
    {
        switch (mode)
        {
            case InteractiveToolMode.RectangleRoi:
            case InteractiveToolMode.TeachTarget:
            case InteractiveToolMode.PointPicker:
            case InteractiveToolMode.PolygonRoi:
                ViewportBorder.Cursor = Cursors.Cross;
                break;
            case InteractiveToolMode.Eraser:
                ViewportBorder.Cursor = Cursors.Hand;
                break;
            case InteractiveToolMode.LineCaliper:
                ViewportBorder.Cursor = Cursors.Pen;
                break;
            default:
                ViewportBorder.Cursor = Cursors.Arrow;
                break;
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
                    break;
                case InteractiveToolMode.TeachTarget:
                    if (ToolTeachBtn != null) ToolTeachBtn.IsChecked = true;
                    break;
                case InteractiveToolMode.Eraser:
                    if (ToolEraserBtn != null) ToolEraserBtn.IsChecked = true;
                    break;
                case InteractiveToolMode.LineCaliper:
                    if (ToolLineBtn != null) ToolLineBtn.IsChecked = true;
                    break;
                case InteractiveToolMode.PointPicker:
                    if (ToolPointBtn != null) ToolPointBtn.IsChecked = true;
                    break;
                case InteractiveToolMode.PolygonRoi:
                    if (ToolPolygonBtn != null) ToolPolygonBtn.IsChecked = true;
                    break;
                default:
                    if (ToolPanBtn != null) ToolPanBtn.IsChecked = true;
                    break;
            }
            UpdateCursorForMode(vm.ActiveInteractionMode);
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
        else if (ToolTeachBtn?.IsChecked == true)
        {
            vm.SetInteractionMode(InteractiveToolMode.TeachTarget);
        }
        else if (ToolEraserBtn?.IsChecked == true)
        {
            vm.SetInteractionMode(InteractiveToolMode.Eraser);
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

    private void OnEraseRoiClick(object sender, RoutedEventArgs e)
    {
        ClearPreviews();
        if (ToolPanBtn != null) ToolPanBtn.IsChecked = true;
        if (DataContext is DesignViewModel vm)
        {
            vm.EraseSelectedNodeScanRoi();
        }
    }

    private void OnClearRoiClick(object sender, RoutedEventArgs e)
    {
        ClearPreviews();
        if (ToolPanBtn != null) ToolPanBtn.IsChecked = true;
        if (DataContext is DesignViewModel vm)
        {
            vm.ClearSelectedNodeRoi();
        }
    }

    private bool _isUserDraggingSlider = false;

    private void OnSliderDragStarted(object sender, DragStartedEventArgs e)
    {
        _isUserDraggingSlider = true;
    }

    private void OnSliderDragCompleted(object sender, DragCompletedEventArgs e)
    {
        _isUserDraggingSlider = false;
        if (VideoTimelineSlider != null && DataContext is DesignViewModel vm && vm.HasVideoSource)
        {
            vm.SeekVideoTo((int)VideoTimelineSlider.Value);
        }
    }

    private void OnSliderPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        _isUserDraggingSlider = true;
    }

    private void OnSliderPreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isUserDraggingSlider)
        {
            _isUserDraggingSlider = false;
            if (VideoTimelineSlider != null && DataContext is DesignViewModel vm && vm.HasVideoSource)
            {
                vm.SeekVideoTo((int)VideoTimelineSlider.Value);
            }
        }
    }

    private void OnVideoSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUserDraggingSlider && DataContext is DesignViewModel vm && vm.HasVideoSource)
        {
            // Only update time readout preview while dragging, NEVER hammer the video decoder
            vm.PreviewSeekTime((int)e.NewValue);
        }
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

    private static double DistancePointToSegment(Point pt, Point a, Point b)
    {
        double dx = b.X - a.X;
        double dy = b.Y - a.Y;
        double l2 = dx * dx + dy * dy;
        if (l2 < 1e-4)
        {
            return Math.Sqrt((pt.X - a.X) * (pt.X - a.X) + (pt.Y - a.Y) * (pt.Y - a.Y));
        }

        double t = Math.Clamp(((pt.X - a.X) * dx + (pt.Y - a.Y) * dy) / l2, 0.0, 1.0);
        double projX = a.X + t * dx;
        double projY = a.Y + t * dy;
        return Math.Sqrt((pt.X - projX) * (pt.X - projX) + (pt.Y - projY) * (pt.Y - projY));
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
            // First: Check if user is clicking on an existing line handle or line body to drag it directly!
            if (vm.SelectedNodeSupportsLine)
            {
                var linePts = vm.GetSelectedNodeLinePoints();
                if (linePts.HasValue)
                {
                    var lp1 = linePts.Value.P1;
                    var lp2 = linePts.Value.P2;
                    double dStart = (imgPt - lp1).Length;
                    double dEnd = (imgPt - lp2).Length;
                    double dBody = DistancePointToSegment(imgPt, lp1, lp2);

                    if (dStart <= 16.0)
                    {
                        _isInteracting = true;
                        _activeLineDragMode = LineDragMode.DragStart;
                        _lineDragFixedPt = lp2;
                        _currentImgPt = imgPt;
                        LinePreviewCaliper.Visibility = Visibility.Visible;
                        LineStartMarker.Visibility = Visibility.Visible;
                        LineEndMarker.Visibility = Visibility.Visible;
                        Canvas.SetLeft(LineStartMarker, imgPt.X - 5);
                        Canvas.SetTop(LineStartMarker, imgPt.Y - 5);
                        Canvas.SetLeft(LineEndMarker, lp2.X - 5);
                        Canvas.SetTop(LineEndMarker, lp2.Y - 5);
                        LinePreviewCaliper.X1 = imgPt.X; LinePreviewCaliper.Y1 = imgPt.Y;
                        LinePreviewCaliper.X2 = lp2.X; LinePreviewCaliper.Y2 = lp2.Y;
                        DimensionBadge.Visibility = Visibility.Visible;
                        var isCounter = vm.SelectedNodeIsCounter;
                        DimensionBadgeText.Text = isCounter ? $"VẠCH ĐẾM: {(lp2 - imgPt).Length:F1} px" : $"Caliper: {(lp2 - imgPt).Length:F1} px";
                        var mid = new Point((imgPt.X + lp2.X) / 2.0, (imgPt.Y + lp2.Y) / 2.0);
                        Canvas.SetLeft(DimensionBadge, mid.X + 8);
                        Canvas.SetTop(DimensionBadge, Math.Max(0, mid.Y - 26));
                        ViewportBorder.CaptureMouse();
                        return;
                    }
                    else if (dEnd <= 16.0)
                    {
                        _isInteracting = true;
                        _activeLineDragMode = LineDragMode.DragEnd;
                        _lineDragFixedPt = lp1;
                        _currentImgPt = imgPt;
                        LinePreviewCaliper.Visibility = Visibility.Visible;
                        LineStartMarker.Visibility = Visibility.Visible;
                        LineEndMarker.Visibility = Visibility.Visible;
                        Canvas.SetLeft(LineStartMarker, lp1.X - 5);
                        Canvas.SetTop(LineStartMarker, lp1.Y - 5);
                        Canvas.SetLeft(LineEndMarker, imgPt.X - 5);
                        Canvas.SetTop(LineEndMarker, imgPt.Y - 5);
                        LinePreviewCaliper.X1 = lp1.X; LinePreviewCaliper.Y1 = lp1.Y;
                        LinePreviewCaliper.X2 = imgPt.X; LinePreviewCaliper.Y2 = imgPt.Y;
                        DimensionBadge.Visibility = Visibility.Visible;
                        var isCounter = vm.SelectedNodeIsCounter;
                        DimensionBadgeText.Text = isCounter ? $"VẠCH ĐẾM: {(imgPt - lp1).Length:F1} px" : $"Caliper: {(imgPt - lp1).Length:F1} px";
                        var mid = new Point((lp1.X + imgPt.X) / 2.0, (lp1.Y + imgPt.Y) / 2.0);
                        Canvas.SetLeft(DimensionBadge, mid.X + 8);
                        Canvas.SetTop(DimensionBadge, Math.Max(0, mid.Y - 26));
                        ViewportBorder.CaptureMouse();
                        return;
                    }
                    else if (dBody <= 10.0)
                    {
                        _isInteracting = true;
                        _activeLineDragMode = LineDragMode.DragBody;
                        _lineDragStartMouse = imgPt;
                        _lineDragP1Orig = lp1;
                        _lineDragP2Orig = lp2;
                        _currentImgPt = imgPt;
                        LinePreviewCaliper.Visibility = Visibility.Visible;
                        LineStartMarker.Visibility = Visibility.Visible;
                        LineEndMarker.Visibility = Visibility.Visible;
                        Canvas.SetLeft(LineStartMarker, lp1.X - 5);
                        Canvas.SetTop(LineStartMarker, lp1.Y - 5);
                        Canvas.SetLeft(LineEndMarker, lp2.X - 5);
                        Canvas.SetTop(LineEndMarker, lp2.Y - 5);
                        LinePreviewCaliper.X1 = lp1.X; LinePreviewCaliper.Y1 = lp1.Y;
                        LinePreviewCaliper.X2 = lp2.X; LinePreviewCaliper.Y2 = lp2.Y;
                        DimensionBadge.Visibility = Visibility.Visible;
                        var isCounter = vm.SelectedNodeIsCounter;
                        DimensionBadgeText.Text = isCounter ? "DI CHUYỂN VẠCH ĐẾM" : "DI CHUYỂN THƯỚC";
                        var mid = new Point((lp1.X + lp2.X) / 2.0, (lp1.Y + lp2.Y) / 2.0);
                        Canvas.SetLeft(DimensionBadge, mid.X + 8);
                        Canvas.SetTop(DimensionBadge, Math.Max(0, mid.Y - 26));
                        ViewportBorder.CaptureMouse();
                        return;
                    }
                }
            }

            switch (vm.ActiveInteractionMode)
            {
                case InteractiveToolMode.RectangleRoi:
                    _isInteracting = true;
                    _startImgPt = imgPt;
                    _currentImgPt = imgPt;
                    RoiPreviewRect.Stroke = (Brush)new BrushConverter().ConvertFromString("#ffaa00")!;
                    RoiPreviewRect.Fill = (Brush)new BrushConverter().ConvertFromString("#33ffaa00")!;
                    RoiPreviewRect.Visibility = Visibility.Visible;
                    Canvas.SetLeft(RoiPreviewRect, imgPt.X);
                    Canvas.SetTop(RoiPreviewRect, imgPt.Y);
                    RoiPreviewRect.Width = 0;
                    RoiPreviewRect.Height = 0;
                    DimensionBadge.Visibility = Visibility.Visible;
                    DimensionBadge.BorderBrush = (Brush)new BrushConverter().ConvertFromString("#ffaa00")!;
                    DimensionBadgeText.Foreground = (Brush)new BrushConverter().ConvertFromString("#ffaa00")!;
                    DimensionBadgeText.Text = $"VÙNG QUÉT ROI: [{(int)imgPt.X}, {(int)imgPt.Y}]";
                    Canvas.SetLeft(DimensionBadge, imgPt.X);
                    Canvas.SetTop(DimensionBadge, Math.Max(0, imgPt.Y - 26));
                    ViewportBorder.CaptureMouse();
                    break;

                case InteractiveToolMode.TeachTarget:
                    _isInteracting = true;
                    _startImgPt = imgPt;
                    _currentImgPt = imgPt;
                    RoiPreviewRect.Stroke = (Brush)new BrushConverter().ConvertFromString("#00ff88")!;
                    RoiPreviewRect.Fill = (Brush)new BrushConverter().ConvertFromString("#3300ff88")!;
                    RoiPreviewRect.Visibility = Visibility.Visible;
                    Canvas.SetLeft(RoiPreviewRect, imgPt.X);
                    Canvas.SetTop(RoiPreviewRect, imgPt.Y);
                    RoiPreviewRect.Width = 0;
                    RoiPreviewRect.Height = 0;
                    DimensionBadge.Visibility = Visibility.Visible;
                    DimensionBadge.BorderBrush = (Brush)new BrushConverter().ConvertFromString("#00ff88")!;
                    DimensionBadgeText.Foreground = (Brush)new BrushConverter().ConvertFromString("#00ff88")!;
                    DimensionBadgeText.Text = $"MẪU DẠY (TEACH): [{(int)imgPt.X}, {(int)imgPt.Y}]";
                    Canvas.SetLeft(DimensionBadge, imgPt.X);
                    Canvas.SetTop(DimensionBadge, Math.Max(0, imgPt.Y - 26));
                    ViewportBorder.CaptureMouse();
                    break;

                case InteractiveToolMode.Eraser:
                    ClearPreviews();
                    vm.EraseSelectedNodeScanRoi();
                    vm.SetInteractionMode(InteractiveToolMode.PanZoom);
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

                if (_activeLineDragMode != LineDragMode.None)
                {
                    Point p1, p2;
                    if (_activeLineDragMode == LineDragMode.DragStart)
                    {
                        p1 = imgPt;
                        p2 = _lineDragFixedPt;
                    }
                    else if (_activeLineDragMode == LineDragMode.DragEnd)
                    {
                        p1 = _lineDragFixedPt;
                        p2 = imgPt;
                    }
                    else // DragBody
                    {
                        var delta = imgPt - _lineDragStartMouse;
                        p1 = _lineDragP1Orig + delta;
                        p2 = _lineDragP2Orig + delta;
                    }

                    LinePreviewCaliper.X1 = p1.X;
                    LinePreviewCaliper.Y1 = p1.Y;
                    LinePreviewCaliper.X2 = p2.X;
                    LinePreviewCaliper.Y2 = p2.Y;
                    Canvas.SetLeft(LineStartMarker, p1.X - 5);
                    Canvas.SetTop(LineStartMarker, p1.Y - 5);
                    Canvas.SetLeft(LineEndMarker, p2.X - 5);
                    Canvas.SetTop(LineEndMarker, p2.Y - 5);

                    var len = (p2 - p1).Length;
                    var mid = new Point((p1.X + p2.X) / 2.0, (p1.Y + p2.Y) / 2.0);
                    Canvas.SetLeft(DimensionBadge, mid.X + 8);
                    Canvas.SetTop(DimensionBadge, Math.Max(0, mid.Y - 26));

                    if (_activeLineDragMode == LineDragMode.DragBody)
                    {
                        DimensionBadgeText.Text = vm.SelectedNodeIsCounter ? "DI CHUYỂN VẠCH ĐẾM" : "DI CHUYỂN THƯỚC";
                    }
                    else
                    {
                        DimensionBadgeText.Text = vm.SelectedNodeIsCounter ? $"VẠCH ĐẾM: {len:F1} px" : $"Caliper: {len:F1} px";
                    }
                }
                else if (vm.ActiveInteractionMode == InteractiveToolMode.RectangleRoi || vm.ActiveInteractionMode == InteractiveToolMode.TeachTarget)
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
                    DimensionBadgeText.Text = vm.ActiveInteractionMode == InteractiveToolMode.TeachTarget
                        ? $"MẪU DẠY (TEACH): {(int)rw} x {(int)rh} px [X:{(int)rx}, Y:{(int)ry}]"
                        : $"VÙNG QUÉT ROI: {(int)rw} x {(int)rh} px [X:{(int)rx}, Y:{(int)ry}]";
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
                    DimensionBadgeText.Text = vm.SelectedNodeIsCounter ? $"VẠCH ĐẾM: {len:F1} px" : $"Caliper: {len:F1} px";
                }
            }
            else if (!_isPanning && vm.SelectedNodeSupportsLine)
            {
                var linePts = vm.GetSelectedNodeLinePoints();
                if (linePts.HasValue)
                {
                    var lp1 = linePts.Value.P1;
                    var lp2 = linePts.Value.P2;
                    double dStart = (imgPt - lp1).Length;
                    double dEnd = (imgPt - lp2).Length;
                    double dBody = DistancePointToSegment(imgPt, lp1, lp2);

                    if (dStart <= 16.0 || dEnd <= 16.0)
                    {
                        ViewportBorder.Cursor = Cursors.Hand;
                    }
                    else if (dBody <= 10.0)
                    {
                        ViewportBorder.Cursor = Cursors.SizeAll;
                    }
                    else
                    {
                        UpdateCursorForMode(vm.ActiveInteractionMode);
                    }
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

            var currentInteractionMode = vm.ActiveInteractionMode;

            // Hide rubberband preview elements after mouse release so they don't linger on screen!
            if (RoiPreviewRect != null) RoiPreviewRect.Visibility = Visibility.Collapsed;
            if (LinePreviewCaliper != null) LinePreviewCaliper.Visibility = Visibility.Collapsed;
            if (LineStartMarker != null) LineStartMarker.Visibility = Visibility.Collapsed;
            if (LineEndMarker != null) LineEndMarker.Visibility = Visibility.Collapsed;
            if (DimensionBadge != null) DimensionBadge.Visibility = Visibility.Collapsed;

            if (_activeLineDragMode != LineDragMode.None)
            {
                var mode = _activeLineDragMode;
                _activeLineDragMode = LineDragMode.None;

                if (mode == LineDragMode.DragStart)
                {
                    var dx = _lineDragFixedPt.X - _currentImgPt.X;
                    var dy = _lineDragFixedPt.Y - _currentImgPt.Y;
                    if (Math.Sqrt(dx * dx + dy * dy) >= 3)
                    {
                        vm.ApplyInteractiveLine(_currentImgPt.X, _currentImgPt.Y, _lineDragFixedPt.X, _lineDragFixedPt.Y);
                    }
                }
                else if (mode == LineDragMode.DragEnd)
                {
                    var dx = _currentImgPt.X - _lineDragFixedPt.X;
                    var dy = _currentImgPt.Y - _lineDragFixedPt.Y;
                    if (Math.Sqrt(dx * dx + dy * dy) >= 3)
                    {
                        vm.ApplyInteractiveLine(_lineDragFixedPt.X, _lineDragFixedPt.Y, _currentImgPt.X, _currentImgPt.Y);
                    }
                }
                else if (mode == LineDragMode.DragBody)
                {
                    var delta = _currentImgPt - _lineDragStartMouse;
                    var p1 = _lineDragP1Orig + delta;
                    var p2 = _lineDragP2Orig + delta;
                    vm.ApplyInteractiveLine(p1.X, p1.Y, p2.X, p2.Y);
                }
                SyncModeFromViewModel();
                return;
            }

            if (currentInteractionMode == InteractiveToolMode.RectangleRoi)
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
            else if (currentInteractionMode == InteractiveToolMode.TeachTarget)
            {
                var rx = Math.Min(_startImgPt.X, _currentImgPt.X);
                var ry = Math.Min(_startImgPt.Y, _currentImgPt.Y);
                var rw = Math.Abs(_currentImgPt.X - _startImgPt.X);
                var rh = Math.Abs(_currentImgPt.Y - _startImgPt.Y);

                if (rw >= 4 && rh >= 4)
                {
                    vm.ApplyInteractiveTeachTarget(rx, ry, rw, rh);
                }
            }
            else if (currentInteractionMode == InteractiveToolMode.LineCaliper)
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
