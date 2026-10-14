namespace VisionStudio.Hmi.Model;

/// <summary>
/// Data binding descriptor linking an HMI widget property to a live tag or pipeline measurement.
/// </summary>
public sealed class HmiBinding
{
    public HmiBindingSource Source { get; set; } = HmiBindingSource.PipelineOutput;

    /// <summary>
    /// Key path for data lookup (e.g., "Station1.Verdict", "Measurements.Diameter", "PLC.Speed").
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// String format pattern (e.g. "{0:F2} mm", "{0:N0}").
    /// </summary>
    public string Format { get; set; } = "{0}";

    /// <summary>
    /// Optional low limit for warning/alarm status on gauges & numbers.
    /// </summary>
    public double? LowLimit { get; set; }

    /// <summary>
    /// Optional high limit for warning/alarm status on gauges & numbers.
    /// </summary>
    public double? HighLimit { get; set; }

    /// <summary>
    /// Nominal target center value.
    /// </summary>
    public double? TargetValue { get; set; }

    /// <summary>
    /// Invert boolean logic if binding to boolean values.
    /// </summary>
    public bool InvertBoolean { get; set; }
}
