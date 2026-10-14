using System.Text.Json;
using System.Text.Json.Serialization;

namespace VisionStudio.Hmi.Model;

/// <summary>
/// Persists and restores HMI projects in .vshmi JSON format with standard templates.
/// </summary>
public static class HmiSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static string Serialize(HmiProject project)
    {
        project.UpdatedAtUtc = DateTime.UtcNow;
        return JsonSerializer.Serialize(project, JsonOptions);
    }

    public static HmiProject Deserialize(string json)
    {
        var project = JsonSerializer.Deserialize<HmiProject>(json, JsonOptions);
        if (project == null)
            throw new InvalidOperationException("Failed to parse HMI project JSON.");
        return project;
    }

    /// <summary>
    /// Generates a turnkey industrial machine-vision HMI configuration with standard pages.
    /// </summary>
    public static HmiProject CreateDefaultProject()
    {
        var project = new HmiProject
        {
            Title = "Dây Chuyền Đóng Gói & Kiểm Tra Vision",
            MachineId = "LINE-01-STATION-A",
            Theme = HmiTheme.Dark,
            LogoText = "KHA STUDIO INDUSTRIAL HMI",
            RefreshRateMs = 100
        };

        // PAGE 1: Overview & Inspection Run
        var pageOverview = new HmiPage
        {
            Id = "page-overview",
            Title = "Vận Hành Kiểm Tra",
            Icon = "🖥️",
            Order = 0,
            MinimumRole = HmiRole.Operator
        };

        pageOverview.Widgets.Add(new HmiWidget
        {
            Id = "w-live-img",
            Type = HmiWidgetType.LiveImage,
            Title = "Camera Trực Tiếp (Live Stream & Overlays)",
            Column = 0,
            Row = 0,
            ColSpan = 6,
            RowSpan = 6,
            Binding = new HmiBinding { Source = HmiBindingSource.PipelineOutput, Path = "Station1.Image" }
        });

        pageOverview.Widgets.Add(new HmiWidget
        {
            Id = "w-verdict",
            Type = HmiWidgetType.OkNgBadge,
            Title = "Kết Quả Kiểm Tra (Pass/Fail)",
            Column = 6,
            Row = 0,
            ColSpan = 6,
            RowSpan = 2,
            Binding = new HmiBinding { Source = HmiBindingSource.PipelineOutput, Path = "Station1.Verdict" }
        });

        pageOverview.Widgets.Add(new HmiWidget
        {
            Id = "w-cycle-time",
            Type = HmiWidgetType.NumericDisplay,
            Title = "Chu Kỳ Chụp (Cycle Time)",
            Column = 6,
            Row = 2,
            ColSpan = 3,
            RowSpan = 2,
            Binding = new HmiBinding { Source = HmiBindingSource.PipelineOutput, Path = "Station1.CycleTimeMs", Format = "{0:F1} ms" }
        });

        pageOverview.Widgets.Add(new HmiWidget
        {
            Id = "w-gauge-dia",
            Type = HmiWidgetType.Gauge,
            Title = "Kích Thước Đường Kính",
            Column = 9,
            Row = 2,
            ColSpan = 3,
            RowSpan = 2,
            Binding = new HmiBinding
            {
                Source = HmiBindingSource.PipelineOutput,
                Path = "Measurements.Diameter",
                Format = "{0:F2} mm",
                LowLimit = 24.5,
                HighLimit = 25.5,
                TargetValue = 25.0
            },
            Properties = { ["Min"] = "20", ["Max"] = "30", ["Unit"] = "mm" }
        });

        pageOverview.Widgets.Add(new HmiWidget
        {
            Id = "w-recipe-sel",
            Type = HmiWidgetType.RecipeSelector,
            Title = "Chuyển Đổi Recipe / Mã Hàng",
            Column = 6,
            Row = 4,
            ColSpan = 3,
            RowSpan = 1,
            Binding = new HmiBinding { Source = HmiBindingSource.System, Path = "ActiveRecipe" }
        });

        pageOverview.Widgets.Add(new HmiWidget
        {
            Id = "w-btn-trigger",
            Type = HmiWidgetType.Button,
            Title = "Kích Hoạt Chụp (Trigger)",
            Column = 9,
            Row = 4,
            ColSpan = 3,
            RowSpan = 1,
            Properties = { ["Action"] = "TriggerStation", ["ButtonClass"] = "primary", ["Label"] = "KÍCH HOẠT CHỤP ⚡" }
        });

        pageOverview.Widgets.Add(new HmiWidget
        {
            Id = "w-trend",
            Type = HmiWidgetType.TrendChart,
            Title = "Biểu Đồ Xu Hướng Kích Thước (Trend Line)",
            Column = 6,
            Row = 5,
            ColSpan = 6,
            RowSpan = 3,
            Binding = new HmiBinding
            {
                Source = HmiBindingSource.PipelineOutput,
                Path = "Measurements.Diameter",
                LowLimit = 24.5,
                HighLimit = 25.5
            },
            Properties = { ["MaxPoints"] = "40" }
        });

        pageOverview.Widgets.Add(new HmiWidget
        {
            Id = "w-logs",
            Type = HmiWidgetType.LogViewer,
            Title = "Nhật Ký Hệ Thống Thời Gian Thực",
            Column = 0,
            Row = 6,
            ColSpan = 6,
            RowSpan = 2,
            Binding = new HmiBinding { Source = HmiBindingSource.System, Path = "Logs" }
        });

        // PAGE 2: Quality & SPC
        var pageSpc = new HmiPage
        {
            Id = "page-spc",
            Title = "Thống Kê & Chất Lượng SPC",
            Icon = "📈",
            Order = 1,
            MinimumRole = HmiRole.Operator
        };

        pageSpc.Widgets.Add(new HmiWidget
        {
            Id = "w-hist",
            Type = HmiWidgetType.Histogram,
            Title = "Phân Bố Sai Lệch Kích Thước (Histogram)",
            Column = 0,
            Row = 0,
            ColSpan = 6,
            RowSpan = 4,
            Binding = new HmiBinding { Source = HmiBindingSource.Metric, Path = "SPC.Histogram" }
        });

        pageSpc.Widgets.Add(new HmiWidget
        {
            Id = "w-table",
            Type = HmiWidgetType.Table,
            Title = "Bảng Nhật Ký 10 Sản Phẩm Gần Nhất",
            Column = 6,
            Row = 0,
            ColSpan = 6,
            RowSpan = 4,
            Binding = new HmiBinding { Source = HmiBindingSource.Metric, Path = "SPC.RecentRecords" }
        });

        pageSpc.Widgets.Add(new HmiWidget
        {
            Id = "w-gauge-yield",
            Type = HmiWidgetType.Gauge,
            Title = "Tỉ Lệ Đạt (Yield %)",
            Column = 0,
            Row = 4,
            ColSpan = 6,
            RowSpan = 4,
            Binding = new HmiBinding
            {
                Source = HmiBindingSource.Metric,
                Path = "SPC.YieldPercent",
                Format = "{0:F1} %",
                LowLimit = 95.0,
                HighLimit = 100.0,
                TargetValue = 99.0
            },
            Properties = { ["Min"] = "80", ["Max"] = "100", ["Unit"] = "%" }
        });

        pageSpc.Widgets.Add(new HmiWidget
        {
            Id = "w-num-total",
            Type = HmiWidgetType.NumericDisplay,
            Title = "Tổng Sản Lượng Đã Kiểm",
            Column = 6,
            Row = 4,
            ColSpan = 6,
            RowSpan = 4,
            Binding = new HmiBinding { Source = HmiBindingSource.Metric, Path = "SPC.TotalParts", Format = "{0:N0} cái" }
        });

        // PAGE 3: Engineering Setup
        var pageSettings = new HmiPage
        {
            Id = "page-settings",
            Title = "Cấu Hình Kỹ Thuật",
            Icon = "⚙️",
            Order = 2,
            MinimumRole = HmiRole.Engineer
        };

        pageSettings.Widgets.Add(new HmiWidget
        {
            Id = "w-login",
            Type = HmiWidgetType.UserLogin,
            Title = "Xác Thực Phân Quyền HMI",
            Column = 0,
            Row = 0,
            ColSpan = 4,
            RowSpan = 3
        });

        pageSettings.Widgets.Add(new HmiWidget
        {
            Id = "w-txt-speed",
            Type = HmiWidgetType.TextInput,
            Title = "Tốc Độ Băng Tải (m/phút)",
            Column = 4,
            Row = 0,
            ColSpan = 4,
            RowSpan = 3,
            Binding = new HmiBinding { Source = HmiBindingSource.DeviceTag, Path = "PLC.ConveyorSpeed" }
        });

        pageSettings.Widgets.Add(new HmiWidget
        {
            Id = "w-btn-reset",
            Type = HmiWidgetType.Button,
            Title = "Đặt Lại Bộ Đếm Sản Lượng",
            Column = 8,
            Row = 0,
            ColSpan = 4,
            RowSpan = 3,
            MinimumRole = HmiRole.Admin,
            Properties = { ["Action"] = "ResetCounters", ["ButtonClass"] = "danger", ["Label"] = "RESET COUNTER ⚠️" }
        });

        project.Pages.Add(pageOverview);
        project.Pages.Add(pageSpc);
        project.Pages.Add(pageSettings);
        project.DefaultPageId = pageOverview.Id;

        return project;
    }
}
