namespace VisionStudio.Hmi.Engine;

public enum WidgetStatus
{
    Normal = 0,
    Ok = 1,
    Warning = 2,
    Alarm = 3,
    Error = 4,
    Disabled = 5
}

/// <summary>
/// Evaluated runtime snapshot of a single widget for display on WPF and Web HMI clients.
/// </summary>
public sealed class HmiWidgetSnapshot
{
    public string WidgetId { get; set; } = string.Empty;
    public string DisplayValue { get; set; } = string.Empty;
    public object? RawValue { get; set; }
    public WidgetStatus Status { get; set; } = WidgetStatus.Normal;
    public object? ExtraData { get; set; }
}

/// <summary>
/// Complete evaluation of all widgets on an active screen page.
/// </summary>
public sealed class HmiPageSnapshot
{
    public string PageId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public Dictionary<string, HmiWidgetSnapshot> Widgets { get; set; } = new();
}
