using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using Point = System.Windows.Point;
using VisionStudio.App.Converters;
using VisionStudio.App.Services;
using VisionStudio.Core.Models;
using VisionStudio.Core.Overlays;
using System.IO;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Pipeline;
using VisionStudio.Core.Tools;

namespace VisionStudio.App.ViewModels;

public enum InteractiveToolMode
{
    PanZoom,
    RectangleRoi,
    TeachTarget,
    Eraser,
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
    public bool SelectedNodeSupportsLine => SelectedNode?.Parameters.Any(p => p.Name is "StartX" or "StartY" or "EndX" or "EndY" or "X1" or "Y1" or "X2" or "Y2" or "BaseStartX" or "LineStartX") == true;
    public bool SelectedNodeSupportsPoint => SelectedNode?.Parameters.Any(p => p.Name is "CenterX" or "CenterY" or "X" or "Y" or "PointX" or "PointY" or "X1" or "Y1") == true;
    public bool SelectedNodeSupportsPolygon => SelectedNode?.Parameters.Any(p => p.Name is "Points" or "PolygonPoints") == true;
    public bool SelectedNodeSupportsTeach => SelectedNode?.ToolId.Contains("CartonCounter", StringComparison.OrdinalIgnoreCase) == true
        || SelectedNode?.Parameters.Any(p => p.Name is "TeachTarget" or "TeachTemplate") == true;
    public bool SelectedNodeSupportsClearRoi => SelectedNodeSupportsRoi || SelectedNodeSupportsTeach || SelectedNodeSupportsPolygon;
    public bool SelectedNodeSupportsInteractiveSelection => SelectedNodeSupportsRoi || SelectedNodeSupportsLine || SelectedNodeSupportsPoint || SelectedNodeSupportsPolygon || SelectedNodeSupportsTeach;
    public bool SelectedNodeIsCounter => SelectedNode?.ToolId.Contains("CartonCounter", StringComparison.OrdinalIgnoreCase) == true;

    [ObservableProperty]
    private bool _isGridSnapEnabled = true;

    [ObservableProperty]
    private bool _isAutoRunEnabled = true;

    [ObservableProperty]
    private bool _isRunning = false;

    [ObservableProperty]
    private bool _isContinuousRunning = false;

    [ObservableProperty]
    private string _continuousRunButtonText = "▶️ Phát Video";

    [ObservableProperty]
    private bool _hasVideoSource = false;

    [ObservableProperty]
    private int _videoCurrentFrame = 0;

    [ObservableProperty]
    private int _videoTotalFrames = 0;

    [ObservableProperty]
    private string _videoTimeInfo = string.Empty;

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
        OnPropertyChanged(nameof(SelectedNodeSupportsTeach));
        OnPropertyChanged(nameof(SelectedNodeSupportsClearRoi));
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
    public static string ResolveSampleImagePath(string fileName)
    {
        var cleanName = Path.GetFileName(fileName);
        var p1 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "samples", "images", cleanName);
        if (File.Exists(p1)) return p1;

        var p2 = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../../samples/images", cleanName));
        if (File.Exists(p2)) return p2;

        var p3 = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../samples/images", cleanName));
        if (File.Exists(p3)) return p3;

        var p4 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
        if (File.Exists(p4)) return p4;

