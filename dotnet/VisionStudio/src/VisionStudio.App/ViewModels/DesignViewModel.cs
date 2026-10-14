using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using VisionStudio.App.Converters;
using VisionStudio.App.Services;
using VisionStudio.Core.Models;
using VisionStudio.Core.Overlays;
using VisionStudio.Core.Pipeline;
using VisionStudio.Core.Tools;

namespace VisionStudio.App.ViewModels;

public enum InteractiveToolMode
{
    PanZoom,
    RectangleRoi,
    LineCaliper,
    PointPicker,
    PolygonRoi
}

/// <summary>
/// Master ViewModel for Design Mode (Visual Pipeline DAG editor, live parameter tuning, inspection viewer).
/// </summary>
public partial class DesignViewModel : ObservableObject
{
    private readonly ILogger<DesignViewModel> _logger;
    private readonly ToolRegistry _toolRegistry;
    private readonly PipelineGraph _graph;
    private readonly PipelineExecutor _executor;
    private readonly UndoRedoManager _undoRedo;

    public PipelineGraph Graph => _graph;

    private int _nodeCounter = 1;
    private CancellationTokenSource? _runCts;

    [ObservableProperty]
    private NodeViewModel? _selectedNode;

    [ObservableProperty]
    private ConnectionViewModel? _selectedConnection;

    [ObservableProperty]
    private BitmapSource? _displayedImage;

    [ObservableProperty]
    private IReadOnlyList<IOverlayPrimitive>? _displayedOverlays;

    [ObservableProperty]
    private string _displayedTitle = "No Image Selected";

    [ObservableProperty]
    private string _displayedImageInfo = "Ready";

    [ObservableProperty]
    private double _zoomFactor = 1.0;

    [ObservableProperty]
    private double _cursorX;

    [ObservableProperty]
    private double _cursorY;

    [ObservableProperty]
    private string _cursorPixelInfo = "X: 0, Y: 0 | RGB: -";

    [ObservableProperty]
    private InteractiveToolMode _activeInteractionMode = InteractiveToolMode.PanZoom;

    [ObservableProperty]
    private string _interactiveModeText = "Chế độ: Di chuyển & Thu phóng (Pan/Zoom)";

    [ObservableProperty]
    private string _interactiveStatusHint = "Kéo chuột giữa hoặc Space + kéo chuột trái để di chuyển";

    public event Action? RequestSwitchToImageViewer;

    public bool SelectedNodeSupportsRoi => SelectedNode?.Parameters.Any(p => p.Name is "X" or "CenterX" or "RoiX" or "Width" or "Radius" or "OuterRadius" or "CropX" or "Left" or "TopLeftX" or "X1" or "RoiWidth") == true;
    public bool SelectedNodeSupportsLine => SelectedNode?.Parameters.Any(p => p.Name is "StartX" or "StartY" or "EndX" or "EndY" or "X1" or "Y1" or "X2" or "Y2" or "BaseStartX") == true;
    public bool SelectedNodeSupportsPoint => SelectedNode?.Parameters.Any(p => p.Name is "CenterX" or "CenterY" or "X" or "Y" or "PointX" or "PointY" or "X1" or "Y1") == true;
    public bool SelectedNodeSupportsPolygon => SelectedNode?.Parameters.Any(p => p.Name is "Points" or "PolygonPoints") == true;
    public bool SelectedNodeSupportsInteractiveSelection => SelectedNodeSupportsRoi || SelectedNodeSupportsLine || SelectedNodeSupportsPoint || SelectedNodeSupportsPolygon;

    [ObservableProperty]
    private bool _isGridSnapEnabled = true;

    [ObservableProperty]
    private bool _isAutoRunEnabled = true;

    [ObservableProperty]
    private bool _isRunning = false;

    [ObservableProperty]
    private string _toolSearchText = string.Empty;

    public ObservableCollection<NodeViewModel> Nodes { get; } = new();
    public ObservableCollection<ConnectionViewModel> Connections { get; } = new();
    public ObservableCollection<InspectionItemViewModel> InspectionStrip { get; } = new();
    public ObservableCollection<ToolItemViewModel> AllTools { get; } = new();
    public ObservableCollection<ToolItemViewModel> FilteredTools { get; } = new();

    [ObservableProperty]
    private ToolDocumentation? _activeGuide;

    partial void OnActiveGuideChanged(ToolDocumentation? value)
    {
        OnPropertyChanged(nameof(HasActiveGuide));
        OnPropertyChanged(nameof(ShowCatalogGuide));
        OnPropertyChanged(nameof(ShowEmptyPlaceholder));
    }

    [ObservableProperty]
    private ToolItemViewModel? _selectedToolItem;

    private bool _isUpdatingSelection;

    partial void OnSelectedToolItemChanged(ToolItemViewModel? value)
    {
        if (_isUpdatingSelection) return;

        if (value != null)
        {
            ActiveGuide = value.Guide;
            try
            {
                _isUpdatingSelection = true;
                SelectedNode = null;
            }
            finally
            {
                _isUpdatingSelection = false;
            }
        }
        OnPropertyChanged(nameof(HasSelectedNode));
        OnPropertyChanged(nameof(ShowCatalogGuide));
        OnPropertyChanged(nameof(ShowEmptyPlaceholder));
    }

    public bool HasSelectedNode => SelectedNode != null;
    public bool HasActiveGuide => ActiveGuide != null;
    public bool ShowCatalogGuide => SelectedNode == null && ActiveGuide != null;
    public bool ShowEmptyPlaceholder => SelectedNode == null && ActiveGuide == null;

    public bool CanUndo => _undoRedo.CanUndo;
    public bool CanRedo => _undoRedo.CanRedo;

