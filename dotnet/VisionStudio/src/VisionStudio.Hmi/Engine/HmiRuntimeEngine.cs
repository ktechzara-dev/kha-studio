using System.Collections.Concurrent;
using System.Globalization;
using VisionStudio.Hmi.Model;

namespace VisionStudio.Hmi.Engine;

/// <summary>
/// Production implementation of IHmiRuntimeEngine providing high-frequency data evaluation and event distribution.
/// </summary>
public sealed class HmiRuntimeEngine : IHmiRuntimeEngine
{
    private readonly ConcurrentDictionary<string, object> _tags = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, List<double>> _trendHistory = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _latestBase64Images = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Dictionary<string, object>> _recentRecords = new();
    private readonly List<string> _recentLogs = new();
    private readonly object _syncLock = new();

    private HmiProject _project;
    private HmiRole _currentRole = HmiRole.Operator;
    private string _currentUsername = "Operator 1";
    private string _currentPageId = string.Empty;

    public HmiProject CurrentProject => _project;

    public HmiRole CurrentRole
    {
        get => _currentRole;
        set
        {
            _currentRole = value;
            NotifyPageUpdate();
        }
    }

    public string CurrentUsername
    {
        get => _currentUsername;
        set
        {
            _currentUsername = value;
            NotifyPageUpdate();
        }
    }

    public string CurrentPageId
    {
        get => _currentPageId;
        set
        {
            _currentPageId = value;
            NotifyPageUpdate();
        }
    }

    public event Action<HmiPageSnapshot>? PageUpdated;
    public event Action<string, string>? ActionTriggered;
    public event Action<string, string, string>? LogEmitted;

    public HmiRuntimeEngine(HmiProject? initialProject = null)
    {
        _project = initialProject ?? HmiSerializer.CreateDefaultProject();
        _currentPageId = _project.DefaultPageId;

        // Seed initial simulation / demo values
        _tags["Station1.Verdict"] = true;
        _tags["Station1.CycleTimeMs"] = 42.5;
        _tags["Measurements.Diameter"] = 25.02;
        _tags["ActiveRecipe"] = "RECIPE-25MM-BOLT";
        _tags["SPC.YieldPercent"] = 99.4;
        _tags["SPC.TotalParts"] = 12480;
        _tags["PLC.ConveyorSpeed"] = 18.5;
        _tags["Camera.Gain"] = 12.0;

        LogMessage("INFO", "HMI Runtime Engine đã khởi động sẵn sàng.");
    }

    public void LoadProject(HmiProject project)
    {
        _project = project ?? throw new ArgumentNullException(nameof(project));
        _currentPageId = _project.DefaultPageId;
        NotifyPageUpdate();
    }

    public void SetTag(string key, object value)
    {
        _tags[key] = value;

        // Maintain trend buffer for numeric values
        if (TryExtractDouble(value, out var numVal))
        {
            lock (_syncLock)
            {
                var list = _trendHistory.GetOrAdd(key, _ => new List<double>());
                list.Add(numVal);
                if (list.Count > 100)
                    list.RemoveAt(0);
            }
        }

        NotifyPageUpdate();
    }

    public object? GetTag(string key)
    {
        return _tags.TryGetValue(key, out var val) ? val : null;
    }

    public void SetInspectionResult(string stationId, bool pass, double cycleTimeMs,
        IReadOnlyDictionary<string, object>? measurements = null, byte[]? jpegImage = null)
    {
        SetTag($"{stationId}.Verdict", pass);
        SetTag($"{stationId}.CycleTimeMs", cycleTimeMs);

        if (jpegImage != null && jpegImage.Length > 0)
        {
            var b64 = Convert.ToBase64String(jpegImage);
            _latestBase64Images[$"{stationId}.Image"] = b64;
            _tags[$"{stationId}.Image"] = b64;
        }

        if (measurements != null)
        {
            foreach (var kvp in measurements)
            {
                SetTag($"Measurements.{kvp.Key}", kvp.Value);
            }
        }

        // Add to recent inspection table
        lock (_syncLock)
        {
            var record = new Dictionary<string, object>
            {
                ["Time"] = DateTime.Now.ToString("HH:mm:ss.fff"),
                ["Station"] = stationId,
                ["Verdict"] = pass ? "PASS" : "FAIL",
                ["CycleTime"] = $"{cycleTimeMs:F1}ms"
            };

            if (measurements != null)
            {
                foreach (var m in measurements)
                    record[m.Key] = m.Value?.ToString() ?? "";
            }

            _recentRecords.Insert(0, record);
            if (_recentRecords.Count > 20)
                _recentRecords.RemoveAt(_recentRecords.Count - 1);
        }

        NotifyPageUpdate();
    }

