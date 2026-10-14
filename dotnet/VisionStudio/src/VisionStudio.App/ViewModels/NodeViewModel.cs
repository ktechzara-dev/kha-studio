using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using VisionStudio.App.Converters;
using VisionStudio.App.Services;
using VisionStudio.Core.Models;
using VisionStudio.Core.Pipeline;
using VisionStudio.Core.Tools;

namespace VisionStudio.App.ViewModels;

/// <summary>
/// ViewModel representing an executable tool node on the interactive pipeline canvas.
/// </summary>
public partial class NodeViewModel : ObservableObject
{
    private readonly Action<NodeViewModel>? _onParameterChanged;

    public string NodeId { get; }
    public IVisionTool Tool { get; }
    public string ToolId => Tool.Id;
    public string DisplayName => Tool.DisplayName;
    public string Category => Tool.Category;
    public string Description => Tool.Description;

    public ToolDocumentation Guide => ToolGuideService.GetGuide(ToolId, DisplayName, Category);

    public string GuideTitle => LocalizationService.Instance.CurrentLanguage == AppLanguage.Vietnamese
        ? Guide.DisplayNameVi : Guide.DisplayNameEn;

    public string GuidePrinciple => LocalizationService.Instance.CurrentLanguage == AppLanguage.Vietnamese
        ? Guide.PrincipleVi : Guide.PrincipleEn;

    public string GuidePorts => LocalizationService.Instance.CurrentLanguage == AppLanguage.Vietnamese
        ? Guide.PortsGuideVi : Guide.PortsGuideEn;

    public string GuideParameters => LocalizationService.Instance.CurrentLanguage == AppLanguage.Vietnamese
        ? Guide.ParametersGuideVi : Guide.ParametersGuideEn;

    public string GuideUseCases => LocalizationService.Instance.CurrentLanguage == AppLanguage.Vietnamese
        ? Guide.IndustrialUseCasesVi : Guide.IndustrialUseCasesEn;

    public string GuideTips => LocalizationService.Instance.CurrentLanguage == AppLanguage.Vietnamese
        ? Guide.QuickTipsVi : Guide.QuickTipsEn;

    public string IllustrationDiagram => Guide.IllustrationDiagram;

    [ObservableProperty]
    private double _x;

    [ObservableProperty]
    private double _y;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _hasBreakpoint;

    [ObservableProperty]
    private ToolStatus? _status = null;

    public string StatusText => Status?.ToString() ?? "Idle";

    [ObservableProperty]
    private double _executionTimeMs = 0.0;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private BitmapSource? _thumbnail;

    [ObservableProperty]
    private ToolResult? _lastResult;

    public ObservableCollection<PortViewModel> InputPorts { get; } = new();
    public ObservableCollection<PortViewModel> OutputPorts { get; } = new();
    public ObservableCollection<ParameterEditorViewModel> Parameters { get; } = new();
    public ObservableCollection<KeyValuePair<string, string>> OutputValuesSummary { get; } = new();

    public const double NodeWidth = 220;
    public const double HeaderHeight = 40;
    public const double PortRowHeight = 28;

    public double EstimatedHeight => HeaderHeight + Math.Max(InputPorts.Count, OutputPorts.Count) * PortRowHeight + 20;

    public NodeViewModel(string nodeId, IVisionTool tool, double x = 100, double y = 100, Action<NodeViewModel>? onParameterChanged = null)
    {
        NodeId = nodeId ?? throw new ArgumentNullException(nameof(nodeId));
        Tool = tool ?? throw new ArgumentNullException(nameof(tool));
        _x = x;
        _y = y;
        _onParameterChanged = onParameterChanged;

        foreach (var port in Tool.Inputs)
        {
            InputPorts.Add(new PortViewModel(this, port));
        }

        foreach (var port in Tool.Outputs)
        {
            OutputPorts.Add(new PortViewModel(this, port));
        }

        foreach (var param in Tool.Parameters)
        {
            Parameters.Add(new ParameterEditorViewModel(param, _ => _onParameterChanged?.Invoke(this)));
        }

        UpdatePortPositions();

        LocalizationService.Instance.PropertyChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(GuideTitle));
            OnPropertyChanged(nameof(GuidePrinciple));
            OnPropertyChanged(nameof(GuidePorts));
            OnPropertyChanged(nameof(GuideParameters));
            OnPropertyChanged(nameof(GuideUseCases));
            OnPropertyChanged(nameof(GuideTips));
        };
    }

    partial void OnXChanged(double value) => UpdatePortPositions();
    partial void OnYChanged(double value) => UpdatePortPositions();
    partial void OnStatusChanged(ToolStatus? value) => OnPropertyChanged(nameof(StatusText));

    /// <summary>
    /// Calculates the absolute canvas coordinates for all input and output port pins.
    /// </summary>
    public void UpdatePortPositions()
    {
        // Inputs on Left edge (X + 0), Outputs on Right edge (X + NodeWidth)
        for (int i = 0; i < InputPorts.Count; i++)
        {
            var portY = Y + HeaderHeight + (i * PortRowHeight) + (PortRowHeight / 2.0);
            InputPorts[i].CanvasPosition = new Point(X, portY);
        }

        for (int i = 0; i < OutputPorts.Count; i++)
        {
            var portY = Y + HeaderHeight + (i * PortRowHeight) + (PortRowHeight / 2.0);
            OutputPorts[i].CanvasPosition = new Point(X + NodeWidth, portY);
        }
    }

    /// <summary>
    /// Updates the execution status, metrics, and thumbnail from a ToolResult.
    /// </summary>
    public void ApplyResult(ToolResult result)
    {
        LastResult = result;
        Status = result.Status;
        ExecutionTimeMs = result.ExecutionTime.TotalMilliseconds;
        ErrorMessage = result.Message;

        // Extract primary output image if any for thumbnail
        ImageFrame? outputFrame = null;
        OutputValuesSummary.Clear();

        foreach (var kvp in result.Outputs)
        {
            if (kvp.Value is ImageFrame frame)
            {
                outputFrame ??= frame;
                OutputValuesSummary.Add(new KeyValuePair<string, string>(kvp.Key, $"{frame.Width}x{frame.Height} ({frame.Channels}ch)"));
            }
            else
            {
                OutputValuesSummary.Add(new KeyValuePair<string, string>(kvp.Key, kvp.Value?.ToString() ?? "null"));
            }
        }

        if (outputFrame != null)
        {
            Thumbnail = ImageConversionHelper.CreateThumbnail(outputFrame.Mat, 96);
        }
    }
}