        return p1;
    }

    public static string ResolvePipelinePath(string fileName)
    {
        var cleanName = Path.GetFileName(fileName);
        var p1 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "samples", "pipelines", cleanName);
        if (File.Exists(p1)) return p1;

        var p2 = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../../samples/pipelines", cleanName));
        if (File.Exists(p2)) return p2;

        var p3 = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../samples/pipelines", cleanName));
        if (File.Exists(p3)) return p3;

        return p1;
    }

    public static string ResolveSampleVideoPath(string fileName)
    {
        var cleanName = Path.GetFileName(fileName);
        var p1 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "samples", "videos", cleanName);
        if (File.Exists(p1)) return p1;

        var p2 = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../../samples/videos", cleanName));
        if (File.Exists(p2)) return p2;

        var p3 = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../samples/videos", cleanName));
        if (File.Exists(p3)) return p3;

        var p4 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
        if (File.Exists(p4)) return p4;

        return p1;
    }

    /// <summary>
    /// Loads a pipeline directly from a .vspipe.json document, resolving sample image paths and restoring all connections.
    /// </summary>
    public async Task<bool> LoadPipelineFileAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                var candidate = ResolvePipelinePath(filePath);
                if (File.Exists(candidate)) filePath = candidate;
                else return false;
            }

            var json = await File.ReadAllTextAsync(filePath);
            var doc = System.Text.Json.JsonSerializer.Deserialize<SerializedPipelineDocument>(json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (doc == null) return false;

            var origAutoRun = IsAutoRunEnabled;
            IsAutoRunEnabled = false;

            try
            {
                ResetPipeline();
                _undoRedo.Clear();

                foreach (var c in Connections.ToList()) DisconnectPortsInternal(c);
                foreach (var n in Nodes.ToList()) _graph.RemoveNode(n.NodeId);
                Nodes.Clear();
                InspectionStrip.Clear();
                _nodeCounter = 1;

                var nodeMap = new Dictionary<string, NodeViewModel>(StringComparer.OrdinalIgnoreCase);

                foreach (var nodeDto in doc.Nodes)
                {
                    if (!_toolRegistry.HasTool(nodeDto.ToolId)) continue;
                    var nodeVm = AddNodeInternal(nodeDto.ToolId, nodeDto.X, nodeDto.Y);
                    nodeVm.HasBreakpoint = nodeDto.HasBreakpoint;

                    if (nodeDto.Parameters != null)
                    {
                        foreach (var (pName, pVal) in nodeDto.Parameters)
                        {
                            var targetParam = nodeVm.Parameters.FirstOrDefault(p => p.Name.Equals(pName, StringComparison.OrdinalIgnoreCase));
                            if (targetParam != null && pVal != null)
                            {
                                if (targetParam.IsFilePath || pName.Contains("File", StringComparison.OrdinalIgnoreCase))
                                {
                                    var fileStr = pVal.ToString() ?? "";
                                    var ext = Path.GetExtension(fileStr).ToLowerInvariant();
                                    var resolved = (ext is ".avi" or ".mp4" or ".mkv" or ".mov" or ".wmv")
                                        ? ResolveSampleVideoPath(fileStr)
                                        : ResolveSampleImagePath(fileStr);
                                    targetParam.StringValue = File.Exists(resolved) ? resolved : fileStr;
                                }
                                else if (pVal is System.Text.Json.JsonElement je)
                                {
                                    if (targetParam.IsDouble && je.TryGetDouble(out var d)) targetParam.DoubleValue = d;
                                    else if (targetParam.IsInteger && je.TryGetInt32(out var i)) targetParam.IntValue = i;
                                    else if (targetParam.IsBoolean) targetParam.BoolValue = je.GetBoolean();
                                    else targetParam.StringValue = je.GetString() ?? je.ToString();
                                }
                                else
                                {
                                    if (targetParam.IsDouble) targetParam.DoubleValue = Convert.ToDouble(pVal);
                                    else if (targetParam.IsInteger) targetParam.IntValue = Convert.ToInt32(pVal);
                                    else if (targetParam.IsBoolean) targetParam.BoolValue = Convert.ToBoolean(pVal);
                                    else targetParam.StringValue = pVal.ToString() ?? "";
                                }
                            }
                        }
                    }

                    nodeMap[nodeDto.Id] = nodeVm;
                }

                foreach (var connDto in doc.Connections)
                {
                    if (nodeMap.TryGetValue(connDto.SourceNodeId, out var srcNode) &&
                        nodeMap.TryGetValue(connDto.TargetNodeId, out var dstNode))
                    {
                        try
                        {
                            ConnectPortsInternal(srcNode, connDto.SourcePortId, dstNode, connDto.TargetPortId);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to connect {Src} -> {Dst}", connDto.SourcePortId, connDto.TargetPortId);
                        }
                    }
                }

                SelectedNode = Nodes.FirstOrDefault(n => n.ToolId.Contains("CartonCounter"))
                    ?? Nodes.LastOrDefault(n => !n.ToolId.Contains("CameraGrab") && !n.ToolId.Contains("Judge") && !n.ToolId.Contains("Output"))
                    ?? Nodes.LastOrDefault(n => !n.ToolId.Contains("CameraGrab"))
                    ?? Nodes.LastOrDefault();
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

            _logger.LogInformation("Successfully loaded pipeline file {Path}", filePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load pipeline file {Path}", filePath);
            return false;
        }
    }

    /// <summary>
    /// Loads a full industrial sample inspection program
    /// (CameraGrab with real sample test image -> Grayscale -> Gaussian -> Sub-pixel Caliper -> SPC Process Capability),
    /// wires all ports, runs execution immediately, and highlights the measurement result.
    /// </summary>
    [RelayCommand]
    public async Task LoadSampleInspectionPipelineAsync()
    {
        // First try to load from the .vspipe.json file
        if (await LoadPipelineFileAsync("01_caliper_dimension_measurement.vspipe.json"))
        {
            return;
        }

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
                // Node 1: Camera Acquisition (with real sample stepped shaft image)
                var n1 = AddNodeInternal("Acquisition.CameraGrab", 50, 90);
                var sampleImg = ResolveSampleImagePath("01_stepped_shaft_dimension.png");
                var pFilePath = n1.Parameters.FirstOrDefault(p => p.Name == "FilePath");
                if (pFilePath != null && File.Exists(sampleImg)) pFilePath.StringValue = sampleImg;

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

                    if (pX1 != null) pX1.DoubleValue = 400.0;
                    if (pY1 != null) pY1.DoubleValue = 120.0;
                    if (pX2 != null) pX2.DoubleValue = 400.0;
                    if (pY2 != null) pY2.DoubleValue = 480.0;
                    if (pScale != null) pScale.DoubleValue = 0.1; // 1 px = 0.1 mm
                }

                // Node 5: SPC Process Capability / Tolerance Check
                var n5 = _toolRegistry.HasTool("Spc.ProcessCapability") ? AddNodeInternal("Spc.ProcessCapability", 1130, 90) : null;
                if (n5 != null)
                {
                    var pLsl = n5.Parameters.FirstOrDefault(p => p.Name == "LowerSpecLimit");
                    var pUsl = n5.Parameters.FirstOrDefault(p => p.Name == "UpperSpecLimit");
                    if (pLsl != null) pLsl.DoubleValue = 19.0;
                    if (pUsl != null) pUsl.DoubleValue = 21.0;
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
    /// Loads one of the 8 industrial application wizard presets with matching real test images.
    /// </summary>
    public async Task LoadPresetPipelineAsync(string presetType)
    {
        try
        {
            string fileName = presetType switch
            {
                "MeasureDistance" or "01" or "Caliper" => "01_caliper_dimension_measurement.vspipe.json",
                "ReadCodeAndGrade" or "02" or "Barcode" => "02_barcode_qr_and_iso_grading.vspipe.json",
                "SurfaceDefect" or "03" or "PcbDefect" => "03_pcb_surface_defect_detection.vspipe.json",
                "LocateAndPick" or "04" or "RobotGuidance" => "04_geometric_pattern_alignment_and_pick.vspipe.json",
                "PillCounting" or "05" or "Tablets" => "05_pill_particle_blob_counting.vspipe.json",
                "PresenceCheck" or "ColorCheck" or "06" => "06_color_sorting_presence_check.vspipe.json",
                "PolarUnwrap" or "07" or "SealRing" => "07_bottle_cap_polar_inspection.vspipe.json",
                "OcrExpiryDate" or "08" or "Ocr" => "08_optical_character_reading_ocr.vspipe.json",
                "CartonCounter" or "BoxCounting" or "09" => "09_carton_deeplearning_cpm_counter.vspipe.json",
                "AppleCounter" or "FruitCounting" or "10" => "10_apple_counter_quality_inspection.vspipe.json",
                "BagCounter" or "BagCounting" or "11" => "11_bag_counter_directional.vspipe.json",
                _ => "01_caliper_dimension_measurement.vspipe.json"
            };

            // Attempt file loading first
            if (await LoadPipelineFileAsync(fileName))
            {
                return;
            }

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
                    case "01":
                        await LoadSampleInspectionPipelineAsync();
                        return;

                    case "ReadCodeAndGrade":
                    case "02":
                        var cam2 = AddNodeInternal("Acquisition.CameraGrab", 50, 90);
                        var img2 = ResolveSampleImagePath("02_industrial_codes_label.png");
                        if (File.Exists(img2)) cam2.Parameters.FirstOrDefault(p => p.Name == "FilePath")!.StringValue = img2;

                        var gray2 = _toolRegistry.HasTool("Color.Grayscale") ? AddNodeInternal("Color.Grayscale", 320, 90) : null;
                        var barcode2 = _toolRegistry.HasTool("Identification.BarcodeReader") ? AddNodeInternal("Identification.BarcodeReader", 590, 90) : null;
                        var grade2 = _toolRegistry.HasTool("Grading.CodeQuality") ? AddNodeInternal("Grading.CodeQuality", 860, 90) : null;

                        if (cam2 != null && gray2 != null) ConnectPortsInternal(cam2, "Image", gray2, "ImageIn");
                        if (gray2 != null && barcode2 != null) ConnectPortsInternal(gray2, "ImageOut", barcode2, "ImageIn");
                        if (barcode2 != null && grade2 != null)
                        {
                            ConnectPortsInternal(barcode2, "ImageOut", grade2, "ImageIn");
                            ConnectPortsInternal(barcode2, "DecodedText", grade2, "DecodedTextIn");
                        }
                        SelectedNode = grade2 ?? barcode2 ?? cam2;
                        break;

                    case "SurfaceDefect":
                    case "03":
                        var cam3 = AddNodeInternal("Acquisition.CameraGrab", 50, 90);
                        var img3 = ResolveSampleImagePath("03_pcb_circuit_defect.png");
                        if (File.Exists(img3)) cam3.Parameters.FirstOrDefault(p => p.Name == "FilePath")!.StringValue = img3;

                        var gray3 = _toolRegistry.HasTool("Color.Grayscale") ? AddNodeInternal("Color.Grayscale", 320, 90) : null;
                        var sharpen3 = _toolRegistry.HasTool("Filter.Sharpen") ? AddNodeInternal("Filter.Sharpen", 590, 90) : null;
                        var thresh3 = _toolRegistry.HasTool("Segmentation.Threshold") ? AddNodeInternal("Segmentation.Threshold", 860, 90) : null;
                        var blob3 = _toolRegistry.HasTool("Blob.Analysis") ? AddNodeInternal("Blob.Analysis", 1130, 90) : null;

                        if (cam3 != null && gray3 != null) ConnectPortsInternal(cam3, "Image", gray3, "ImageIn");
                        if (gray3 != null && sharpen3 != null) ConnectPortsInternal(gray3, "ImageOut", sharpen3, "ImageIn");
                        if (sharpen3 != null && thresh3 != null) ConnectPortsInternal(sharpen3, "ImageOut", thresh3, "ImageIn");
                        if (thresh3 != null && blob3 != null) ConnectPortsInternal(thresh3, "ImageOut", blob3, "ImageIn");
                        SelectedNode = blob3 ?? thresh3 ?? cam3;
                        break;

                    case "LocateAndPick":
                    case "04":
                        var cam4 = AddNodeInternal("Acquisition.CameraGrab", 50, 90);
                        var img4 = ResolveSampleImagePath("04_gear_part_alignment.png");
                        if (File.Exists(img4)) cam4.Parameters.FirstOrDefault(p => p.Name == "FilePath")!.StringValue = img4;

                        var gray4 = _toolRegistry.HasTool("Color.Grayscale") ? AddNodeInternal("Color.Grayscale", 320, 90) : null;
                        var thresh4 = _toolRegistry.HasTool("Segmentation.Threshold") ? AddNodeInternal("Segmentation.Threshold", 590, 90) : null;
                        var blob4 = _toolRegistry.HasTool("Blob.Analysis") ? AddNodeInternal("Blob.Analysis", 860, 90) : null;
                        var align4 = _toolRegistry.HasTool("Matching.PatternAlign") ? AddNodeInternal("Matching.PatternAlign", 1130, 90) : null;

                        if (cam4 != null && gray4 != null) ConnectPortsInternal(cam4, "Image", gray4, "ImageIn");
                        if (gray4 != null && thresh4 != null) ConnectPortsInternal(gray4, "ImageOut", thresh4, "ImageIn");
                        if (thresh4 != null && blob4 != null) ConnectPortsInternal(thresh4, "ImageOut", blob4, "ImageIn");
                        if (blob4 != null && align4 != null)
                        {
                            ConnectPortsInternal(blob4, "PrimaryCenterX", align4, "CurrentX");
                            ConnectPortsInternal(blob4, "PrimaryCenterY", align4, "CurrentY");
                        }
                        SelectedNode = align4 ?? blob4 ?? cam4;
                        break;

                    case "PillCounting":
                    case "05":
                        var cam5 = AddNodeInternal("Acquisition.CameraGrab", 50, 90);
                        var img5 = ResolveSampleImagePath("05_pharmaceutical_tablets.png");
                        if (File.Exists(img5)) cam5.Parameters.FirstOrDefault(p => p.Name == "FilePath")!.StringValue = img5;

                        var gray5 = _toolRegistry.HasTool("Color.Grayscale") ? AddNodeInternal("Color.Grayscale", 320, 90) : null;
                        var thresh5 = _toolRegistry.HasTool("Segmentation.Threshold") ? AddNodeInternal("Segmentation.Threshold", 590, 90) : null;
                        var morph5 = _toolRegistry.HasTool("Morphology.Basic") ? AddNodeInternal("Morphology.Basic", 860, 90) : null;
                        var blob5 = _toolRegistry.HasTool("Blob.Analysis") ? AddNodeInternal("Blob.Analysis", 1130, 90) : null;

                        if (cam5 != null && gray5 != null) ConnectPortsInternal(cam5, "Image", gray5, "ImageIn");
                        if (gray5 != null && thresh5 != null) ConnectPortsInternal(gray5, "ImageOut", thresh5, "ImageIn");
                        if (thresh5 != null && morph5 != null) ConnectPortsInternal(thresh5, "ImageOut", morph5, "ImageIn");
                        if (morph5 != null && blob5 != null) ConnectPortsInternal(morph5, "ImageOut", blob5, "ImageIn");
                        SelectedNode = blob5 ?? thresh5 ?? cam5;
                        break;

                    case "PresenceCheck":
                    case "ColorCheck":
                    case "06":
                        var cam6 = AddNodeInternal("Acquisition.CameraGrab", 50, 90);
                        var img6 = ResolveSampleImagePath("06_colored_bottle_caps.png");
                        if (File.Exists(img6)) cam6.Parameters.FirstOrDefault(p => p.Name == "FilePath")!.StringValue = img6;

                        var crop6 = _toolRegistry.HasTool("Geometry.Crop") ? AddNodeInternal("Geometry.Crop", 320, 90) : null;
                        var color6 = _toolRegistry.HasTool("Identification.ColorCheck") ? AddNodeInternal("Identification.ColorCheck", 590, 90) : null;
                        var pres6 = _toolRegistry.HasTool("Identification.PresenceAbsence") ? AddNodeInternal("Identification.PresenceAbsence", 860, 90) : null;

                        if (cam6 != null && crop6 != null) ConnectPortsInternal(cam6, "Image", crop6, "ImageIn");
                        if (crop6 != null && color6 != null) ConnectPortsInternal(crop6, "ImageOut", color6, "ImageIn");
                        if (crop6 != null && pres6 != null) ConnectPortsInternal(crop6, "ImageOut", pres6, "ImageIn");
                        SelectedNode = color6 ?? pres6 ?? crop6 ?? cam6;
                        break;

                    case "PolarUnwrap":
                    case "07":
                        var cam7 = AddNodeInternal("Acquisition.CameraGrab", 50, 90);
                        var img7 = ResolveSampleImagePath("07_circular_seal_ring.png");
                        if (File.Exists(img7)) cam7.Parameters.FirstOrDefault(p => p.Name == "FilePath")!.StringValue = img7;

                        var gray7 = _toolRegistry.HasTool("Color.Grayscale") ? AddNodeInternal("Color.Grayscale", 320, 90) : null;
                        var polar7 = _toolRegistry.HasTool("Geometry.PolarUnwrap") ? AddNodeInternal("Geometry.PolarUnwrap", 590, 90) : null;
                        if (polar7 != null) polar7.Parameters.FirstOrDefault(p => p.Name == "MaxRadius")!.DoubleValue = 260.0;

                        var gaus7 = _toolRegistry.HasTool("Filter.Gaussian") ? AddNodeInternal("Filter.Gaussian", 860, 90) : null;
                        var caliper7 = _toolRegistry.HasTool("Measurement.Caliper") ? AddNodeInternal("Measurement.Caliper", 1130, 90) : null;

                        if (cam7 != null && gray7 != null) ConnectPortsInternal(cam7, "Image", gray7, "ImageIn");
                        if (gray7 != null && polar7 != null) ConnectPortsInternal(gray7, "ImageOut", polar7, "ImageIn");
                        if (polar7 != null && gaus7 != null) ConnectPortsInternal(polar7, "ImageOut", gaus7, "ImageIn");
                        if (gaus7 != null && caliper7 != null) ConnectPortsInternal(gaus7, "ImageOut", caliper7, "ImageIn");
                        SelectedNode = caliper7 ?? polar7 ?? cam7;
                        break;

                    case "OcrExpiryDate":
                    case "08":
                    default:
                        var cam8 = AddNodeInternal("Acquisition.CameraGrab", 50, 90);
                        var img8 = ResolveSampleImagePath("08_food_pack_expiry_date.png");
                        if (File.Exists(img8)) cam8.Parameters.FirstOrDefault(p => p.Name == "FilePath")!.StringValue = img8;

                        var gray8 = _toolRegistry.HasTool("Color.Grayscale") ? AddNodeInternal("Color.Grayscale", 320, 90) : null;
                        var sharp8 = _toolRegistry.HasTool("Filter.Sharpen") ? AddNodeInternal("Filter.Sharpen", 590, 90) : null;
                        var ocr8 = _toolRegistry.HasTool("Ocr.TrainableOcr") ? AddNodeInternal("Ocr.TrainableOcr", 860, 90) : null;
                        if (ocr8 != null) ocr8.Parameters.FirstOrDefault(p => p.Name == "ExpectedString")!.StringValue = "EXP: 2028-12-31";

                        if (cam8 != null && gray8 != null) ConnectPortsInternal(cam8, "Image", gray8, "ImageIn");
                        if (gray8 != null && sharp8 != null) ConnectPortsInternal(gray8, "ImageOut", sharp8, "ImageIn");
                        if (sharp8 != null && ocr8 != null) ConnectPortsInternal(sharp8, "ImageOut", ocr8, "ImageIn");
                        SelectedNode = ocr8 ?? sharp8 ?? cam8;
                        break;

                    case "CartonCounter":
                    case "BoxCounting":
                    case "09":
                        var cam9 = _toolRegistry.HasTool("Acquisition.VideoFile")
                            ? AddNodeInternal("Acquisition.VideoFile", 60, 90)
                            : AddNodeInternal("Acquisition.CameraGrab", 60, 90);
                        var video9 = ResolveSampleVideoPath("09_conveyor_boxes_flow.avi");
                        if (File.Exists(video9) && cam9 != null)
                        {
                            var pPath = cam9.Parameters.FirstOrDefault(p => p.Name == "FilePath");
                            if (pPath != null) pPath.StringValue = video9;
                        }

                        var carton9 = _toolRegistry.HasTool("DeepLearning.CartonCounter")
                            ? AddNodeInternal("DeepLearning.CartonCounter", 480, 90)
                            : null;
                        var judge9 = _toolRegistry.HasTool("Output.Judge")
                            ? AddNodeInternal("Output.Judge", 900, 90)
                            : null;

                        if (cam9 != null && carton9 != null)
                        {
                            ConnectPortsInternal(cam9, "Image", carton9, "ImageIn");
                        }
                        if (carton9 != null && judge9 != null)
                        {
                            ConnectPortsInternal(carton9, "BoxesPerMinute", judge9, "Value");
                        }
                        SelectedNode = carton9 ?? judge9 ?? cam9;
                        break;

                    case "AppleCounter":
                    case "FruitCounting":
                    case "10":
                        var cam10 = _toolRegistry.HasTool("Acquisition.VideoFile")
                            ? AddNodeInternal("Acquisition.VideoFile", 60, 90)
                            : AddNodeInternal("Acquisition.CameraGrab", 60, 90);
                        var apple10 = _toolRegistry.HasTool("DeepLearning.CartonCounter")
                            ? AddNodeInternal("DeepLearning.CartonCounter", 480, 90)
                            : null;
                        if (apple10 != null)
                        {
                            var pObj = apple10.Parameters.FirstOrDefault(p => p.Name == "TargetObjectType");
                            if (pObj != null) pObj.StringValue = "Apple_Fruit";
                            var pHud = apple10.Parameters.FirstOrDefault(p => p.Name == "HudDashboardLayout");
                            if (pHud != null) pHud.StringValue = "RightDashboard";
                            var pBadge = apple10.Parameters.FirstOrDefault(p => p.Name == "BadgeStyle");
                            if (pBadge != null) pBadge.StringValue = "VibrantPill";
                        }
                        var judge10 = _toolRegistry.HasTool("Output.Judge")
                            ? AddNodeInternal("Output.Judge", 920, 90)
                            : null;

                        if (cam10 != null && apple10 != null)
                        {
                            ConnectPortsInternal(cam10, "Image", apple10, "ImageIn");
                        }
                        if (apple10 != null && judge10 != null)
                        {
                            ConnectPortsInternal(apple10, "QualityPercent", judge10, "Value");
                        }
                        SelectedNode = apple10 ?? judge10 ?? cam10;
                        break;

                    case "BagCounter":
                    case "BagCounting":
                    case "11":
                        var cam11 = _toolRegistry.HasTool("Acquisition.VideoFile")
                            ? AddNodeInternal("Acquisition.VideoFile", 60, 90)
                            : AddNodeInternal("Acquisition.CameraGrab", 60, 90);
                        var bag11 = _toolRegistry.HasTool("DeepLearning.CartonCounter")
                            ? AddNodeInternal("DeepLearning.CartonCounter", 480, 90)
                            : null;
                        if (bag11 != null)
                        {
                            var pObj = bag11.Parameters.FirstOrDefault(p => p.Name == "TargetObjectType");
                            if (pObj != null) pObj.StringValue = "Bag_Package";
                            var pHud = bag11.Parameters.FirstOrDefault(p => p.Name == "HudDashboardLayout");
                            if (pHud != null) pHud.StringValue = "TopBanner";
                            var pBadge = bag11.Parameters.FirstOrDefault(p => p.Name == "BadgeStyle");
                            if (pBadge != null) pBadge.StringValue = "VibrantPill";
                        }
                        var judge11 = _toolRegistry.HasTool("Output.Judge")
                            ? AddNodeInternal("Output.Judge", 920, 90)
                            : null;

                        if (cam11 != null && bag11 != null)
                        {
                            ConnectPortsInternal(cam11, "Image", bag11, "ImageIn");
                        }
                        if (bag11 != null && judge11 != null)
                        {
                            ConnectPortsInternal(bag11, "BoxesPerMinute", judge11, "Value");
                        }
                        SelectedNode = bag11 ?? judge11 ?? cam11;
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

    private bool _hasPendingRun;
    private CancellationTokenSource? _continuousCts;

    [RelayCommand]
    public async Task RunPipelineAsync()
    {
        if (IsRunning)
        {
            _hasPendingRun = true;
            return;
        }

        IsRunning = true;
        try
        {
            do
            {
                _hasPendingRun = false;
                _runCts = new CancellationTokenSource();
                foreach (var n in _graph.Nodes) n.MarkDirty();
                var report = await _executor.ExecuteAsync(_graph, stopAtBreakpoints: false, _runCts.Token);
                ApplyExecutionReport(report);
            } while (_hasPendingRun);
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
    public async Task ToggleContinuousRunAsync()
    {
        if (IsContinuousRunning)
        {
            StopContinuousRun();
            return;
        }

        IsContinuousRunning = true;
        ContinuousRunButtonText = "⏹️ Dừng Video";
        _continuousCts = new CancellationTokenSource();
        var ct = _continuousCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                while (!ct.IsCancellationRequested && IsContinuousRunning)
                {
                    foreach (var n in _graph.Nodes) n.MarkDirty();

                    var report = await _executor.ExecuteAsync(_graph, stopAtBreakpoints: false, ct);

                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        ApplyExecutionReport(report);
                        if (SelectedNode != null)
                        {
                            UpdateViewerFromNode(SelectedNode);
                        }
                    });

                    var delay = Math.Max(10, 33 - (int)report.TotalTime.TotalMilliseconds);
                    await Task.Delay(delay, ct);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Continuous video playback loop error.");
            }
            finally
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    IsContinuousRunning = false;
                    ContinuousRunButtonText = "▶️ Phát Video";
                });
            }
        }, ct);
    }

    public void StopContinuousRun()
    {
        if (!IsContinuousRunning) return;
        _continuousCts?.Cancel();
        IsContinuousRunning = false;
        ContinuousRunButtonText = "▶️ Phát Video";
    }

    public IVideoSeekable? GetActiveVideoSource()
    {
        foreach (var node in _graph.Nodes)
        {
            if (node.Tool is IVideoSeekable seekable)
            {
                return seekable;
            }
        }
        return null;
    }

    public void UpdateVideoState()
    {
        var v = GetActiveVideoSource();
        if (v != null && v.IsVideoLoaded)
        {
            HasVideoSource = true;
            VideoCurrentFrame = v.CurrentFrame;
            VideoTotalFrames = Math.Max(VideoCurrentFrame, v.TotalFrames);

            double fps = v.Fps > 0 ? v.Fps : 30.0;
            var curTime = TimeSpan.FromSeconds(v.CurrentFrame / fps);
            var totTime = TimeSpan.FromSeconds(VideoTotalFrames / fps);
            VideoTimeInfo = $"{curTime:mm\\:ss\\.f} / {totTime:mm\\:ss\\.f} (Frame #{v.CurrentFrame}/{VideoTotalFrames})";
        }
        else
        {
            HasVideoSource = v != null;
            if (v != null)
            {
                VideoCurrentFrame = v.CurrentFrame;
                VideoTotalFrames = v.TotalFrames;
            }
        }
    }

    public void PreviewSeekTime(int targetFrame)
    {
        var v = GetActiveVideoSource();
        if (v != null)
        {
            double fps = v.Fps > 0 ? v.Fps : 30.0;
            int total = Math.Max(targetFrame, VideoTotalFrames);
            var curTime = TimeSpan.FromSeconds(targetFrame / fps);
            var totTime = TimeSpan.FromSeconds(total / fps);
            VideoTimeInfo = $"{curTime:mm\\:ss\\.f} / {totTime:mm\\:ss\\.f} (Tua tới #{targetFrame}/{total})";
        }
    }

    [RelayCommand]
    public void RestartVideo()
    {
        var v = GetActiveVideoSource();
        if (v != null)
        {
            v.Restart();
            foreach (var n in _graph.Nodes)
            {
                n.CachedResult = null;
                n.IsDirty = true;
                if (n.Tool is not IVideoSeekable)
                {
                    try { n.Tool?.Reset(); } catch { }
                }
            }
            if (!IsContinuousRunning)
            {
                _ = RunPipelineAsync();
            }
            UpdateVideoState();
            InteractiveStatusHint = "⏮️ Đã phát lại video từ đầu (Frame #0) và đặt lại bộ đếm!";
        }
        else
        {
            ResetPipeline();
            if (!IsContinuousRunning)
            {
                _ = RunPipelineAsync();
            }
            InteractiveStatusHint = "⏮️ Đã đặt lại trạng thái xử lý kịch bản!";
        }
    }

    [RelayCommand]
    public void StepForwardVideo()
    {
        SeekVideoTo(VideoCurrentFrame + 30);
    }

    [RelayCommand]
    public void StepBackwardVideo()
    {
        SeekVideoTo(Math.Max(0, VideoCurrentFrame - 30));
    }

    public void SeekVideoTo(int targetFrame)
    {
        var v = GetActiveVideoSource();
        if (v != null)
        {
            v.Seek(targetFrame);
            // If seeking, reset downstream trackers so counts don't jump wildly
            foreach (var n in _graph.Nodes)
            {
                if (n.Tool is not IVideoSeekable)
                {
                    n.CachedResult = null;
                    n.IsDirty = true;
                    try { n.Tool?.Reset(); } catch { }
                }
                else
                {
                    n.CachedResult = null;
                    n.IsDirty = true;
                }
            }

            if (!IsContinuousRunning)
            {
                _ = RunPipelineAsync();
            }
            UpdateVideoState();
            InteractiveStatusHint = $"⏩ Đã tua tới Frame #{targetFrame}/{VideoTotalFrames}";
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

        // Ensure every node in the pipeline has a valid thumbnail (fallback to associated upstream image)
        foreach (var nodeVm in Nodes)
        {
            if (nodeVm.Thumbnail == null)
            {
                var frame = FindAssociatedImageFrame(nodeVm);
                if (frame != null && !frame.Mat.IsDisposed)
                {
                    nodeVm.Thumbnail = ImageConversionHelper.CreateThumbnail(frame.Mat, 96);
                }
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

        UpdateVideoState();
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
            try { pNode.Tool?.Reset(); } catch { }
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

        // Find primary output image directly or trace upstream through the DAG
        var frame = FindAssociatedImageFrame(node);

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

        // Overlays: If current node has its own overlays (e.g. SPC text box, Caliper lines, Bounding boxes), show them!
        if (res.Overlays != null && res.Overlays.Count > 0)
        {
            DisplayedOverlays = res.Overlays;
        }
        else
        {
            // If current node has no overlays of its own, check if the upstream node that produced the image has overlays
            DisplayedOverlays = FindUpstreamOverlays(node);
        }
    }

    /// <summary>
    /// Finds the active image frame associated with a node:
    /// 1. Direct output ImageFrame from this node.
    /// 2. Upstream DAG incoming connections trace.
    /// 3. Backwards sequence fallback.
    /// 4. Any camera or image node in the active graph.
    /// </summary>
    public ImageFrame? FindAssociatedImageFrame(NodeViewModel? node)
    {
        if (node == null) return null;

        // 1. Direct output from node itself
        if (node.LastResult != null)
        {
            foreach (var val in node.LastResult.Outputs.Values)
            {
                if (val is ImageFrame f && !f.Mat.IsDisposed) return f;
            }
        }

        // 2. Upstream DAG search via incoming connections (BFS)
        var visited = new HashSet<string> { node.NodeId };
        var queue = new Queue<NodeViewModel>();

        foreach (var conn in Connections)
        {
            if (conn.TargetPort?.Node?.NodeId == node.NodeId && conn.SourcePort?.Node != null)
            {
                if (visited.Add(conn.SourcePort.Node.NodeId))
                {
                    queue.Enqueue(conn.SourcePort.Node);
                }
            }
        }

        while (queue.Count > 0)
        {
            var curr = queue.Dequeue();
            if (curr.LastResult != null)
            {
                foreach (var val in curr.LastResult.Outputs.Values)
                {
                    if (val is ImageFrame f && !f.Mat.IsDisposed) return f;
                }
            }

            foreach (var conn in Connections)
            {
                if (conn.TargetPort?.Node?.NodeId == curr.NodeId && conn.SourcePort?.Node != null)
                {
                    if (visited.Add(conn.SourcePort.Node.NodeId))
                    {
                        queue.Enqueue(conn.SourcePort.Node);
                    }
                }
            }
        }

        // 3. Fallback: Search backwards in Nodes list
        int idx = Nodes.IndexOf(node);
        if (idx > 0)
        {
            for (int i = idx - 1; i >= 0; i--)
            {
                var prev = Nodes[i];
                if (prev.LastResult != null)
                {
                    foreach (var val in prev.LastResult.Outputs.Values)
                    {
                        if (val is ImageFrame f && !f.Mat.IsDisposed) return f;
                    }
                }
            }
        }

        // 4. Fallback: Any node in the entire graph with an active image (e.g. CameraGrab)
        foreach (var n in Nodes)
        {
            if (n.LastResult != null)
            {
                foreach (var val in n.LastResult.Outputs.Values)
                {
                    if (val is ImageFrame f && !f.Mat.IsDisposed) return f;
                }
            }
        }

        return null;
    }

    private IReadOnlyList<IOverlayPrimitive>? FindUpstreamOverlays(NodeViewModel node)
    {
        var visited = new HashSet<string> { node.NodeId };
        var queue = new Queue<NodeViewModel>();

        foreach (var conn in Connections)
        {
            if (conn.TargetPort?.Node?.NodeId == node.NodeId && conn.SourcePort?.Node != null)
            {
                if (visited.Add(conn.SourcePort.Node.NodeId))
                    queue.Enqueue(conn.SourcePort.Node);
            }
        }

        while (queue.Count > 0)
        {
            var curr = queue.Dequeue();
            if (curr.LastResult?.Overlays != null && curr.LastResult.Overlays.Count > 0)
            {
                return curr.LastResult.Overlays;
            }

            foreach (var conn in Connections)
            {
                if (conn.TargetPort?.Node?.NodeId == curr.NodeId && conn.SourcePort?.Node != null)
                {
                    if (visited.Add(conn.SourcePort.Node.NodeId))
                        queue.Enqueue(conn.SourcePort.Node);
                }
            }
        }

        return null;
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

        var frame = FindAssociatedImageFrame(SelectedNode);

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
                InteractiveModeText = "Chế độ: Vùng Quét Kiểm Tra ROI (Scan ROI)";
                InteractiveStatusHint = SelectedNode != null
                    ? $"Kéo chuột trái trên ảnh để gán VÙNG QUÉT ROI cho [{SelectedNode.DisplayName}]"
                    : "Kéo chuột trái trên ảnh để vẽ vùng quét ROI";
                RequestSwitchToImageViewer?.Invoke();
                break;

            case InteractiveToolMode.TeachTarget:
                InteractiveModeText = "Chế độ: Dạy Mẫu Vật Thể (Teach Target)";
                InteractiveStatusHint = SelectedNode != null
                    ? $"👉 Kéo chuột trái khoanh mẫu vật thể chuẩn trên ảnh để Dạy Mẫu cho [{SelectedNode.DisplayName}]"
                    : "Kéo chuột trái khoanh mẫu vật thể chuẩn để Dạy Mẫu (Teach Target)";
                RequestSwitchToImageViewer?.Invoke();
                break;

            case InteractiveToolMode.Eraser:
                InteractiveModeText = "Chế độ: Cục Tẩy Xóa Vùng Quét ROI (Eraser)";
                InteractiveStatusHint = SelectedNode != null
                    ? $"🧽 Nhấp chuột trái trên ảnh để TẨY SẠCH vùng quét ROI lỡ khoanh của [{SelectedNode.DisplayName}]"
                    : "Nhấp chuột trái trên ảnh để tẩy sạch vùng quét ROI";
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
    public void TeachSelectedNodeTarget()
    {
        if (SelectedNode == null) return;
        SetInteractionMode(InteractiveToolMode.TeachTarget);
        InteractiveStatusHint = $"👉 Kéo chuột trái khoanh mẫu vật thể chuẩn trên ảnh: Hệ thống sẽ tạo VÙNG MẪU DẠY RIÊNG BIỆT và khóa nhận diện ngay!";
    }

    [RelayCommand]
    public void EraseSelectedNodeScanRoi()
    {
        if (SelectedNode == null) return;

        var origAuto = IsAutoRunEnabled;
        try
        {
            IsAutoRunEnabled = false;
            foreach (var name in new[] { "RoiX", "RoiY", "RoiWidth", "RoiHeight", "ScanRoiX", "ScanRoiY", "ScanRoiWidth", "ScanRoiHeight" })
            {
                var p = SelectedNode.Parameters.FirstOrDefault(param => param.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (p != null) p.DoubleValue = 0.0;
            }
        }
        finally
        {
            IsAutoRunEnabled = origAuto;
        }

        var pNode = _graph.GetNode(SelectedNode.NodeId);
        if (pNode?.Tool is VisionStudio.DeepLearning.Tools.CartonThroughputCounterTool cartonTool)
        {
            cartonTool.ResetScanRoi();
        }

        SetInteractionMode(InteractiveToolMode.PanZoom);
        pNode?.MarkDirty();
        _ = RunPipelineAsync();
        InteractiveStatusHint = $"🧽 ĐÃ TẨY VÙNG QUÉT ROI của [{SelectedNode.DisplayName}]! Quay lại quét toàn màn hình.";
    }

    [RelayCommand]
    public void ClearSelectedNodeRoi()
    {
        if (SelectedNode == null) return;

        var origAuto = IsAutoRunEnabled;
        try
        {
            IsAutoRunEnabled = false;

            // Clear box & ROI coordinates
            foreach (var name in new[] { "RoiX", "RoiY", "RoiWidth", "RoiHeight",
                                         "X", "Y", "Width", "Height",
                                         "OriginX", "OriginY", "CropX", "CropY", "Left", "Top", "TopLeftX", "TopLeftY",
                                         "CenterX", "CenterY", "Radius", "OuterRadius", "InnerRadius" })
            {
                var p = SelectedNode.Parameters.FirstOrDefault(param => param.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (p != null) p.DoubleValue = 0.0;
            }

            // If node is a pure line/caliper tool without box ROI, clear line coords
            if (SelectedNodeSupportsLine && !SelectedNodeSupportsRoi)
            {
                foreach (var name in new[] { "StartX", "StartY", "EndX", "EndY", "X1", "Y1", "X2", "Y2", "BaseStartX", "BaseStartY", "BaseEndX", "BaseEndY" })
                {
                    var p = SelectedNode.Parameters.FirstOrDefault(param => param.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                    if (p != null) p.DoubleValue = 0.0;
                }
            }

            var pTeach = SelectedNode.Parameters.FirstOrDefault(p => p.Name is "TeachTarget" or "TeachTemplate");
            if (pTeach != null) pTeach.BoolValue = false;

            var pIsTeached = SelectedNode.Parameters.FirstOrDefault(p => p.Name is "IsTeached");
            if (pIsTeached != null) pIsTeached.BoolValue = false;

            var pTargetType = SelectedNode.Parameters.FirstOrDefault(p => p.Name is "TargetObjectType");
            if (pTargetType != null && pTargetType.StringValue == "CustomTaught") pTargetType.StringValue = "General";
        }
        finally
        {
            IsAutoRunEnabled = origAuto;
        }

        var pNode = _graph.GetNode(SelectedNode.NodeId);
        if (pNode?.Tool is VisionStudio.DeepLearning.Tools.CartonThroughputCounterTool cartonTool)
        {
            cartonTool.ResetTeach();
            cartonTool.Reset();
        }

        SetInteractionMode(InteractiveToolMode.PanZoom);

        pNode?.MarkDirty();
        _ = RunPipelineAsync();
        InteractiveStatusHint = $"🧹 Đã xóa sạch vùng chọn ROI và hủy mẫu dạy của [{SelectedNode.DisplayName}]!";
    }

    [RelayCommand]
    public void ResetSelectedNodeTeach()
    {
        if (SelectedNode == null) return;

        var pTeach = SelectedNode.Parameters.FirstOrDefault(p => p.Name is "TeachTarget" or "TeachTemplate");
        if (pTeach != null) pTeach.BoolValue = false;

        var pIsTeached = SelectedNode.Parameters.FirstOrDefault(p => p.Name is "IsTeached");
        if (pIsTeached != null) pIsTeached.BoolValue = false;

        var pTargetType = SelectedNode.Parameters.FirstOrDefault(p => p.Name is "TargetObjectType");
        if (pTargetType != null) pTargetType.StringValue = "General";

        var pNode = _graph.GetNode(SelectedNode.NodeId);
        if (pNode?.Tool is VisionStudio.DeepLearning.Tools.CartonThroughputCounterTool cartonTool)
        {
            cartonTool.ResetTeach();
            cartonTool.Reset();
        }

        pNode?.MarkDirty();
        _ = RunPipelineAsync();
        InteractiveStatusHint = $"🔄 Đã hủy mẫu đã dạy của [{SelectedNode.DisplayName}]! Quay lại chế độ nhận diện tự động tổng quát.";
    }

    [RelayCommand]
    public void SelectLineOnImage() => SetInteractionMode(InteractiveToolMode.LineCaliper);

    [RelayCommand]
    public void SelectPointOnImage() => SetInteractionMode(InteractiveToolMode.PointPicker);

    [RelayCommand]
    public void SelectPolygonOnImage() => SetInteractionMode(InteractiveToolMode.PolygonRoi);

    [RelayCommand]
    public void ResetInteractionMode() => SetInteractionMode(InteractiveToolMode.PanZoom);

    public (Point P1, Point P2)? GetSelectedNodeLinePoints()
    {
        if (SelectedNode == null) return null;
        var pStartX = SelectedNode.Parameters.FirstOrDefault(p => p.Name is "StartX" or "X1" or "Start_X" or "BaseStartX" or "LineStartX");
        var pStartY = SelectedNode.Parameters.FirstOrDefault(p => p.Name is "StartY" or "Y1" or "Start_Y" or "BaseStartY" or "LineStartY");
        var pEndX = SelectedNode.Parameters.FirstOrDefault(p => p.Name is "EndX" or "X2" or "End_X" or "BaseEndX" or "LineEndX");
        var pEndY = SelectedNode.Parameters.FirstOrDefault(p => p.Name is "EndY" or "Y2" or "End_Y" or "BaseEndY" or "LineEndY");

        if (pStartX != null && pStartY != null && pEndX != null && pEndY != null)
        {
            if (pStartX.DoubleValue == 0 && pStartY.DoubleValue == 0 && pEndX.DoubleValue == 0 && pEndY.DoubleValue == 0)
                return null;

            return (new Point(pStartX.DoubleValue, pStartY.DoubleValue), new Point(pEndX.DoubleValue, pEndY.DoubleValue));
        }
        return null;
    }

    [RelayCommand]
    public void NudgeLine(string direction)
    {
        var pts = GetSelectedNodeLinePoints();
        if (pts == null) return;
        double step = 10.0;
        double dx = 0, dy = 0;
        switch (direction?.ToLowerInvariant())
        {
            case "up": dy = -step; break;
            case "down": dy = step; break;
            case "left": dx = -step; break;
            case "right": dx = step; break;
            case "extend":
                var d = pts.Value.P2 - pts.Value.P1;
                var len = d.Length;
                if (len > 1.0)
                {
                    var u = d / len;
                    ApplyInteractiveLine(pts.Value.P1.X - u.X * 15, pts.Value.P1.Y - u.Y * 15, pts.Value.P2.X + u.X * 15, pts.Value.P2.Y + u.Y * 15);
                }
                return;
            case "shorten":
                var d2 = pts.Value.P2 - pts.Value.P1;
                var len2 = d2.Length;
                if (len2 > 40.0)
                {
                    var u2 = d2 / len2;
                    ApplyInteractiveLine(pts.Value.P1.X + u2.X * 15, pts.Value.P1.Y + u2.Y * 15, pts.Value.P2.X - u2.X * 15, pts.Value.P2.Y - u2.Y * 15);
                }
                return;
        }

        ApplyInteractiveLine(pts.Value.P1.X + dx, pts.Value.P1.Y + dy, pts.Value.P2.X + dx, pts.Value.P2.Y + dy);
    }

    [RelayCommand]
    public void SetLineHorizontal()
    {
        var pts = GetSelectedNodeLinePoints();
        double midY = pts.HasValue ? (pts.Value.P1.Y + pts.Value.P2.Y) / 2.0 : 300.0;
        double minX = pts.HasValue ? Math.Min(pts.Value.P1.X, pts.Value.P2.X) : 60.0;
        double maxX = pts.HasValue ? Math.Max(pts.Value.P1.X, pts.Value.P2.X) : 740.0;
        if (maxX - minX < 80.0)
        {
            minX = 60.0;
            maxX = 740.0;
        }
        ApplyInteractiveLine(minX, midY, maxX, midY);
    }

    [RelayCommand]
    public void SetLineVertical()
    {
        var pts = GetSelectedNodeLinePoints();
        double midX = pts.HasValue ? (pts.Value.P1.X + pts.Value.P2.X) / 2.0 : 400.0;
        double minY = pts.HasValue ? Math.Min(pts.Value.P1.Y, pts.Value.P2.Y) : 60.0;
        double maxY = pts.HasValue ? Math.Max(pts.Value.P1.Y, pts.Value.P2.Y) : 540.0;
        if (maxY - minY < 80.0)
        {
            minY = 60.0;
            maxY = 540.0;
        }
        ApplyInteractiveLine(midX, minY, midX, maxY);
    }

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

        var origAuto = IsAutoRunEnabled;
        try
        {
            IsAutoRunEnabled = false;

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
        }
        finally
        {
            IsAutoRunEnabled = origAuto;
        }

        if (updated)
        {
            var isCounter = SelectedNode.ToolId.Contains("CartonCounter", StringComparison.OrdinalIgnoreCase);
            if (isCounter)
            {
                InteractiveStatusHint = $"🔲 ĐÃ GÁN VÙNG QUÉT KIỂM TRA ROI [X:{(int)x}, Y:{(int)y}, W:{(int)w}, H:{(int)h}] cho [{SelectedNode.DisplayName}]! Hệ thống chỉ quét tìm vật thể trong vùng này.";
            }
            else
            {
                InteractiveStatusHint = $"Đã gán ROI [X:{(int)x}, Y:{(int)y}, W:{(int)w}, H:{(int)h}] cho [{SelectedNode.DisplayName}]";
            }

            SetInteractionMode(InteractiveToolMode.PanZoom);

            var pNodeToRun = _graph.GetNode(SelectedNode.NodeId);
            pNodeToRun?.MarkDirty();
            _ = RunPipelineAsync();
        }
        return updated;
    }

    public bool ApplyInteractiveTeachTarget(double x, double y, double w, double h)
    {
        if (SelectedNode == null) return false;

        var pTx = SelectedNode.Parameters.FirstOrDefault(p => p.Name is "TeachRoiX");
        var pTy = SelectedNode.Parameters.FirstOrDefault(p => p.Name is "TeachRoiY");
        var pTw = SelectedNode.Parameters.FirstOrDefault(p => p.Name is "TeachRoiWidth");
        var pTh = SelectedNode.Parameters.FirstOrDefault(p => p.Name is "TeachRoiHeight");

        var origAuto = IsAutoRunEnabled;
        try
        {
            IsAutoRunEnabled = false;
            if (pTx != null) pTx.DoubleValue = Math.Round(x);
            if (pTy != null) pTy.DoubleValue = Math.Round(y);
            if (pTw != null) pTw.DoubleValue = Math.Round(w);
            if (pTh != null) pTh.DoubleValue = Math.Round(h);
        }
        finally
        {
            IsAutoRunEnabled = origAuto;
        }

        var pNode = _graph.GetNode(SelectedNode.NodeId);
        var cartonTool = pNode?.Tool as VisionStudio.DeepLearning.Tools.CartonThroughputCounterTool;
        var activeFrame = FindAssociatedImageFrame(SelectedNode);

        if (w >= 15 && h >= 15)
        {
            if (activeFrame != null && !activeFrame.Mat.IsDisposed && cartonTool != null)
            {
                cartonTool.TeachFromRoi(activeFrame.Mat, x, y, w, h);
            }

            var pTeach = SelectedNode.Parameters.FirstOrDefault(p => p.Name is "TeachTarget" or "TeachTemplate");
            if (pTeach != null) pTeach.BoolValue = false;

            var pIsTeached = SelectedNode.Parameters.FirstOrDefault(p => p.Name is "IsTeached");
            if (pIsTeached != null) pIsTeached.BoolValue = true;

            var pTargetType = SelectedNode.Parameters.FirstOrDefault(p => p.Name is "TargetObjectType");
            if (pTargetType != null) pTargetType.StringValue = "CustomTaught";

            InteractiveStatusHint = $"🎯 ĐÃ DẠY MẪU VẬT THỂ THÀNH CÔNG [{(int)w}x{(int)h}px] cho [{SelectedNode.DisplayName}]! Đã lưu vùng mẫu dạy riêng biệt, không đè vùng quét ROI.";
        }
        else
        {
            InteractiveStatusHint = $"⚠️ Vùng khoanh mẫu quá nhỏ (W:{(int)w}x{(int)h}px < 15px), vui lòng khoanh trọn vật thể để học mẫu!";
        }

        SetInteractionMode(InteractiveToolMode.PanZoom);

        var pNodeToRun = _graph.GetNode(SelectedNode.NodeId);
        pNodeToRun?.MarkDirty();
        _ = RunPipelineAsync();
        return true;
    }

    public bool ApplyInteractiveLine(double x1, double y1, double x2, double y2)
    {
        if (SelectedNode == null) return false;

        var pStartX = SelectedNode.Parameters.FirstOrDefault(p => p.Name is "StartX" or "X1" or "Start_X" or "BaseStartX" or "LineStartX");
        var pStartY = SelectedNode.Parameters.FirstOrDefault(p => p.Name is "StartY" or "Y1" or "Start_Y" or "BaseStartY" or "LineStartY");
        var pEndX = SelectedNode.Parameters.FirstOrDefault(p => p.Name is "EndX" or "X2" or "End_X" or "BaseEndX" or "LineEndX");
        var pEndY = SelectedNode.Parameters.FirstOrDefault(p => p.Name is "EndY" or "Y2" or "End_Y" or "BaseEndY" or "LineEndY");

        if (pStartX != null && pStartY != null && pEndX != null && pEndY != null)
        {
            var origAuto = IsAutoRunEnabled;
            try
            {
                IsAutoRunEnabled = false;
                pStartX.DoubleValue = Math.Round(x1, 1);
                pStartY.DoubleValue = Math.Round(y1, 1);
                pEndX.DoubleValue = Math.Round(x2, 1);
                pEndY.DoubleValue = Math.Round(y2, 1);
            }
            finally
            {
                IsAutoRunEnabled = origAuto;
            }

            var len = Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
            var isCounter = SelectedNode.ToolId.Contains("CartonCounter", StringComparison.OrdinalIgnoreCase);
            InteractiveStatusHint = isCounter
                ? $"Đã gán vạch đếm thùng L:{len:F1}px [({(int)x1},{(int)y1}) -> ({(int)x2},{(int)y2})] cho [{SelectedNode.DisplayName}]"
                : $"Đã gán thước Caliper L:{len:F1}px [({(int)x1},{(int)y1}) -> ({(int)x2},{(int)y2})] cho [{SelectedNode.DisplayName}]";
            
            var pNode = _graph.GetNode(SelectedNode.NodeId);
            pNode?.MarkDirty();
            _ = RunPipelineAsync();
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