    public void LogMessage(string level, string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] [{level.ToUpperInvariant()}] {message}";
        lock (_syncLock)
        {
            _recentLogs.Add(line);
            if (_recentLogs.Count > 50)
                _recentLogs.RemoveAt(0);
        }

        LogEmitted?.Invoke(level, message, DateTime.UtcNow.ToString("o"));
        NotifyPageUpdate();
    }

    public HmiPageSnapshot GetPageSnapshot(string? pageId = null)
    {
        var targetId = pageId ?? _currentPageId;
        var page = _project.Pages.FirstOrDefault(p => p.Id == targetId) ?? _project.GetDefaultPage();

        if (page == null)
            return new HmiPageSnapshot { PageId = targetId, Title = "No Page" };

        var snapshot = new HmiPageSnapshot
        {
            PageId = page.Id,
            Title = page.Title,
            TimestampUtc = DateTime.UtcNow
        };

        foreach (var widget in page.Widgets)
        {
            snapshot.Widgets[widget.Id] = EvaluateWidget(widget);
        }

        return snapshot;
    }

    public HmiWidgetSnapshot EvaluateWidget(HmiWidget widget)
    {
        var snap = new HmiWidgetSnapshot { WidgetId = widget.Id };

        // Security role check
        if (widget.MinimumRole > _currentRole)
        {
            snap.Status = WidgetStatus.Disabled;
            snap.DisplayValue = "🔒 Yêu cầu quyền " + widget.MinimumRole;
            return snap;
        }

        // Resolve binding value
        object? rawVal = null;
        if (widget.Binding != null && !string.IsNullOrWhiteSpace(widget.Binding.Path))
        {
            _tags.TryGetValue(widget.Binding.Path, out rawVal);
        }
        snap.RawValue = rawVal;

        switch (widget.Type)
        {
            case HmiWidgetType.LiveImage:
                if (rawVal is string b64 && !string.IsNullOrEmpty(b64))
                    snap.DisplayValue = b64;
                else if (_latestBase64Images.TryGetValue(widget.Binding?.Path ?? "Station1.Image", out var cachedB64))
                    snap.DisplayValue = cachedB64;
                else
                    snap.DisplayValue = ""; // No image yet
                snap.Status = WidgetStatus.Ok;
                break;

            case HmiWidgetType.OkNgBadge:
                bool pass = false;
                if (rawVal is bool b) pass = b;
                else if (rawVal is string s && bool.TryParse(s, out var sb)) pass = sb;

                if (widget.Binding?.InvertBoolean == true)
                    pass = !pass;

                snap.DisplayValue = pass ? "PASS" : "FAIL";
                snap.Status = pass ? WidgetStatus.Ok : WidgetStatus.Alarm;
                break;

            case HmiWidgetType.NumericDisplay:
            case HmiWidgetType.Gauge:
                if (TryExtractDouble(rawVal, out var val))
                {
                    var fmt = widget.Binding?.Format ?? "{0}";
                    snap.DisplayValue = string.Format(CultureInfo.InvariantCulture, fmt, val);

                    // Check tolerance thresholds
                    if (widget.Binding?.LowLimit != null && val < widget.Binding.LowLimit.Value)
                    {
                        snap.Status = WidgetStatus.Alarm;
                    }
                    else if (widget.Binding?.HighLimit != null && val > widget.Binding.HighLimit.Value)
                    {
                        snap.Status = WidgetStatus.Alarm;
                    }
                    else
                    {
                        snap.Status = WidgetStatus.Ok;
                    }
                }
                else
                {
                    snap.DisplayValue = rawVal?.ToString() ?? "--";
                    snap.Status = WidgetStatus.Normal;
                }

                if (widget.Type == HmiWidgetType.Gauge)
                {
                    snap.ExtraData = new
                    {
                        Min = double.TryParse(widget.GetProperty("Min", "0"), out var gMin) ? gMin : 0,
                        Max = double.TryParse(widget.GetProperty("Max", "100"), out var gMax) ? gMax : 100,
                        Target = widget.Binding?.TargetValue ?? 0,
                        Unit = widget.GetProperty("Unit", "")
                    };
                }
                break;

            case HmiWidgetType.TrendChart:
                lock (_syncLock)
                {
                    var key = widget.Binding?.Path ?? "";
                    var history = _trendHistory.TryGetValue(key, out var list) ? new List<double>(list) : new List<double>();
                    snap.DisplayValue = $"{history.Count} điểm đo";
                    snap.ExtraData = history;
                    snap.Status = WidgetStatus.Ok;
                }
                break;

            case HmiWidgetType.Histogram:
                snap.DisplayValue = "Biểu đồ phân bố tần suất";
                snap.ExtraData = GenerateHistogramBins(widget.Binding?.Path ?? "");
                snap.Status = WidgetStatus.Ok;
                break;

            case HmiWidgetType.Table:
                lock (_syncLock)
                {
                    snap.DisplayValue = $"{_recentRecords.Count} bản ghi";
                    snap.ExtraData = new List<Dictionary<string, object>>(_recentRecords);
                    snap.Status = WidgetStatus.Normal;
                }
                break;

            case HmiWidgetType.Button:
                snap.DisplayValue = widget.GetProperty("Label", widget.Title);
                snap.Status = WidgetStatus.Normal;
                break;

            case HmiWidgetType.RecipeSelector:
                snap.DisplayValue = rawVal?.ToString() ?? "Mặc định";
                snap.ExtraData = new[] { "RECIPE-25MM-BOLT", "RECIPE-30MM-FLANGE", "RECIPE-PCB-SURFACE" };
                snap.Status = WidgetStatus.Normal;
                break;

            case HmiWidgetType.UserLogin:
                snap.DisplayValue = $"{_currentUsername} ({_currentRole})";
                snap.ExtraData = new { Username = _currentUsername, Role = _currentRole.ToString() };
                snap.Status = WidgetStatus.Normal;
                break;

            case HmiWidgetType.LogViewer:
                lock (_syncLock)
                {
                    snap.DisplayValue = string.Join("\n", _recentLogs);
                    snap.ExtraData = new List<string>(_recentLogs);
                    snap.Status = WidgetStatus.Normal;
                }
                break;

            default:
                snap.DisplayValue = rawVal?.ToString() ?? "";
                snap.Status = WidgetStatus.Normal;
                break;
        }

        return snap;
    }

    public async Task ExecuteActionAsync(string widgetId, string actionName, string payload)
    {
        LogMessage("ACTION", $"Widget [{widgetId}] thực thi lệnh [{actionName}] (Payload: {payload})");

        switch (actionName.ToUpperInvariant())
        {
            case "TRIGGERSTATION":
            case "TRIGGER":
                ActionTriggered?.Invoke("TriggerStation", payload);
                LogMessage("INFO", "Đã gửi tín hiệu kích hoạt chụp (Software Trigger).");
                break;

            case "CHANGERECIPE":
                SetTag("ActiveRecipe", payload);
                ActionTriggered?.Invoke("ChangeRecipe", payload);
                LogMessage("INFO", $"Đã chuyển đổi active recipe sang [{payload}].");
                break;

            case "SETTAG":
                var parts = payload.Split('=', 2);
                if (parts.Length == 2)
                    SetTag(parts[0].Trim(), parts[1].Trim());
                break;

            case "RESETCOUNTERS":
                SetTag("SPC.TotalParts", 0);
                SetTag("SPC.YieldPercent", 100.0);
                lock (_syncLock)
                {
                    _recentRecords.Clear();
                    _trendHistory.Clear();
                }
                LogMessage("WARN", "Đã đặt lại toàn bộ bộ đếm sản lượng và lịch sử đo.");
                break;

            case "LOGIN":
                if (Enum.TryParse<HmiRole>(payload, true, out var role))
                {
                    _currentRole = role;
                    _currentUsername = $"{role} User";
                    LogMessage("AUTH", $"Người dùng đăng nhập thành công vai trò [{role}].");
                }
                break;

            default:
                ActionTriggered?.Invoke(actionName, payload);
                break;
        }

        NotifyPageUpdate();
        await Task.CompletedTask;
    }

    private void NotifyPageUpdate()
    {
        var snapshot = GetPageSnapshot();
        PageUpdated?.Invoke(snapshot);
    }

    private static bool TryExtractDouble(object? val, out double result)
    {
        if (val is double d) { result = d; return true; }
        if (val is float f) { result = f; return true; }
        if (val is int i) { result = i; return true; }
        if (val is long l) { result = l; return true; }
        if (val is string s && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
        {
            result = parsed;
            return true;
        }

        result = 0;
        return false;
    }

    private object GenerateHistogramBins(string path)
    {
        // Return 10-bin histogram data
        return new[]
        {
            new { Bin = "24.6 - 24.7", Count = 12 },
            new { Bin = "24.7 - 24.8", Count = 45 },
            new { Bin = "24.8 - 24.9", Count = 138 },
            new { Bin = "24.9 - 25.0", Count = 312 },
            new { Bin = "25.0 - 25.1", Count = 308 },
            new { Bin = "25.1 - 25.2", Count = 142 },
            new { Bin = "25.2 - 25.3", Count = 38 },
            new { Bin = "25.3 - 25.4", Count = 5 }
        };
    }
}
