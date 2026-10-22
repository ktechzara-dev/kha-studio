using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using VisionStudio.App.Controls;
using VisionStudio.App.Converters;
using VisionStudio.App.Services;
using VisionStudio.Core.Logging;
using VisionStudio.Core.Recipes;
using VisionStudio.Core.Tools;
using VisionStudio.Core.Traceability;

namespace VisionStudio.App.ViewModels;

public enum ApplicationMode
{
    Design,
    Run
}

public enum WorkspaceTab
{
    PipelineCanvas,
    ImageViewer,
    SplitView,
    TelemetryAndDevices
}

/// <summary>
/// Root ViewModel for VisionStudio main shell window.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly ILogger<MainViewModel> _logger;
    private readonly IInAppLogSink _logSink;
    private readonly ToolRegistry _toolRegistry;

    [ObservableProperty]
    private string _title = "VisionStudio 2026 - Industrial Machine Vision Research Studio";

    [ObservableProperty]
    private ApplicationMode _currentMode = ApplicationMode.Design;

    [ObservableProperty]
    private WorkspaceTab _activeTab = WorkspaceTab.PipelineCanvas;

    [ObservableProperty]
    private string _statusMessage = "Sẵn sàng";

    [ObservableProperty]
    private int _logCount = 0;

    public DesignViewModel DesignVm { get; }
    public LocalizationService Loc => LocalizationService.Instance;

    public ObservableCollection<LogEntry> Logs { get; } = new();

    public MainViewModel(ILogger<MainViewModel> logger, IInAppLogSink logSink, DesignViewModel designVm, ToolRegistry toolRegistry)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logSink = logSink ?? throw new ArgumentNullException(nameof(logSink));
        DesignVm = designVm ?? throw new ArgumentNullException(nameof(designVm));
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));

        // Subscribe to log sink
        _logSink.LogEmitted += OnLogEmitted;

        // Subscribe to language changes
        Loc.PropertyChanged += (_, _) =>
        {
            StatusMessage = CurrentMode == ApplicationMode.Design
                ? Loc["Status_DesignMode"]
                : Loc["Status_RunMode"];
        };

        // Auto switch tab to ImageViewer if user clicks interactive selection button
        DesignVm.RequestSwitchToImageViewer += () =>
        {
            if (ActiveTab == WorkspaceTab.PipelineCanvas)
            {
                ActiveTab = WorkspaceTab.ImageViewer;
            }
        };

        StatusMessage = Loc["Status_DesignMode"];
        _logger.LogInformation("VisionStudio application initialized in {Mode} mode.", CurrentMode);
    }

    private void OnLogEmitted(LogEntry entry)
    {
        var app = Application.Current;
        if (app != null && !app.Dispatcher.HasShutdownStarted)
        {
            app.Dispatcher.BeginInvoke(() =>
            {
                Logs.Add(entry);
                while (Logs.Count > 500)
                {
                    Logs.RemoveAt(0);
                }
                LogCount = Logs.Count;
            });
        }
    }

    [RelayCommand]
    private void SwitchMode(ApplicationMode mode)
    {
        CurrentMode = mode;
        StatusMessage = mode == ApplicationMode.Design
            ? Loc["Status_DesignMode"]
            : Loc["Status_RunMode"];
        _logger.LogInformation("Switched operational mode to {Mode}", mode);
    }

    [RelayCommand]
    private void SetTab(WorkspaceTab tab)
    {
        ActiveTab = tab;
        _logger.LogInformation("Switched workspace view tab to {Tab}", tab);
    }

    [RelayCommand]
    private async Task ImportTestImageAsync()
    {
        await DesignVm.ImportTestImageAsync();
        ActiveTab = WorkspaceTab.ImageViewer;
    }

    [RelayCommand]
    private void ToggleLanguage()
    {
        Loc.ToggleLanguage();
        _logger.LogInformation("Language switched to {Lang}", Loc.CurrentLanguage);
    }

    [RelayCommand]
    private void OpenToolGuide()
    {
        try
        {
            var guideWindow = new ToolGuideWindow(_toolRegistry);
            guideWindow.Owner = Application.Current?.MainWindow;
            guideWindow.Show();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open Tool Guide window.");
        }
    }

    [RelayCommand]
    private void OpenCameraManager()
    {
        try
        {
            var vm = new CameraManagerViewModel(
                onSnapshotToPipeline: frame =>
                {
                    DesignVm.DisplayedImage = ImageConversionHelper.MatToBitmapSource(frame.Mat);
                    DesignVm.DisplayedTitle = $"Camera Snapshot (#{frame.FrameId})";
                    DesignVm.DisplayedImageInfo = $"{frame.Width} x {frame.Height} px | Frame #{frame.FrameId} | {frame.CameraId}";
                    _logger.LogInformation("Camera snapshot Frame #{FrameId} loaded into pipeline.", frame.FrameId);
                });

            var camWindow = new Controls.CameraManagerWindow(vm)
            {
                Owner = Application.Current?.MainWindow
            };
            camWindow.Show();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open Camera Manager window.");
        }
    }

    [RelayCommand]
    private void OpenModelEditor()
    {
        try
        {
            var editorWindow = new Controls.GeometricModelEditorWindow
            {
                Owner = Application.Current?.MainWindow
            };
            editorWindow.Show();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open Geometric Model Editor window.");
        }
    }

    [RelayCommand]
    private void OpenCoordinateFrames()
    {
        try
        {
            var framesWindow = new Controls.CoordinateFrameTreeWindow
            {
                Owner = Application.Current?.MainWindow
            };
            framesWindow.Show();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open Coordinate Frames window.");
        }
    }

    [RelayCommand]
    private void OpenMacroLibrary()
    {
        try
        {
            var macroWin = new Controls.MacroLibraryWindow
            {
                Owner = Application.Current?.MainWindow
            };
            macroWin.Show();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open Macro Library window.");
        }
    }

    [RelayCommand]
    private void OpenAutoTune()
    {
        try
        {
            string? selId = DesignVm.SelectedNode?.NodeId;
            var autoTuneWin = new Controls.AutoTuneWindow(DesignVm.Graph, selId)
            {
                Owner = Application.Current?.MainWindow
            };
            autoTuneWin.Show();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open Auto-Tune window.");
        }
    }

    [RelayCommand]
    private void OpenStateMachine()
    {
        try
        {
            var smWin = new Controls.StateMachineEditorWindow
            {
                Owner = Application.Current?.MainWindow
            };
            smWin.Show();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open State Machine Editor window.");
        }
    }

    [RelayCommand]
    private void OpenDeepLearningWorkspace()
    {
        try
        {
            var dlWin = new Controls.DeepLearningWorkspaceWindow
            {
                Owner = Application.Current?.MainWindow
            };
            dlWin.Show();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open Deep Learning Workspace window.");
        }
    }

    [RelayCommand]
    private void OpenRecipeManager()
    {
        try
        {
            var recipeWin = new Controls.RecipeManagerWindow(new RecipeManager(), DesignVm.Graph)
            {
                Owner = Application.Current?.MainWindow
            };
            recipeWin.Show();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open Recipe Manager window.");
        }
    }

    [RelayCommand]
    private void OpenTraceabilityHistory()
    {
        try
        {
            var traceWin = new Controls.TraceabilityHistoryWindow(new TraceabilityStore(), DesignVm.Graph)
            {
                Owner = Application.Current?.MainWindow
            };
            traceWin.Show();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open Traceability History window.");
        }
    }

    [RelayCommand]
    private void OpenIndustrialConnectivity()
    {
        try
        {
            var connWin = new Controls.IndustrialConnectivityWindow
            {
                Owner = Application.Current?.MainWindow
            };
            connWin.Show();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open Industrial Connectivity window.");
        }
    }

    [RelayCommand]
    private void OpenHmiDesigner()
    {
        try
        {
            var hmiWin = new Controls.HmiDesignerWindow
            {
                Owner = Application.Current?.MainWindow
            };
            hmiWin.Show();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open HMI Designer window.");
        }
    }

    [RelayCommand]
    private void OpenCommandPalette()
    {
        try
        {
            var items = new List<Controls.CommandPaletteItem>
            {
                new("🖥️", "Màn Hình HMI & Web Server", "Mở trình thiết kế HMI và máy chủ Web HMI Kestrel", "Giao Diện", OpenHmiDesigner),
                new("🌐", "Kết Nối IIoT Hub", "Cấu hình OPC UA, MQTT, SQL Logging và Lighting", "Kết Nối", OpenIndustrialConnectivity),
                new("🧠", "Deep Learning Workspace", "Quản lý dataset, gán nhãn và huấn luyện ONNX", "Trí Tuệ Nhân Tạo", OpenDeepLearningWorkspace),
                new("⚙️", "Industrial State Machine", "Trình thiết kế và mô phỏng máy trạng thái dây chuyền", "Tự Động Hoá", OpenStateMachine),
                new("📷", "Quản Lý Camera Industrial", "Tìm kiếm, kết nối và cấu hình tham số camera GenICam", "Thiết Bị", OpenCameraManager),
                new("📜", "Truy Xuất Lịch Sử (Audit Replay)", "Tra cứu lịch sử kiểm tra, hash chain và chạy lại offline", "Chất Lượng", OpenTraceabilityHistory),
                new("🎯", "Mẫu Hình Học (Geometric Matcher)", "Trình tạo mẫu biên dạng hình học và toạ độ fixturing", "Thuật Toán", OpenModelEditor),
                new("📦", "Thư Viện Khối Macro", "Quản lý các khối Macro sub-pipeline tái sử dụng", "Mô-đun", OpenMacroLibrary),
                new("🎛️", "Quản Lý Recipe Sản Phẩm", "Tạo, chỉnh sửa và chuyển đổi công thức mã hàng", "Sản Xuất", OpenRecipeManager),
                new("⚡", "Kho 8 Kịch Bản & Ảnh Mẫu (Quick Build)", "Mở bảng chọn trực quan 8 chuyên đề thị giác công nghiệp kèm ảnh mẫu", "Mẫu Công Nghiệp", OpenQuickBuildWizard),
                new("📐", "1. Đo Caliper & Năng Lực SPC (Trục bậc)", "Nạp mẫu đo kích thước phôi cơ khí Sub-pixel và tính Cp/Cpk (01_stepped_shaft_dimension.png)", "Kịch Bản Mẫu", () => { _ = DesignVm.LoadPresetPipelineAsync("measure"); }),
                new("🏷️", "2. Barcode, QR & Phân Cấp ISO/IEC (Bao bì)", "Nạp mẫu đọc mã vạch công nghiệp, DataMatrix, QR và ISO grading (02_industrial_codes_label.png)", "Kịch Bản Mẫu", () => { _ = DesignVm.LoadPresetPipelineAsync("barcode"); }),
                new("🔬", "3. Phát Hiện Khuyết Tật Bo Mạch SMT (PCB)", "Nạp mẫu phát hiện đứt đường mạch, hàn chập PCB (03_pcb_circuit_defect.png)", "Kịch Bản Mẫu", () => { _ = DesignVm.LoadPresetPipelineAsync("pcb"); }),
                new("🎯", "4. Định Vị Khớp Mẫu Robot Pick & Place (Gear)", "Nạp mẫu tìm toạ độ khớp mẫu bánh răng xoay 360 độ (04_gear_part_alignment.png)", "Kịch Bản Mẫu", () => { _ = DesignVm.LoadPresetPipelineAsync("alignment"); }),
                new("💊", "5. Đếm Hạt & Kích Thước Vỉ Thuốc (Dược phẩm)", "Nạp mẫu đếm số lượng viên thuốc nén, diện tích và phân loại hạt (05_pharmaceutical_tablets.png)", "Kịch Bản Mẫu", () => { _ = DesignVm.LoadPresetPipelineAsync("blob_counting"); }),
                new("🎨", "6. Phân Loại Màu Sắc CIE Delta-E (Nắp chai)", "Nạp mẫu phân loại màu sắc nắp chai đồ uống, kiểm tra lắp thiếu (06_colored_bottle_caps.png)", "Kịch Bản Mẫu", () => { _ = DesignVm.LoadPresetPipelineAsync("color_presence"); }),
                new("🔄", "7. Trải Phẳng Toạ Độ Cực Polar Unwrap (O-Ring)", "Nạp mẫu mở phẳng 360 độ gioăng cao su O-Ring tròn sang hình chữ nhật (07_circular_seal_ring.png)", "Kịch Bản Mẫu", () => { _ = DesignVm.LoadPresetPipelineAsync("polar_unwrap"); }),
                new("🔤", "8. Nhận Dạng Ký Tự Quang Học OCR/OCV (Hạn Dùng)", "Nạp mẫu đọc hạn dùng EXP/LOT in phun công nghiệp (08_food_pack_expiry_date.png)", "Kịch Bản Mẫu", () => { _ = DesignVm.LoadPresetPipelineAsync("ocr_expiry"); }),
                new("📦", "9. Đếm Thùng & Tốc Độ Thùng/Phút (AI Conveyor CPM)", "Nạp mẫu nhận dạng đếm thùng carton qua vạch và đo tốc độ CPM từ video (09_conveyor_boxes_flow.avi)", "Kịch Bản Mẫu", () => { _ = DesignVm.LoadPresetPipelineAsync("CartonCounter"); }),
                new("📂", "Mở Thư Mục Ảnh Test Mẫu (.png)", "Mở thư mục 8 tệp ảnh test độ phân giải cao trong Windows Explorer", "Tài Liệu Mẫu", OpenSampleImagesFolder),
                new("🎬", "Mở Thư Mục Video Test Mẫu (.avi)", "Mở thư mục video băng chuyền công nghiệp trong Windows Explorer", "Tài Liệu Mẫu", OpenSampleVideosFolder),
                new("📁", "Mở Thư Mục Kịch Bản Mẫu (.vspipe.json)", "Mở thư mục kịch bản pipeline đồ thị DAG trong Windows Explorer", "Tài Liệu Mẫu", OpenSamplePipelinesFolder),
                new("⚖️", "So Sánh Thuật Toán (Tool Compare A/B)", "So sánh song song 2 bộ tham số trên cùng ảnh kiểm", "Đo Kiểm", OpenToolCompare),
                new("▶️", "Chạy Pipeline (Run Once)", "Thực thi toàn bộ pipeline DAG hiện tại 1 chu kỳ", "Vận Hành", () => { _ = DesignVm.RunPipelineCommand.ExecuteAsync(null); }),
                new("🔄", "Phát / Dừng Video Liên Tục (Play Video CPM)", "Chạy liên tục chuỗi video và cập nhật tốc độ thùng/phút live", "Vận Hành", () => { _ = DesignVm.ToggleContinuousRunCommand.ExecuteAsync(null); })
            };

            var paletteWin = new Controls.CommandPaletteWindow(items)
            {
                Owner = Application.Current?.MainWindow
            };
            paletteWin.ShowDialog();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open Command Palette.");
        }
    }

    [RelayCommand]
    private async Task LoadSamplePipelineAsync()
    {
        await DesignVm.LoadSampleInspectionPipelineAsync();
        StatusMessage = "⭐ Đã nạp thành công Chương Trình Mẫu: Kiểm Tra Ngoại Quan & Đo Caliper Sub-pixel!";
    }

    [RelayCommand]
    private void OpenSampleImagesFolder()
    {
        try
        {
            var sampleFile = DesignViewModel.ResolveSampleImagePath("01_stepped_shaft_dimension.png");
            var dir = Path.GetDirectoryName(sampleFile);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = dir,
                    UseShellExecute = true
                });
                StatusMessage = $"📂 Đã mở thư mục ảnh test mẫu: {dir}";
            }
            else
            {
                StatusMessage = "⚠️ Không tìm thấy thư mục samples/images.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open sample images folder.");
        }
    }

    [RelayCommand]
    private void OpenSamplePipelinesFolder()
    {
        try
        {
            var samplePipe = DesignViewModel.ResolvePipelinePath("01_caliper_dimension_measurement.vspipe.json");
            var dir = Path.GetDirectoryName(samplePipe);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = dir,
                    UseShellExecute = true
                });
                StatusMessage = $"📁 Đã mở thư mục kịch bản mẫu: {dir}";
            }
            else
            {
                StatusMessage = "⚠️ Không tìm thấy thư mục samples/pipelines.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open sample pipelines folder.");
        }
    }

    [RelayCommand]
    private void OpenSampleVideosFolder()
    {
        try
        {
            var sampleVideo = DesignViewModel.ResolveSampleVideoPath("09_conveyor_boxes_flow.avi");
            var dir = Path.GetDirectoryName(sampleVideo);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = dir,
                    UseShellExecute = true
                });
                StatusMessage = $"🎬 Đã mở thư mục video mẫu: {dir}";
            }
            else
            {
                StatusMessage = "⚠️ Không tìm thấy thư mục samples/videos.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open sample videos folder.");
        }
    }

    [RelayCommand]
    private void OpenQuickBuildWizard()
    {
        try
        {
            var wizWin = new Controls.QuickBuildWizardWindow
            {
                Owner = Application.Current?.MainWindow,
                OnWizardSelected = wizardType =>
                {
                    _ = DesignVm.LoadPresetPipelineAsync(wizardType);
                    StatusMessage = $"⚡ Đã nạp kịch bản Pipeline mẫu: {wizardType}";
                }
            };
            wizWin.ShowDialog();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open Quick Build Wizard.");
        }
    }

    [RelayCommand]
    private void OpenToolCompare()
    {
        try
        {
            var cmpWin = new Controls.ToolCompareWindow
            {
                Owner = Application.Current?.MainWindow
            };
            cmpWin.Show();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open Tool Compare window.");
        }
    }

    [RelayCommand]
    private void ClearLogs()
    {
        _logSink.Clear();
        Logs.Clear();
        LogCount = 0;
        _logger.LogDebug("Logs cleared by user.");
    }
}
