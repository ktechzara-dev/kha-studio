using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VisionStudio.App.Services;

public enum AppLanguage
{
    Vietnamese,
    English
}

/// <summary>
/// Central localization service providing dynamic runtime language switching
/// between Vietnamese and English for all UI labels, controls, and guides.
/// </summary>
public sealed class LocalizationService : INotifyPropertyChanged
{
    private static readonly Lazy<LocalizationService> _instance = new(() => new LocalizationService());
    public static LocalizationService Instance => _instance.Value;

    private AppLanguage _currentLanguage = AppLanguage.Vietnamese;

    public event PropertyChangedEventHandler? PropertyChanged;

    public AppLanguage CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            if (_currentLanguage != value)
            {
                _currentLanguage = value;
                OnPropertyChanged();
                OnPropertyChanged("Item[]");
                OnPropertyChanged(nameof(CurrentLanguageName));
                OnPropertyChanged(nameof(SwitchLanguageButtonText));
            }
        }
    }

    public string CurrentLanguageName => _currentLanguage == AppLanguage.Vietnamese ? "Tiếng Việt" : "English";
    public string SwitchLanguageButtonText => _currentLanguage == AppLanguage.Vietnamese ? "🇻🇳 Tiếng Việt" : "🇬🇧 English";

    public void ToggleLanguage()
    {
        CurrentLanguage = CurrentLanguage == AppLanguage.Vietnamese ? AppLanguage.English : AppLanguage.Vietnamese;
    }

    public string this[string key] => Get(key);

    public string Get(string key)
    {
        if (string.IsNullOrEmpty(key)) return string.Empty;

        var dict = _currentLanguage == AppLanguage.Vietnamese ? VietnameseStrings : EnglishStrings;
        if (dict.TryGetValue(key, out var val))
        {
            return val;
        }

        // Fallback to English if missing in Vietnamese
        if (EnglishStrings.TryGetValue(key, out var enVal))
        {
            return enVal;
        }

        return key;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static readonly Dictionary<string, string> VietnameseStrings = new(StringComparer.OrdinalIgnoreCase)
    {
        // Top Bar
        ["App_Title"] = "VisionStudio 2026 - Nghiên Cứu & Phát Triển Thị Giác Máy Công Nghiệp",
        ["App_BrandSubtitle"] = " | Studio Thị Giác Máy Công Nghiệp",
        ["Mode_Design"] = "🛠 Chế độ Thiết kế",
        ["Mode_Run"] = "⚡ Chế độ Vận hành",
        ["Btn_Run"] = "▶ Chạy toàn bộ (F5)",
        ["Btn_Step"] = "⏭ Chạy từng bước (F10)",
        ["Btn_Breakpoint"] = "⏯ Đến điểm dừng",
        ["Btn_Reset"] = "⏹ Đặt lại",
        ["Btn_Undo"] = "↺ Hoàn tác",
        ["Btn_Redo"] = "↻ Làm lại",
        ["Chk_AutoRun"] = "Tự động chạy khi chỉnh số",
        ["Btn_Help"] = "📖 Hướng dẫn sử dụng",
        ["Btn_Camera"] = "📷 Camera Studio",

        // Toolbox
        ["Toolbox_Header"] = "DANH MỤC CÔNG CỤ",
        ["Toolbox_SearchPlaceholder"] = "Tìm kiếm công cụ vision...",
        ["Toolbox_AddTooltip"] = "Thêm công cụ này vào sơ đồ",

        // Canvas
        ["Canvas_GridSnap"] = "Bám lưới",
        ["Canvas_ResetView"] = "Căn góc nhìn",
        ["Canvas_BreakpointTooltip"] = "Bật/Tắt điểm dừng (F9)",
        ["Canvas_DeleteSelected"] = "Xóa đã chọn (Delete)",

        // Image Viewer
        ["Viewer_Fit"] = "Vừa khung",
        ["Viewer_ActualSize"] = "1:1 Chuẩn",
        ["Viewer_ZoomIn"] = "+",
        ["Viewer_ZoomOut"] = "−",
        ["Viewer_ShowOverlays"] = "Hiển thị đo (Overlays)",
        ["Viewer_NoImage"] = "Chưa có ảnh hiển thị",
        ["Viewer_NoResult"] = "Chưa có kết quả thực thi",
        ["Viewer_PixelInspection"] = "Lấy mẫu điểm ảnh: ",

        // Property Panel
        ["Props_Header"] = "THUỘC TÍNH & KẾT QUẢ ĐO",
        ["Props_NoSelectionTitle"] = "Chưa chọn khối công cụ",
        ["Props_NoSelectionDesc"] = "Nhấp chuột vào một node trên sơ đồ để tinh chỉnh tham số, xem giải thích chức năng và đọc kết quả đo đạc.",
        ["Props_ParametersSection"] = "THAM SỐ CẤU HÌNH",
        ["Props_OutputsSection"] = "KẾT QUẢ ĐO & TÍN HIỆU ĐẦU RA",
        ["Props_GuideSection"] = "💡 HƯỚNG DẪN DÙNG CÔNG CỤ NÀY",
        ["Props_Category"] = "Danh mục: ",
        ["Props_LastExec"] = "Thời gian xử lý: ",
        ["Props_Enabled"] = "Kích hoạt",

        // Inspection Strip
        ["Strip_Header"] = "🎞 DẢI BĂNG KIỂM TRA THEO TỪNG BƯỚC",
        ["Strip_Subtitle"] = "(Bấm vào ảnh thu nhỏ để xem chi tiết ảnh và các hình vẽ đo đạc của bước đó)",
        ["Strip_NoImage"] = "Không có ảnh",

        // System Logs
        ["Logs_Header"] = "NHẬT KÝ HỆ THỐNG & VẬN HÀNH",
        ["Logs_Entries"] = " bản ghi",
        ["Logs_Clear"] = "Xóa nhật ký",

        // Status Bar
        ["Status_Ready"] = "Sẵn sàng",
        ["Status_DesignMode"] = "Chế độ Thiết kế: Biên tập sơ đồ và tinh chỉnh tham số offline",
        ["Status_RunMode"] = "Chế độ Vận hành: Chạy liên tục thời gian thực và bắt tay PLC / Robot",
        ["Status_Version"] = "VisionStudio v1.0.0 | .NET 8 WPF",

        // Tool Categories
        ["Category_Acquisition"] = "Thu nhận ảnh (Acquisition)",
        ["Category_Geometry"] = "Hình học & ROI",
        ["Category_Color"] = "Xử lý màu & Cường độ",
        ["Category_Filtering"] = "Bộ lọc & Khử nhiễu",
        ["Category_Segmentation"] = "Phân đoạn ảnh",
        ["Category_Morphology"] = "Hình thái học (Morphology)",
        ["Category_Blob"] = "Phân tích Blob",
        ["Category_Edges"] = "Đường biên & Biên dạng (Edges)",
        ["Category_Fitting"] = "Khớp hình học (Geometry Fitting)",
        ["Category_Measurement"] = "Đo lường & Caliper",
        ["Category_Matching"] = "So khớp & Căn chỉnh (Matching)",
        ["Category_Identification"] = "Nhận diện & Đọc mã",
        ["Category_Defect"] = "Phát hiện lỗi bề mặt (Defect)",
        ["Category_Flow"] = "Điều khiển luồng (Flow Control)",
        ["Category_Output"] = "Xuất dữ liệu & Phán quyết"
    };

    private static readonly Dictionary<string, string> EnglishStrings = new(StringComparer.OrdinalIgnoreCase)
    {
        // Top Bar
        ["App_Title"] = "VisionStudio 2026 - Industrial Machine Vision Research Studio",
        ["App_BrandSubtitle"] = " | Machine Vision Studio",
        ["Mode_Design"] = "🛠 Design Mode",
        ["Mode_Run"] = "⚡ Run Mode",
        ["Btn_Run"] = "▶ Run (F5)",
        ["Btn_Step"] = "⏭ Step (F10)",
        ["Btn_Breakpoint"] = "⏯ Breakpoint",
        ["Btn_Reset"] = "⏹ Reset",
        ["Btn_Undo"] = "↺ Undo",
        ["Btn_Redo"] = "↻ Redo",
        ["Chk_AutoRun"] = "Auto-Run on Tune",
        ["Btn_Help"] = "📖 User Guide",
        ["Btn_Camera"] = "📷 Camera Studio",

        // Toolbox
        ["Toolbox_Header"] = "TOOLBOX CATALOG",
        ["Toolbox_SearchPlaceholder"] = "Search vision tools...",
        ["Toolbox_AddTooltip"] = "Add tool to canvas",

        // Canvas
        ["Canvas_GridSnap"] = "Grid Snap",
        ["Canvas_ResetView"] = "Reset View",
        ["Canvas_BreakpointTooltip"] = "Toggle Breakpoint (F9)",
        ["Canvas_DeleteSelected"] = "Delete Selected (Delete)",

        // Image Viewer
        ["Viewer_Fit"] = "Fit",
        ["Viewer_ActualSize"] = "1:1",
        ["Viewer_ZoomIn"] = "+",
        ["Viewer_ZoomOut"] = "−",
        ["Viewer_ShowOverlays"] = "Overlays",
        ["Viewer_NoImage"] = "No Image Selected",
        ["Viewer_NoResult"] = "No execution result available",
        ["Viewer_PixelInspection"] = "Pixel Inspection: ",

        // Property Panel
        ["Props_Header"] = "PROPERTIES & RESULTS",
        ["Props_NoSelectionTitle"] = "No Node Selected",
        ["Props_NoSelectionDesc"] = "Click a tool node on the pipeline canvas to inspect and tune its parameters in real-time.",
        ["Props_ParametersSection"] = "CONFIGURABLE PARAMETERS",
        ["Props_OutputsSection"] = "OUTPUTS & MEASUREMENTS",
        ["Props_GuideSection"] = "💡 HOW TO USE THIS TOOL",
        ["Props_Category"] = "Category: ",
        ["Props_LastExec"] = "Last run: ",
        ["Props_Enabled"] = "Enabled",

        // Inspection Strip
        ["Strip_Header"] = "🎞 IN-LINE INSPECTION STRIP",
        ["Strip_Subtitle"] = "(Click any node thumbnail to inspect full output image & overlays)",
        ["Strip_NoImage"] = "No Image",

        // System Logs
        ["Logs_Header"] = "SYSTEM & EXECUTION LOGS",
        ["Logs_Entries"] = " entries",
        ["Logs_Clear"] = "Clear Logs",

        // Status Bar
        ["Status_Ready"] = "Ready",
        ["Status_DesignMode"] = "Design Mode: Pipeline editing and offline parameter tuning",
        ["Status_RunMode"] = "Run Mode: Live continuous execution and PLC/Robot handshake",
        ["Status_Version"] = "VisionStudio v1.0.0 | .NET 8 WPF",

        // Tool Categories
        ["Category_Acquisition"] = "Acquisition",
        ["Category_Geometry"] = "Geometry / ROI",
        ["Category_Color"] = "Color / Intensity",
        ["Category_Filtering"] = "Filtering",
        ["Category_Segmentation"] = "Segmentation",
        ["Category_Morphology"] = "Morphology",
        ["Category_Blob"] = "Blob Analysis",
        ["Category_Edges"] = "Edges and Contours",
        ["Category_Fitting"] = "Geometry Fitting",
        ["Category_Measurement"] = "Measurement",
        ["Category_Matching"] = "Matching and Localization",
        ["Category_Identification"] = "Identification",
        ["Category_Defect"] = "Defect Detection",
        ["Category_Flow"] = "Flow Control",
        ["Category_Output"] = "Output"
    };
}