    public DesignViewModel(ILogger<DesignViewModel> logger, ToolRegistry toolRegistry)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _graph = new PipelineGraph();
        _executor = new PipelineExecutor(_logger);
        _undoRedo = new UndoRedoManager();
        _undoRedo.StateChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
        };

        LocalizationService.Instance.PropertyChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(ActiveGuide));
        };

        PopulateToolbox();
        CreateDefaultPipeline();
    }

    private void PopulateToolbox()
    {
        AllTools.Clear();
        FilteredTools.Clear();

        foreach (var desc in _toolRegistry.GetAvailableTools())
        {
            var item = new ToolItemViewModel(desc);
            AllTools.Add(item);
            FilteredTools.Add(item);
        }

        if (FilteredTools.Count > 0 && SelectedToolItem == null && SelectedNode == null)
        {
            SelectedToolItem = FilteredTools[0];
        }
    }

    partial void OnToolSearchTextChanged(string value)
    {
        FilteredTools.Clear();
        var query = value?.Trim() ?? string.Empty;
        var filtered = string.IsNullOrEmpty(query)
            ? AllTools
            : AllTools.Where(t => t.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
                               || t.Category.Contains(query, StringComparison.OrdinalIgnoreCase)
                               || t.Description.Contains(query, StringComparison.OrdinalIgnoreCase));

        foreach (var item in filtered)
        {
            FilteredTools.Add(item);
        }
    }

    partial void OnSelectedNodeChanged(NodeViewModel? value)
    {
        if (_isUpdatingSelection) return;

        foreach (var n in Nodes)
        {
            n.IsSelected = ReferenceEquals(n, value);
        }

        foreach (var item in InspectionStrip)
        {
            item.IsSelected = ReferenceEquals(item.Node, value);
        }

        UpdateViewerFromNode(value);

        if (value != null)
        {
            ActiveGuide = value.Guide;
            try
            {
                _isUpdatingSelection = true;
                SelectedToolItem = null;
            }
            finally
            {
                _isUpdatingSelection = false;
            }
        }
        else if (SelectedToolItem != null)
        {
            ActiveGuide = SelectedToolItem.Guide;
        }

        OnPropertyChanged(nameof(HasSelectedNode));
        OnPropertyChanged(nameof(ShowCatalogGuide));
        OnPropertyChanged(nameof(ShowEmptyPlaceholder));
        OnPropertyChanged(nameof(SelectedNodeSupportsRoi));
        OnPropertyChanged(nameof(SelectedNodeSupportsLine));
        OnPropertyChanged(nameof(SelectedNodeSupportsPoint));
        OnPropertyChanged(nameof(SelectedNodeSupportsPolygon));
        OnPropertyChanged(nameof(SelectedNodeSupportsInteractiveSelection));
    }

    /// <summary>
    /// Loads a pre-built starter pipeline (CameraGrab -> Grayscale -> GaussianFilter -> Caliper)
    /// for immediate out-of-the-box exploration without hardware.
    /// </summary>
    private void CreateDefaultPipeline()
    {
        try
        {
            var origAutoRun = IsAutoRunEnabled;
            IsAutoRunEnabled = false;

            try
            {
                if (_toolRegistry.HasTool("Acquisition.CameraGrab"))
                {
                    var n1 = AddNodeInternal("Acquisition.CameraGrab", 50, 80);
                    var n2 = _toolRegistry.HasTool("Color.Grayscale") ? AddNodeInternal("Color.Grayscale", 320, 80) : null;
                    var n3 = _toolRegistry.HasTool("Filter.Gaussian") ? AddNodeInternal("Filter.Gaussian", 590, 80) : null;
                    var n4 = _toolRegistry.HasTool("Measurement.Caliper") ? AddNodeInternal("Measurement.Caliper", 860, 80) : null;

                    // Configure Caliper scan line across target part (part is at X: 220..420, Y: 140..340)
                    if (n4 != null)
                    {
                        var pX1 = n4.Parameters.FirstOrDefault(p => p.Name == "StartX");
                        var pY1 = n4.Parameters.FirstOrDefault(p => p.Name == "StartY");
                        var pX2 = n4.Parameters.FirstOrDefault(p => p.Name == "EndX");
                        var pY2 = n4.Parameters.FirstOrDefault(p => p.Name == "EndY");
                        if (pX1 != null) pX1.DoubleValue = 150.0;
                        if (pY1 != null) pY1.DoubleValue = 240.0;
                        if (pX2 != null) pX2.DoubleValue = 490.0;
                        if (pY2 != null) pY2.DoubleValue = 240.0;
                    }

                    if (n1 != null && n2 != null) ConnectPortsInternal(n1, "Image", n2, "ImageIn");
                    if (n2 != null && n3 != null) ConnectPortsInternal(n2, "ImageOut", n3, "ImageIn");
                    if (n3 != null && n4 != null) ConnectPortsInternal(n3, "ImageOut", n4, "ImageIn");
                }
            }
            finally
            {
                IsAutoRunEnabled = origAutoRun;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize default starter pipeline.");
        }
    }

    /// <summary>
    /// Loads a full industrial sample inspection program
    /// (CameraGrab -> Grayscale -> Gaussian -> Sub-pixel Caliper -> SPC Process Capability),
    /// wires all ports, runs execution immediately, and highlights the measurement result.
    /// </summary>
    [RelayCommand]
    public async Task LoadSampleInspectionPipelineAsync()
    {
        try
        {
            var origAutoRun = IsAutoRunEnabled;
            IsAutoRunEnabled = false;

            try
            {
                // 1. Reset execution and clear existing graph & canvas
                ResetPipeline();
                _undoRedo.Clear();

                var connsToRemove = Connections.ToList();
                foreach (var c in connsToRemove)
                {
                    DisconnectPortsInternal(c);
                }

                var nodesToRemove = Nodes.ToList();
                foreach (var n in nodesToRemove)
                {
                    _graph.RemoveNode(n.NodeId);
                }
                Nodes.Clear();
                InspectionStrip.Clear();
                _nodeCounter = 1;

                // 2. Instantiate and position nodes
                // Node 1: Camera Acquisition (Synthetic Machined Part with circular bores)
                var n1 = AddNodeInternal("Acquisition.CameraGrab", 50, 90);
                var pCamPattern = n1.Parameters.FirstOrDefault(p => p.Name == "Pattern");
                if (pCamPattern != null) pCamPattern.StringValue = "TargetPart";

                // Node 2: Grayscale Conversion
                var n2 = _toolRegistry.HasTool("Color.Grayscale") ? AddNodeInternal("Color.Grayscale", 320, 90) : null;

                // Node 3: Gaussian Smoothing Filter
                var n3 = _toolRegistry.HasTool("Filter.Gaussian") ? AddNodeInternal("Filter.Gaussian", 590, 90) : null;
                if (n3 != null)
                {
                    var pK = n3.Parameters.FirstOrDefault(p => p.Name == "KernelSize");
                    if (pK != null) pK.IntValue = 5;
                }

                // Node 4: 1D Sub-pixel Caliper (measures part width across target)
                var n4 = _toolRegistry.HasTool("Measurement.Caliper") ? AddNodeInternal("Measurement.Caliper", 860, 90) : null;
                if (n4 != null)
                {
                    var pX1 = n4.Parameters.FirstOrDefault(p => p.Name == "StartX");
                    var pY1 = n4.Parameters.FirstOrDefault(p => p.Name == "StartY");
                    var pX2 = n4.Parameters.FirstOrDefault(p => p.Name == "EndX");
                    var pY2 = n4.Parameters.FirstOrDefault(p => p.Name == "EndY");
                    var pScale = n4.Parameters.FirstOrDefault(p => p.Name == "ScaleMmPerPixel");

                    if (pX1 != null) pX1.DoubleValue = 140.0;
                    if (pY1 != null) pY1.DoubleValue = 240.0;
                    if (pX2 != null) pX2.DoubleValue = 500.0;
                    if (pY2 != null) pY2.DoubleValue = 240.0;
                    if (pScale != null) pScale.DoubleValue = 0.1; // 1 px = 0.1 mm
                }

                // Node 5: SPC Process Capability / Tolerance Check
                var n5 = _toolRegistry.HasTool("Spc.ProcessCapability") ? AddNodeInternal("Spc.ProcessCapability", 1130, 90) : null;
                if (n5 != null)
                {
                    var pLsl = n5.Parameters.FirstOrDefault(p => p.Name == "LowerSpecLimit");
                    var pUsl = n5.Parameters.FirstOrDefault(p => p.Name == "UpperSpecLimit");
                    if (pLsl != null) pLsl.DoubleValue = 30.0;
                    if (pUsl != null) pUsl.DoubleValue = 34.0;
                }

                // 3. Connect all ports
                if (n1 != null && n2 != null) ConnectPortsInternal(n1, "Image", n2, "ImageIn");
                if (n2 != null && n3 != null) ConnectPortsInternal(n2, "ImageOut", n3, "ImageIn");
                if (n3 != null && n4 != null) ConnectPortsInternal(n3, "ImageOut", n4, "ImageIn");
                if (n4 != null && n5 != null) ConnectPortsInternal(n4, "WidthMm", n5, "ValueIn");

                // 4. Highlight Caliper inspection result
                SelectedNode = n4 ?? n1;

                _logger.LogInformation("Loaded Sample Inspection Program (Camera -> Grayscale -> Gaussian -> Caliper -> SPC).");
            }
            finally
            {
                IsAutoRunEnabled = origAutoRun;
            }

            // 5. Automatically execute the sample pipeline
            await RunPipelineAsync();
            if (SelectedNode != null)
            {
                UpdateViewerFromNode(SelectedNode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load sample inspection pipeline.");
        }
    }

    /// <summary>
    /// Loads one of the 5 industrial application wizard presets.
    /// </summary>
    public async Task LoadPresetPipelineAsync(string presetType)
    {
        try
        {
            var origAutoRun = IsAutoRunEnabled;
            IsAutoRunEnabled = false;

            try
            {
                ResetPipeline();
                _undoRedo.Clear();

                foreach (var c in Connections.ToList())
                {
                    DisconnectPortsInternal(c);
                }

                foreach (var n in Nodes.ToList())
                {
                    _graph.RemoveNode(n.NodeId);
                }
                Nodes.Clear();
                InspectionStrip.Clear();
                _nodeCounter = 1;

                switch (presetType)
                {
                    case "MeasureDistance":
                        await LoadSampleInspectionPipelineAsync();
                        return;

                    case "PresenceCheck":
                        var cam1 = AddNodeInternal("Acquisition.CameraGrab", 50, 90);
                        var gray1 = _toolRegistry.HasTool("Color.Grayscale") ? AddNodeInternal("Color.Grayscale", 320, 90) : null;
                        var thresh1 = _toolRegistry.HasTool("Segmentation.Threshold") ? AddNodeInternal("Segmentation.Threshold", 590, 90) : null;
                        var caliper1 = _toolRegistry.HasTool("Measurement.Caliper") ? AddNodeInternal("Measurement.Caliper", 860, 90) : null;

                        if (cam1 != null && gray1 != null) ConnectPortsInternal(cam1, "Image", gray1, "ImageIn");
                        if (gray1 != null && thresh1 != null) ConnectPortsInternal(gray1, "ImageOut", thresh1, "ImageIn");
                        if (gray1 != null && caliper1 != null) ConnectPortsInternal(gray1, "ImageOut", caliper1, "ImageIn");
                        SelectedNode = caliper1 ?? thresh1 ?? cam1;
                        break;

                    case "ReadCodeAndGrade":
                        var cam2 = AddNodeInternal("Acquisition.CameraGrab", 50, 90);
                        var gray2 = _toolRegistry.HasTool("Color.Grayscale") ? AddNodeInternal("Color.Grayscale", 320, 90) : null;
                        var barcode2 = _toolRegistry.HasTool("Identification.BarcodeReader") ? AddNodeInternal("Identification.BarcodeReader", 590, 90) : null;
                        var grade2 = _toolRegistry.HasTool("Grading.CodeQuality") ? AddNodeInternal("Grading.CodeQuality", 860, 90) : null;

                        if (cam2 != null && gray2 != null) ConnectPortsInternal(cam2, "Image", gray2, "ImageIn");
                        if (gray2 != null && barcode2 != null) ConnectPortsInternal(gray2, "ImageOut", barcode2, "ImageIn");
                        if (barcode2 != null && grade2 != null) ConnectPortsInternal(barcode2, "AnnotatedImage", grade2, "ImageIn");
                        SelectedNode = barcode2 ?? cam2;
                        break;

                    case "LocateAndPick":
                        var cam3 = AddNodeInternal("Acquisition.CameraGrab", 50, 90);
                        var gray3 = _toolRegistry.HasTool("Color.Grayscale") ? AddNodeInternal("Color.Grayscale", 320, 90) : null;
                        var match3 = _toolRegistry.HasTool("Matching.GeometricMatcher") ? AddNodeInternal("Matching.GeometricMatcher", 590, 90)
                                   : _toolRegistry.HasTool("Matching.TemplateMatch") ? AddNodeInternal("Matching.TemplateMatch", 590, 90) : null;
                        var align3 = _toolRegistry.HasTool("Matching.PatternAlign") ? AddNodeInternal("Matching.PatternAlign", 860, 90) : null;

                        if (cam3 != null && gray3 != null) ConnectPortsInternal(cam3, "Image", gray3, "ImageIn");
                        if (gray3 != null && match3 != null) ConnectPortsInternal(gray3, "ImageOut", match3, "ImageIn");
                        if (match3 != null && align3 != null) ConnectPortsInternal(match3, "OutputImage", align3, "ImageIn");
                        SelectedNode = match3 ?? cam3;
                        break;

                    case "SurfaceDefect":
                    default:
                        var cam4 = AddNodeInternal("Acquisition.CameraGrab", 50, 90);
                        var gray4 = _toolRegistry.HasTool("Color.Grayscale") ? AddNodeInternal("Color.Grayscale", 320, 90) : null;
                        var sharpen4 = _toolRegistry.HasTool("Filter.Sharpen") ? AddNodeInternal("Filter.Sharpen", 590, 90) : null;
                        var thresh4 = _toolRegistry.HasTool("Segmentation.Threshold") ? AddNodeInternal("Segmentation.Threshold", 860, 90) : null;

                        if (cam4 != null && gray4 != null) ConnectPortsInternal(cam4, "Image", gray4, "ImageIn");
                        if (gray4 != null && sharpen4 != null) ConnectPortsInternal(gray4, "ImageOut", sharpen4, "ImageIn");
                        if (sharpen4 != null && thresh4 != null) ConnectPortsInternal(sharpen4, "ImageOut", thresh4, "ImageIn");
                        SelectedNode = thresh4 ?? sharpen4 ?? cam4;
                        break;
                }
            }
            finally
            {
                IsAutoRunEnabled = origAutoRun;
            }

            await RunPipelineAsync();
            if (SelectedNode != null)
            {
                UpdateViewerFromNode(SelectedNode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load preset pipeline {Preset}.", presetType);
        }
    }

    public Task? LastExecutionTask { get; private set; }

    #region Pipeline Execution

    [RelayCommand]
    public async Task RunPipelineAsync()
    {
        if (IsRunning) return;
        IsRunning = true;

        try
        {
            _runCts = new CancellationTokenSource();
            var report = await _executor.ExecuteAsync(_graph, stopAtBreakpoints: false, _runCts.Token);
            ApplyExecutionReport(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during pipeline execution.");
        }
        finally
        {
            IsRunning = false;
        }
    }

    [RelayCommand]
    public async Task StepPipelineAsync()
    {
        if (IsRunning) return;
        IsRunning = true;

        try
        {
            var orderedList = _graph.GetTopologicalOrder().ToList();
            if (orderedList.Count == 0) return;

            // Find first node needing execution or not yet executed
            var nextNode = orderedList.FirstOrDefault(n => n.IsDirty || n.CachedResult == null);
            if (nextNode == null)
            {
                // All nodes already executed, restart from first node
                ResetPipeline();
                orderedList = _graph.GetTopologicalOrder().ToList();
                nextNode = orderedList.FirstOrDefault();
            }

            if (nextNode != null)
            {
                var nextIndex = orderedList.IndexOf(nextNode);
                var targetBreakpoint = nextIndex + 1 < orderedList.Count ? orderedList[nextIndex + 1] : null;

                var origBreakpoint = targetBreakpoint?.HasBreakpoint ?? false;
                if (targetBreakpoint != null) targetBreakpoint.HasBreakpoint = true;

                try
                {
                    var report = await _executor.ExecuteAsync(_graph, stopAtBreakpoints: true, CancellationToken.None);
                    ApplyExecutionReport(report);
                    SelectedNode = Nodes.FirstOrDefault(n => n.NodeId == nextNode.Id);
                }
                finally
                {
                    if (targetBreakpoint != null) targetBreakpoint.HasBreakpoint = origBreakpoint;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stepping through pipeline.");
        }
        finally
        {
            IsRunning = false;
        }
    }

    [RelayCommand]
    public async Task RunToBreakpointAsync()
    {
        if (IsRunning) return;
        IsRunning = true;

        try
        {
            var report = await _executor.ExecuteAsync(_graph, stopAtBreakpoints: true, CancellationToken.None);
            ApplyExecutionReport(report);

            if (report.StoppedNodeId != null)
            {
                SelectedNode = Nodes.FirstOrDefault(n => n.NodeId == report.StoppedNodeId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing to breakpoint.");
        }
        finally
        {
            IsRunning = false;
        }
    }

    private void ApplyExecutionReport(PipelineExecutionResult report)
    {
        foreach (var nodeVm in Nodes)
        {
            if (report.NodeResults.TryGetValue(nodeVm.NodeId, out var result))
            {
                nodeVm.ApplyResult(result);
            }
        }

        foreach (var item in InspectionStrip)
        {
            item.Refresh();
        }

        var targetNode = SelectedNode ?? Nodes.LastOrDefault();
        if (targetNode != null)
        {
            UpdateViewerFromNode(targetNode);
        }
    }

    [RelayCommand]
    public void ToggleBreakpoint(NodeViewModel? node)
    {
        if (node == null) return;
        node.HasBreakpoint = !node.HasBreakpoint;
        var pNode = _graph.GetNode(node.NodeId);
        if (pNode != null)
        {
            pNode.HasBreakpoint = node.HasBreakpoint;
        }
    }

    [RelayCommand]
    public void ResetPipeline()
    {
        foreach (var pNode in _graph.Nodes)
        {
            pNode.CachedResult = null;
            pNode.IsDirty = true;
        }

        foreach (var n in Nodes)
        {
            n.Status = null;
            n.ExecutionTimeMs = 0.0;
            n.Thumbnail = null;
            n.LastResult = null;
        }

        foreach (var item in InspectionStrip) item.Refresh();

        DisplayedImage = null;
        DisplayedOverlays = null;
        DisplayedTitle = "Pipeline Reset";
        DisplayedImageInfo = "Ready";
    }

    private void OnNodeParameterChanged(NodeViewModel node)
    {
        var pNode = _graph.GetNode(node.NodeId);
        pNode?.MarkDirty();

        if (IsAutoRunEnabled && !IsRunning)
        {
            LastExecutionTask = RunPipelineAsync();
        }
    }

    #endregion

    #region Viewer & Inspection

    public void UpdateViewerFromNode(NodeViewModel? node)
    {
        if (node == null || node.LastResult == null)
        {
            DisplayedImage = null;
            DisplayedOverlays = null;
            DisplayedTitle = node?.DisplayName ?? "No Image";
            DisplayedImageInfo = "No execution result available";
            return;
        }

        var res = node.LastResult;
        DisplayedTitle = $"{node.DisplayName} ({node.NodeId})";
        DisplayedOverlays = res.Overlays;

        // Find primary output image
        ImageFrame? frame = null;
        foreach (var val in res.Outputs.Values)
        {
            if (val is ImageFrame f)
            {
                frame = f;
                break;
            }
        }

        if (frame != null && !frame.Mat.IsDisposed)
        {
            DisplayedImage = ImageConversionHelper.FrameToBitmapSource(frame);
            DisplayedImageInfo = $"{frame.Width} x {frame.Height} px | {frame.Channels} Ch | {res.ExecutionTime.TotalMilliseconds:F1} ms | Status: {res.Status}";
        }
        else
        {
            DisplayedImage = null;
            DisplayedImageInfo = $"{res.ExecutionTime.TotalMilliseconds:F1} ms | Outputs: {res.Outputs.Count} | Status: {res.Status}";
        }
    }

    public void SamplePixelAt(int x, int y)
    {
        CursorX = x;
        CursorY = y;

        if (SelectedNode?.LastResult == null)
        {
            CursorPixelInfo = $"X: {x}, Y: {y} | -";
            return;
        }

        ImageFrame? frame = null;
        foreach (var v in SelectedNode.LastResult.Outputs.Values)
        {
            if (v is ImageFrame f) { frame = f; break; }
        }

        if (frame != null && !frame.Mat.IsDisposed)
        {
            var (r, g, b, gray, ok) = ImageConversionHelper.SamplePixel(frame.Mat, x, y);
            if (ok)
            {
                CursorPixelInfo = frame.Channels == 1
                    ? $"X: {x}, Y: {y} | Gray: {gray}"
                    : $"X: {x}, Y: {y} | RGB: ({r}, {g}, {b}) | Gray: {gray}";
                return;
            }
        }

        CursorPixelInfo = $"X: {x}, Y: {y} | Out of bounds";
    }

    #region Interactive Image Selection Tools

    [RelayCommand]
    public void SetInteractionMode(InteractiveToolMode mode)
    {
        ActiveInteractionMode = mode;
        switch (mode)
        {
            case InteractiveToolMode.RectangleRoi:
                InteractiveModeText = "Chế độ: Vùng chọn hình chữ nhật ROI";
                InteractiveStatusHint = SelectedNode != null
                    ? $"Kéo chuột trái trên ảnh để gán ROI cho [{SelectedNode.DisplayName}]"
                    : "Kéo chuột trái trên ảnh để vẽ vùng chọn ROI";
                RequestSwitchToImageViewer?.Invoke();
                break;

            case InteractiveToolMode.LineCaliper:
                InteractiveModeText = "Chế độ: Thước đo Caliper (Line)";
                InteractiveStatusHint = SelectedNode != null
                    ? $"Kéo chuột trái trên ảnh từ điểm bắt đầu đến điểm kết thúc để gán Caliper cho [{SelectedNode.DisplayName}]"
                    : "Kéo chuột trái để vẽ đoạn thẳng đo Caliper";
                RequestSwitchToImageViewer?.Invoke();
                break;

            case InteractiveToolMode.PointPicker:
                InteractiveModeText = "Chế độ: Chọn điểm tọa độ (Point Reticle)";
                InteractiveStatusHint = SelectedNode != null
                    ? $"Nhấn chuột trái trên ảnh để gán tâm/tọa độ cho [{SelectedNode.DisplayName}]"
                    : "Nhấn chuột trái trên ảnh để chọn tọa độ";
                RequestSwitchToImageViewer?.Invoke();
                break;

            case InteractiveToolMode.PolygonRoi:
                InteractiveModeText = "Chế độ: Vùng chọn đa giác (Polygon ROI)";
                InteractiveStatusHint = SelectedNode != null
                    ? $"Nhấp chuột trái các đỉnh đa giác, nhấp đúp để hoàn tất gán cho [{SelectedNode.DisplayName}]"
                    : "Nhấp chuột trái các đỉnh đa giác, nhấp đúp để hoàn tất";
                RequestSwitchToImageViewer?.Invoke();
                break;

            default:
                InteractiveModeText = "Chế độ: Di chuyển & Thu phóng (Pan/Zoom)";
                InteractiveStatusHint = "Kéo chuột giữa hoặc Space + kéo chuột trái để di chuyển";
                break;
        }
    }

    [RelayCommand]
    public void SelectRoiOnImage() => SetInteractionMode(InteractiveToolMode.RectangleRoi);

    [RelayCommand]
    public void SelectLineOnImage() => SetInteractionMode(InteractiveToolMode.LineCaliper);

    [RelayCommand]
    public void SelectPointOnImage() => SetInteractionMode(InteractiveToolMode.PointPicker);

    [RelayCommand]
    public void SelectPolygonOnImage() => SetInteractionMode(InteractiveToolMode.PolygonRoi);

    [RelayCommand]
    public void ResetInteractionMode() => SetInteractionMode(InteractiveToolMode.PanZoom);

    public bool ApplyInteractiveRoi(double x, double y, double w, double h)
    {
        if (SelectedNode == null) return false;

        bool updated = false;
        var pX = SelectedNode.Parameters.FirstOrDefault(p => p.Name is "X" or "OriginX" or "CropX" or "Left" or "TopLeftX");
        var pY = SelectedNode.Parameters.FirstOrDefault(p => p.Name is "Y" or "OriginY" or "CropY" or "Top" or "TopLeftY");
        var pRoiX = SelectedNode.Parameters.FirstOrDefault(p => p.Name == "RoiX");
        var pRoiY = SelectedNode.Parameters.FirstOrDefault(p => p.Name == "RoiY");
        var pCx = SelectedNode.Parameters.FirstOrDefault(p => p.Name == "CenterX");
        var pCy = SelectedNode.Parameters.FirstOrDefault(p => p.Name == "CenterY");
        var pW = SelectedNode.Parameters.FirstOrDefault(p => p.Name is "Width" or "RoiWidth" or "W" or "BoxWidth");
        var pH = SelectedNode.Parameters.FirstOrDefault(p => p.Name is "Height" or "RoiHeight" or "H" or "BoxHeight");
        var pRadius = SelectedNode.Parameters.FirstOrDefault(p => p.Name is "Radius" or "OuterRadius");

        var pX1 = SelectedNode.Parameters.FirstOrDefault(p => p.Name is "X1" or "StartX" or "BaseStartX");
        var pY1 = SelectedNode.Parameters.FirstOrDefault(p => p.Name is "Y1" or "StartY" or "BaseStartY");
        var pX2 = SelectedNode.Parameters.FirstOrDefault(p => p.Name is "X2" or "EndX" or "BaseEndX");
        var pY2 = SelectedNode.Parameters.FirstOrDefault(p => p.Name is "Y2" or "EndY" or "BaseEndY");

        if (pX != null && pY != null)
        {
            pX.DoubleValue = Math.Round(x);
            pY.DoubleValue = Math.Round(y);
            updated = true;
        }
        else if (pRoiX != null && pRoiY != null)
        {
            pRoiX.DoubleValue = Math.Round(x);
            pRoiY.DoubleValue = Math.Round(y);
            updated = true;
        }

        if (pCx != null && pCy != null)
        {
            pCx.DoubleValue = Math.Round(x + w / 2.0);
            pCy.DoubleValue = Math.Round(y + h / 2.0);
            updated = true;
        }

        if (pW != null && pH != null)
        {
            pW.DoubleValue = Math.Max(1, Math.Round(w));
            pH.DoubleValue = Math.Max(1, Math.Round(h));
            updated = true;
        }

        if (pRadius != null)
        {
            pRadius.DoubleValue = Math.Max(1, Math.Round(Math.Min(w, h) / 2.0));
            updated = true;
        }

        // If tool has X1, Y1, X2, Y2 (e.g. Caliper or Box region)
        if (pX1 != null && pY1 != null && pX2 != null && pY2 != null)
        {
            pX1.DoubleValue = Math.Round(x);
            pY1.DoubleValue = Math.Round(y + h / 2.0);
            pX2.DoubleValue = Math.Round(x + w);
            pY2.DoubleValue = Math.Round(y + h / 2.0);
            updated = true;
        }

        if (updated)
        {
            InteractiveStatusHint = $"Đã gán ROI [X:{(int)x}, Y:{(int)y}, W:{(int)w}, H:{(int)h}] cho [{SelectedNode.DisplayName}]";
            OnNodeParameterChanged(SelectedNode);
        }
        return updated;
    }

    public bool ApplyInteractiveLine(double x1, double y1, double x2, double y2)
    {
        if (SelectedNode == null) return false;

        var pStartX = SelectedNode.Parameters.FirstOrDefault(p => p.Name is "StartX" or "X1" or "Start_X" or "BaseStartX");
        var pStartY = SelectedNode.Parameters.FirstOrDefault(p => p.Name is "StartY" or "Y1" or "Start_Y" or "BaseStartY");
        var pEndX = SelectedNode.Parameters.FirstOrDefault(p => p.Name is "EndX" or "X2" or "End_X" or "BaseEndX");
        var pEndY = SelectedNode.Parameters.FirstOrDefault(p => p.Name is "EndY" or "Y2" or "End_Y" or "BaseEndY");

        if (pStartX != null && pStartY != null && pEndX != null && pEndY != null)
        {
            pStartX.DoubleValue = Math.Round(x1, 1);
            pStartY.DoubleValue = Math.Round(y1, 1);
            pEndX.DoubleValue = Math.Round(x2, 1);
            pEndY.DoubleValue = Math.Round(y2, 1);
            var len = Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
            InteractiveStatusHint = $"Đã gán thước Caliper L:{len:F1}px [({(int)x1},{(int)y1}) -> ({(int)x2},{(int)y2})] cho [{SelectedNode.DisplayName}]";
            OnNodeParameterChanged(SelectedNode);
            return true;
        }
        return false;
    }

    public bool ApplyInteractivePoint(double x, double y)
    {
        if (SelectedNode == null) return false;

        bool updated = false;
        var pCx = SelectedNode.Parameters.FirstOrDefault(p => p.Name is "CenterX" or "Center_X");
        var pCy = SelectedNode.Parameters.FirstOrDefault(p => p.Name is "CenterY" or "Center_Y");
        var pX = SelectedNode.Parameters.FirstOrDefault(p => p.Name is "X" or "PointX");
        var pY = SelectedNode.Parameters.FirstOrDefault(p => p.Name is "Y" or "PointY");

        if (pCx != null && pCy != null)
        {
            pCx.IntValue = (int)Math.Round(x);
            pCy.IntValue = (int)Math.Round(y);
            updated = true;
        }
        else if (pX != null && pY != null)
        {
            pX.IntValue = (int)Math.Round(x);
            pY.IntValue = (int)Math.Round(y);
            updated = true;
        }

        if (updated)
        {
            InteractiveStatusHint = $"Đã gán điểm [X:{(int)x}, Y:{(int)y}] cho [{SelectedNode.DisplayName}]";
            OnNodeParameterChanged(SelectedNode);
        }
        return updated;
    }

    public bool ApplyInteractivePolygon(IReadOnlyList<System.Windows.Point> points)
    {
        if (SelectedNode == null || points == null || points.Count < 3) return false;

        var pPoints = SelectedNode.Parameters.FirstOrDefault(p => p.Name == "Points");
        if (pPoints != null)
        {
            var str = string.Join("; ", points.Select(p => $"{(int)Math.Round(p.X)},{(int)Math.Round(p.Y)}"));
            pPoints.StringValue = str;
            InteractiveStatusHint = $"Đã gán đa giác {points.Count} đỉnh cho [{SelectedNode.DisplayName}]";
            OnNodeParameterChanged(SelectedNode);
            return true;
        }
        return false;
    }

    #endregion


    #endregion

    #region Canvas Node & Connection Editing

    [RelayCommand]
    public void AddNode(ToolItemViewModel? toolItem)
    {
        if (toolItem == null) return;
        var x = 120 + (Nodes.Count % 4) * 240;
        var y = 100 + (Nodes.Count / 4) * 200;

        var cmd = new AddNodeUndoableCommand(this, toolItem.Id, x, y);
        _undoRedo.Execute(cmd);
    }

    public NodeViewModel AddNodeInternal(string toolId, double x, double y)
    {
        var tool = _toolRegistry.CreateInstance(toolId);
        var nodeId = $"Node_{_nodeCounter++}_{tool.DisplayName.Replace(" ", "")}";

        var nodeVm = new NodeViewModel(nodeId, tool, x, y, OnNodeParameterChanged);
        _graph.AddNode(new PipelineNode(nodeId, tool, tool.DisplayName, x, y));

        Nodes.Add(nodeVm);
        InspectionStrip.Add(new InspectionItemViewModel(nodeVm));

        SelectedNode = nodeVm;
        return nodeVm;
    }

    internal void RemoveNodeInternal(NodeViewModel node)
    {
        var toRemove = Connections
            .Where(c => c.SourceNodeId == node.NodeId || c.TargetNodeId == node.NodeId)
            .ToList();

        foreach (var conn in toRemove)
        {
            DisconnectPortsInternal(conn);
        }

        _graph.RemoveNode(node.NodeId);
        Nodes.Remove(node);

        var insp = InspectionStrip.FirstOrDefault(i => i.NodeId == node.NodeId);
        if (insp != null) InspectionStrip.Remove(insp);

        if (ReferenceEquals(SelectedNode, node))
        {
            SelectedNode = Nodes.LastOrDefault();
        }
    }

    [RelayCommand]
    public void DeleteSelected()
    {
        if (SelectedConnection != null)
        {
            var cmd = new RemoveConnectionUndoableCommand(this, SelectedConnection);
            _undoRedo.Execute(cmd);
            SelectedConnection = null;
            return;
        }

        if (SelectedNode != null)
        {
            var cmd = new RemoveNodeUndoableCommand(this, SelectedNode);
            _undoRedo.Execute(cmd);
            SelectedNode = null;
        }
    }

    [RelayCommand]
    public void DeleteNode(NodeViewModel? node)
    {
        if (node == null) return;
        var cmd = new RemoveNodeUndoableCommand(this, node);
        _undoRedo.Execute(cmd);
        if (SelectedNode == node)
        {
            SelectedNode = null;
        }
    }

    [RelayCommand]
    public async Task ImportTestImageAsync()
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Chọn tệp hình ảnh thử nghiệm / Select Test Image",
                Filter = "Tệp hình ảnh (*.png;*.jpg;*.jpeg;*.bmp;*.tiff)|*.png;*.jpg;*.jpeg;*.bmp;*.tiff|Tất cả tệp (*.*)|*.*",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() == true)
            {
                var filePath = dialog.FileName;
                var mat = OpenCvSharp.Cv2.ImRead(filePath, OpenCvSharp.ImreadModes.Color);
                if (!mat.Empty())
                {
                    DisplayedImage = Converters.ImageConversionHelper.MatToBitmapSource(mat);
                    DisplayedTitle = $"Tệp ảnh: {System.IO.Path.GetFileName(filePath)}";
                    DisplayedImageInfo = $"{mat.Width} x {mat.Height} px | {System.IO.Path.GetFileName(filePath)}";

                    // Update any CameraGrab or ImageFile node in pipeline
                    var targetNode = Nodes.FirstOrDefault(n => n.ToolId.Contains("CameraGrab", StringComparison.OrdinalIgnoreCase) || n.ToolId.Contains("Image", StringComparison.OrdinalIgnoreCase));
                    if (targetNode != null)
                    {
                        var pFile = targetNode.Parameters.FirstOrDefault(p => p.Name.Contains("File", StringComparison.OrdinalIgnoreCase) || p.Name.Contains("Path", StringComparison.OrdinalIgnoreCase));
                        if (pFile != null)
                        {
                            pFile.StringValue = filePath;
                        }
                    }

                    // Re-run pipeline with the imported image
                    await RunPipelineAsync();
                }
            }
        }
        catch
        {
            // Ignore dialog cancellation
        }
    }

    public bool CanConnect(PortViewModel source, PortViewModel target)
    {
        if (!source.CanConnectTo(target)) return false;

        var outPort = source.IsOutput ? source : target;
        var inPort = source.IsInput ? source : target;

        // Ensure target port doesn't already have an incoming connection
        if (_graph.Connections.Any(c => c.TargetNodeId.Equals(inPort.Node.NodeId, StringComparison.OrdinalIgnoreCase) &&
                                       c.TargetPortId.Equals(inPort.Port.Id, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        // Test cycle freedom
        var testConn = new PipelineConnection(outPort.Node.NodeId, outPort.Port.Id, inPort.Node.NodeId, inPort.Port.Id);
        _graph.AddConnection(testConn);
        var isValid = _graph.Validate().IsValid;
        _graph.RemoveConnection(testConn);

        return isValid;
    }

    public void ConnectPorts(PortViewModel source, PortViewModel target)
    {
        var outPort = source.IsOutput ? source : target;
        var inPort = source.IsInput ? source : target;

        if (!CanConnect(outPort, inPort)) return;

        var cmd = new AddConnectionUndoableCommand(this, outPort, inPort);
        _undoRedo.Execute(cmd);
    }

    internal ConnectionViewModel ConnectPortsInternal(NodeViewModel srcNode, string srcPort, NodeViewModel dstNode, string dstPort)
    {
        var outP = srcNode.OutputPorts.FirstOrDefault(p => p.Name == srcPort || p.Port.Id == srcPort);
        var inP = dstNode.InputPorts.FirstOrDefault(p => p.Name == dstPort || p.Port.Id == dstPort);

        if (outP == null || inP == null)
        {
            throw new InvalidOperationException($"Ports not found: {srcPort} -> {dstPort}");
        }

        return ConnectPortsInternal(outP, inP);
    }

    internal ConnectionViewModel ConnectPortsInternal(PortViewModel outPort, PortViewModel inPort)
    {
        var graphConn = new PipelineConnection(outPort.Node.NodeId, outPort.Port.Id, inPort.Node.NodeId, inPort.Port.Id);
        _graph.AddConnection(graphConn);

        var conn = new ConnectionViewModel(outPort, inPort);
        Connections.Add(conn);
        outPort.IsConnected = true;
        inPort.IsConnected = true;

        if (IsAutoRunEnabled && !IsRunning)
        {
            _ = RunPipelineAsync();
        }

        return conn;
    }

    internal void DisconnectPortsInternal(ConnectionViewModel conn)
    {
        var graphConn = _graph.Connections.FirstOrDefault(c =>
            c.SourceNodeId.Equals(conn.SourceNodeId, StringComparison.OrdinalIgnoreCase) &&
            c.SourcePortId.Equals(conn.SourcePort.Port.Id, StringComparison.OrdinalIgnoreCase) &&
            c.TargetNodeId.Equals(conn.TargetNodeId, StringComparison.OrdinalIgnoreCase) &&
            c.TargetPortId.Equals(conn.TargetPort.Port.Id, StringComparison.OrdinalIgnoreCase));

        if (graphConn != null)
        {
            _graph.RemoveConnection(graphConn);
        }

        Connections.Remove(conn);
        conn.SourcePort.IsConnected = Connections.Any(c => c.SourcePort == conn.SourcePort);
        conn.TargetPort.IsConnected = Connections.Any(c => c.TargetPort == conn.TargetPort);
    }

    [RelayCommand]
    public void Undo()
    {
        _undoRedo.Undo();
        _ = RunPipelineAsync();
    }

    [RelayCommand]
    public void Redo()
    {
        _undoRedo.Redo();
        _ = RunPipelineAsync();
    }

    [RelayCommand]
    public void ZoomIn() => ZoomFactor = Math.Min(5.0, ZoomFactor * 1.25);

    [RelayCommand]
    public void ZoomOut() => ZoomFactor = Math.Max(0.1, ZoomFactor / 1.25);

    [RelayCommand]
    public void ZoomActual() => ZoomFactor = 1.0;

    #endregion

    #region Reversible Canvas Commands

    private sealed class AddNodeUndoableCommand : IUndoableCommand
    {
        private readonly DesignViewModel _vm;
        private readonly string _toolId;
        private readonly double _x;
        private readonly double _y;
        private NodeViewModel? _createdNode;

        public string Description => $"Add Node {_toolId}";

        public AddNodeUndoableCommand(DesignViewModel vm, string toolId, double x, double y)
        {
            _vm = vm;
            _toolId = toolId;
            _x = x;
            _y = y;
        }

        public void Execute()
        {
            _createdNode = _vm.AddNodeInternal(_toolId, _x, _y);
        }

        public void Unexecute()
        {
            if (_createdNode != null)
            {
                _vm.RemoveNodeInternal(_createdNode);
            }
        }
    }

    private sealed class RemoveNodeUndoableCommand : IUndoableCommand
    {
        private readonly DesignViewModel _vm;
        private readonly NodeViewModel _node;
        private readonly List<ConnectionViewModel> _deletedConnections = new();

        public string Description => $"Remove Node {_node.DisplayName}";

        public RemoveNodeUndoableCommand(DesignViewModel vm, NodeViewModel node)
        {
            _vm = vm;
            _node = node;
            _deletedConnections = _vm.Connections
                .Where(c => c.SourceNodeId == node.NodeId || c.TargetNodeId == node.NodeId)
                .ToList();
        }

        public void Execute()
        {
            _vm.RemoveNodeInternal(_node);
        }

        public void Unexecute()
        {
            _vm.Nodes.Add(_node);
            _vm._graph.AddNode(new PipelineNode(_node.NodeId, _node.Tool, _node.DisplayName, _node.X, _node.Y));
            _vm.InspectionStrip.Add(new InspectionItemViewModel(_node));

            foreach (var conn in _deletedConnections)
            {
                _vm.ConnectPortsInternal(conn.SourcePort, conn.TargetPort);
            }
        }
    }

    private sealed class AddConnectionUndoableCommand : IUndoableCommand
    {
        private readonly DesignViewModel _vm;
        private readonly PortViewModel _outPort;
        private readonly PortViewModel _inPort;
        private ConnectionViewModel? _connection;

        public string Description => $"Connect {_outPort.Node.DisplayName}.{_outPort.Name} -> {_inPort.Node.DisplayName}.{_inPort.Name}";

        public AddConnectionUndoableCommand(DesignViewModel vm, PortViewModel outPort, PortViewModel inPort)
        {
            _vm = vm;
            _outPort = outPort;
            _inPort = inPort;
        }

        public void Execute()
        {
            _connection = _vm.ConnectPortsInternal(_outPort, _inPort);
        }

        public void Unexecute()
        {
            if (_connection != null)
            {
                _vm.DisconnectPortsInternal(_connection);
            }
        }
    }

    private sealed class RemoveConnectionUndoableCommand : IUndoableCommand
    {
        private readonly DesignViewModel _vm;
        private readonly ConnectionViewModel _connection;

        public string Description => $"Disconnect {_connection.SourceNodeId} -> {_connection.TargetNodeId}";

        public RemoveConnectionUndoableCommand(DesignViewModel vm, ConnectionViewModel connection)
        {
            _vm = vm;
            _connection = connection;
        }

        public void Execute()
        {
            _vm.DisconnectPortsInternal(_connection);
        }

        public void Unexecute()
        {
            _vm.ConnectPortsInternal(_connection.SourcePort, _connection.TargetPort);
        }
    }

    #endregion
}
