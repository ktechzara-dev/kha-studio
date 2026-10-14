using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using VisionStudio.App.ViewModels;

namespace VisionStudio.App.Controls;

public partial class PipelineCanvasControl : UserControl
{
    private NodeViewModel? _draggedNode;
    private Point _nodeDragOffset;
    private bool _isCanvasPanning;
    private Point _lastPanPoint;
    private Matrix _canvasMatrix = Matrix.Identity;

    private PortViewModel? _connectingSourcePort;
    private Point _connectionDragStartViewportPoint;

    public PipelineCanvasControl()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateMinimap();
    }

    private DesignViewModel? ViewModel => DataContext as DesignViewModel;

    #region Node Dragging & Selection

    private void OnNodeMouseDown(object sender, MouseButtonEventArgs e)
    {
        Focus();
        if (sender is FrameworkElement element && element.DataContext is NodeViewModel node)
        {
            if (ViewModel != null)
            {
                ViewModel.SelectedNode = node;
                ViewModel.SelectedConnection = null;
            }
            e.Handled = true;
        }
    }

    private void OnNodeHeaderMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is Button || (e.OriginalSource is DependencyObject dep && FindVisualParent<Button>(dep) != null))
        {
            // Do not capture mouse if clicking buttons (such as delete ✕ or breakpoint)
            return;
        }

        if (e.ChangedButton == MouseButton.Left && sender is FrameworkElement element && element.DataContext is NodeViewModel node)
        {
            Focus();
            _draggedNode = node;
            var mouseCanvasPos = GetCanvasPoint(e.GetPosition(CanvasViewport));
            _nodeDragOffset = new Point(mouseCanvasPos.X - node.X, mouseCanvasPos.Y - node.Y);

            CanvasViewport.CaptureMouse();
            e.Handled = true;
        }
    }

    private void OnDeleteNodeButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is NodeViewModel node)
        {
            ViewModel?.DeleteNode(node);
            e.Handled = true;
        }
    }

    private void OnDeleteNodeMenuItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem)
        {
            var node = menuItem.DataContext as NodeViewModel ?? ViewModel?.SelectedNode;
            if (node != null)
            {
                ViewModel?.DeleteNode(node);
            }
        }
    }

    private void OnToggleBreakpointClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is NodeViewModel node)
        {
            ViewModel?.ToggleBreakpoint(node);
            e.Handled = true;
        }
    }

    private void OnConnectionMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is ConnectionViewModel conn)
        {
            if (ViewModel != null)
            {
                ViewModel.SelectedConnection = conn;
                ViewModel.SelectedNode = null;
            }
            e.Handled = true;
        }
    }

    #endregion

    #region Port Connecting Drag & Drop

    private void OnPortPinMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && sender is FrameworkElement element && element.DataContext is PortViewModel port)
        {
            // If already in click-to-connect mode, complete the connection!
            if (_connectingSourcePort != null && _connectingSourcePort != port)
            {
                if (ViewModel != null && ViewModel.CanConnect(_connectingSourcePort, port))
                {
                    ViewModel.ConnectPorts(_connectingSourcePort, port);
                }
                CancelConnectionDrag();
                e.Handled = true;
                return;
            }

            _connectingSourcePort = port;
            _connectionDragStartViewportPoint = e.GetPosition(CanvasViewport);
            TempConnectionPath.Visibility = Visibility.Visible;
            CanvasViewport.CaptureMouse();
            e.Handled = true;
        }
    }

    private void OnPortPinMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_connectingSourcePort != null && sender is FrameworkElement element && element.DataContext is PortViewModel targetPort)
        {
            if (targetPort != _connectingSourcePort && ViewModel != null && ViewModel.CanConnect(_connectingSourcePort, targetPort))
            {
                ViewModel.ConnectPorts(_connectingSourcePort, targetPort);
            }
            CancelConnectionDrag();
            CanvasViewport.ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    private void CancelConnectionDrag()
    {
        _connectingSourcePort = null;
        TempConnectionPath.Visibility = Visibility.Collapsed;
        TempConnectionPath.Data = null;
    }

    #endregion

    #region Viewport Pan & Zoom & Drag Handling

    private Point GetCanvasPoint(Point viewportPoint)
    {
        var inv = _canvasMatrix;
        if (inv.HasInverse) inv.Invert();
        return inv.Transform(viewportPoint);
    }

    private void OnViewportMouseDown(object sender, MouseButtonEventArgs e)
    {
        Focus();

        if (e.ChangedButton == MouseButton.Middle || e.ChangedButton == MouseButton.Right ||
            (e.ChangedButton == MouseButton.Left && Keyboard.IsKeyDown(Key.Space)))
        {
            _isCanvasPanning = true;
            _lastPanPoint = e.GetPosition(CanvasViewport);
            CanvasViewport.CaptureMouse();
            Cursor = Cursors.SizeAll;
            e.Handled = true;
        }
        else if (e.ChangedButton == MouseButton.Left)
        {
            // If pending click-to-connect mode is active, handle drop/connect or cancel
            if (_connectingSourcePort != null)
            {
                var viewportPoint = e.GetPosition(CanvasViewport);
                var canvasPoint = GetCanvasPoint(viewportPoint);
                var targetPort = FindPortUnderPoint(viewportPoint, canvasPoint);

                if (targetPort != null && targetPort != _connectingSourcePort)
                {
                    if (ViewModel != null && ViewModel.CanConnect(_connectingSourcePort, targetPort))
                    {
                        ViewModel.ConnectPorts(_connectingSourcePort, targetPort);
                    }
                }

                CancelConnectionDrag();
                CanvasViewport.ReleaseMouseCapture();
                e.Handled = true;
                return;
            }

            // Click on empty canvas clears selection
            if (ViewModel != null && !e.Handled)
            {
                ViewModel.SelectedNode = null;
                ViewModel.SelectedConnection = null;
            }
        }
    }

    private void OnViewportMouseMove(object sender, MouseEventArgs e)
    {
        var viewportPoint = e.GetPosition(CanvasViewport);

        // 1. Panning canvas
        if (_isCanvasPanning)
        {
            var delta = viewportPoint - _lastPanPoint;
            _canvasMatrix.Translate(delta.X, delta.Y);
            CanvasTransform.Matrix = _canvasMatrix;
            _lastPanPoint = viewportPoint;
            return;
        }

        // 2. Dragging node
        if (_draggedNode != null)
        {
            var canvasPos = GetCanvasPoint(viewportPoint);
            var newX = canvasPos.X - _nodeDragOffset.X;
            var newY = canvasPos.Y - _nodeDragOffset.Y;

            // Snap to grid (20px)
            if (ViewModel?.IsGridSnapEnabled == true)
            {
                newX = Math.Round(newX / 20.0) * 20.0;
                newY = Math.Round(newY / 20.0) * 20.0;
            }

            _draggedNode.X = Math.Max(10, newX);
            _draggedNode.Y = Math.Max(10, newY);
            _draggedNode.UpdatePortPositions();

            // Update all connected wires
            if (ViewModel != null)
            {
                foreach (var conn in ViewModel.Connections.Where(c => c.SourceNodeId == _draggedNode.NodeId || c.TargetNodeId == _draggedNode.NodeId))
                {
                    conn.UpdatePoints();
                }
            }

            UpdateMinimap();
            return;
        }

        // 3. Dragging connection wire
        if (_connectingSourcePort != null)
        {
            var mouseCanvas = GetCanvasPoint(viewportPoint);
            var start = _connectingSourcePort.CanvasPosition;
            var dx = Math.Max(40.0, Math.Abs(mouseCanvas.X - start.X) * 0.5);
            var cp1 = new Point(start.X + (_connectingSourcePort.IsOutput ? dx : -dx), start.Y);
            var cp2 = new Point(mouseCanvas.X + (_connectingSourcePort.IsOutput ? -dx : dx), mouseCanvas.Y);

            var geo = new PathGeometry();
            var fig = new PathFigure { StartPoint = start, IsFilled = false };
            fig.Segments.Add(new BezierSegment(cp1, cp2, mouseCanvas, true));
            geo.Figures.Add(fig);
            TempConnectionPath.Data = geo;
        }
    }

    private void OnViewportMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isCanvasPanning)
        {
            _isCanvasPanning = false;
            CanvasViewport.ReleaseMouseCapture();
            Cursor = Cursors.Arrow;
        }

        if (_draggedNode != null)
        {
            _draggedNode = null;
            CanvasViewport.ReleaseMouseCapture();
        }

        if (_connectingSourcePort != null)
        {
            var viewportPoint = e.GetPosition(CanvasViewport);
            var canvasPoint = GetCanvasPoint(viewportPoint);
            var targetPort = FindPortUnderPoint(viewportPoint, canvasPoint);

            if (targetPort != null && targetPort != _connectingSourcePort)
            {
                if (ViewModel != null && ViewModel.CanConnect(_connectingSourcePort, targetPort))
                {
                    ViewModel.ConnectPorts(_connectingSourcePort, targetPort);
                }
                CancelConnectionDrag();
                CanvasViewport.ReleaseMouseCapture();
            }
            else
            {
                var dragDist = (viewportPoint - _connectionDragStartViewportPoint).Length;
                if (dragDist > 8)
                {
                    CancelConnectionDrag();
                    CanvasViewport.ReleaseMouseCapture();
                }
                else
                {
                    // Click-to-connect mode: release mouse capture so clicks route to destination ports
                    CanvasViewport.ReleaseMouseCapture();
                }
            }
        }
    }

    private PortViewModel? FindPortUnderPoint(Point viewportPoint, Point canvasPoint)
    {
        PortViewModel? hitPort = null;

        // 1. Visual tree hit-testing
        VisualTreeHelper.HitTest(
            CanvasViewport,
            null,
            result =>
            {
                if (result?.VisualHit is DependencyObject depObj)
                {
                    var elem = depObj as FrameworkElement ?? FindVisualParent<FrameworkElement>(depObj);
                    while (elem != null)
                    {
                        if (elem.DataContext is PortViewModel pvm)
                        {
                            hitPort = pvm;
                            return HitTestResultBehavior.Stop;
                        }
                        elem = VisualTreeHelper.GetParent(elem) as FrameworkElement;
                    }
                }
                return HitTestResultBehavior.Continue;
            },
            new PointHitTestParameters(viewportPoint));

        if (hitPort != null) return hitPort;

        // 2. Proximity geometric snap (expanded snap radius 65 canvas pixels)
        if (ViewModel != null)
        {
            double bestDistSq = 65.0 * 65.0;
            foreach (var node in ViewModel.Nodes)
            {
                if (_connectingSourcePort != null && ReferenceEquals(node, _connectingSourcePort.Node))
                    continue;

                var ports = (_connectingSourcePort?.IsOutput == true) ? node.InputPorts : node.OutputPorts;
                foreach (var port in ports)
                {
                    var dx = port.CanvasPosition.X - canvasPoint.X;
                    var dy = port.CanvasPosition.Y - canvasPoint.Y;
                    var distSq = dx * dx + dy * dy;
                    if (distSq < bestDistSq)
                    {
                        bestDistSq = distSq;
                        hitPort = port;
                    }
                }
            }
        }

        if (hitPort != null) return hitPort;

        // 3. Whole-node drop detection: if dropped directly on a target node, snap to first connectable port
        if (ViewModel != null && _connectingSourcePort != null)
        {
            foreach (var node in ViewModel.Nodes)
            {
                if (ReferenceEquals(node, _connectingSourcePort.Node))
                    continue;

                if (canvasPoint.X >= node.X - 15 && canvasPoint.X <= node.X + NodeViewModel.NodeWidth + 15 &&
                    canvasPoint.Y >= node.Y - 15 && canvasPoint.Y <= node.Y + node.EstimatedHeight + 15)
                {
                    var candidates = _connectingSourcePort.IsOutput ? node.InputPorts : node.OutputPorts;
                    var compatible = candidates.FirstOrDefault(p => ViewModel.CanConnect(_connectingSourcePort, p));
                    if (compatible != null)
                    {
                        return compatible;
                    }
                }
            }
        }

        return hitPort;
    }

    private static T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
    {
        var parentObj = VisualTreeHelper.GetParent(child);
        if (parentObj == null) return null;
        if (parentObj is T parent) return parent;
        return FindVisualParent<T>(parentObj);
    }

    private void OnViewportMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var mousePos = e.GetPosition(CanvasViewport);
        var zoomFactor = e.Delta > 0 ? 1.15 : (1.0 / 1.15);

        var currentZoom = _canvasMatrix.M11;
        var newZoom = currentZoom * zoomFactor;
        if (newZoom < 0.2 || newZoom > 3.0) return;

        _canvasMatrix.ScaleAt(zoomFactor, zoomFactor, mousePos.X, mousePos.Y);
        CanvasTransform.Matrix = _canvasMatrix;
    }

    private void OnResetCanvasViewClick(object sender, RoutedEventArgs e)
    {
        _canvasMatrix = Matrix.Identity;
        CanvasTransform.Matrix = _canvasMatrix;
        UpdateMinimap();
    }

    private void OnCanvasKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (_connectingSourcePort != null)
            {
                CancelConnectionDrag();
                CanvasViewport.ReleaseMouseCapture();
                e.Handled = true;
                return;
            }
        }

        if (e.Key == Key.Delete)
        {
            ViewModel?.DeleteSelected();
            e.Handled = true;
        }
        else if (e.Key == Key.F5)
        {
            _ = ViewModel?.RunPipelineAsync();
            e.Handled = true;
        }
        else if (e.Key == Key.F10)
        {
            _ = ViewModel?.StepPipelineAsync();
            e.Handled = true;
        }
        else if (e.Key == Key.F9 && ViewModel?.SelectedNode != null)
        {
            ViewModel.ToggleBreakpoint(ViewModel.SelectedNode);
            e.Handled = true;
        }
    }

    #endregion

    #region Minimap

    private void UpdateMinimap()
    {
        if (MinimapCanvas == null || ViewModel == null || ViewModel.Nodes.Count == 0) return;

        MinimapCanvas.Children.Clear();

        // Find bounding box of all nodes
        var minX = ViewModel.Nodes.Min(n => n.X);
        var maxX = ViewModel.Nodes.Max(n => n.X + NodeViewModel.NodeWidth);
        var minY = ViewModel.Nodes.Min(n => n.Y);
        var maxY = ViewModel.Nodes.Max(n => n.Y + 120);

        var width = Math.Max(200.0, maxX - minX + 100);
        var height = Math.Max(150.0, maxY - minY + 100);

        var scaleX = MinimapCanvas.ActualWidth > 0 ? MinimapCanvas.ActualWidth / width : 140.0 / width;
        var scaleY = MinimapCanvas.ActualHeight > 0 ? MinimapCanvas.ActualHeight / height : 90.0 / height;
        var scale = Math.Min(scaleX, scaleY);

        foreach (var node in ViewModel.Nodes)
        {
            var rect = new Rectangle
            {
                Width = Math.Max(8, NodeViewModel.NodeWidth * scale),
                Height = Math.Max(5, 80 * scale),
                Fill = node.IsSelected ? new SolidColorBrush(Color.FromRgb(56, 189, 248)) : new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                RadiusX = 2,
                RadiusY = 2
            };

            Canvas.SetLeft(rect, (node.X - minX + 20) * scale);
            Canvas.SetTop(rect, (node.Y - minY + 20) * scale);
            MinimapCanvas.Children.Add(rect);
        }
    }

    #endregion
}
