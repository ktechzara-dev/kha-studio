namespace VisionStudio.Hmi.Model;

/// <summary>
/// Individual widget element placed on an HMI screen page.
/// </summary>
public sealed class HmiWidget
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public HmiWidgetType Type { get; set; } = HmiWidgetType.NumericDisplay;
    public string Title { get; set; } = "Widget";

    /// <summary>
    /// Grid column index (0-based).
    /// </summary>
    public int Column { get; set; } = 0;

    /// <summary>
    /// Grid row index (0-based).
    /// </summary>
    public int Row { get; set; } = 0;

    /// <summary>
    /// Column span across the grid.
    /// </summary>
    public int ColSpan { get; set; } = 3;

    /// <summary>
    /// Row span across the grid.
    /// </summary>
    public int RowSpan { get; set; } = 2;

    /// <summary>
    /// Minimum user role required to view and interact with this widget.
    /// </summary>
    public HmiRole MinimumRole { get; set; } = HmiRole.Operator;

    /// <summary>
    /// Primary data binding for value/state.
    /// </summary>
    public HmiBinding? Binding { get; set; }

    /// <summary>
    /// Component-specific properties (e.g. Action: "TriggerStation", MinGauge: 0, MaxGauge: 100, Unit: "mm").
    /// </summary>
    public Dictionary<string, string> Properties { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Helper to get a typed property with fallback default.
    /// </summary>
    public string GetProperty(string key, string defaultValue = "") =>
        Properties.TryGetValue(key, out var val) ? val : defaultValue;

    public void SetProperty(string key, string value) => Properties[key] = value;
}
