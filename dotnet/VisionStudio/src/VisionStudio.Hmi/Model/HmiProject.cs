namespace VisionStudio.Hmi.Model;

/// <summary>
/// Top-level project model defining the entire HMI application configuration (.vshmi).
/// </summary>
public sealed class HmiProject
{
    public string Version { get; set; } = "1.0.0";
    public string Title { get; set; } = "VisionStudio Industrial HMI";
    public string MachineId { get; set; } = "LINE-01-STATION-A";
    public HmiTheme Theme { get; set; } = HmiTheme.Dark;
    public string LogoText { get; set; } = "KHA STUDIO VISION";
    public int RefreshRateMs { get; set; } = 100;
    public string DefaultPageId { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<HmiPage> Pages { get; set; } = new();

    public HmiPage? GetDefaultPage() =>
        Pages.FirstOrDefault(p => p.Id == DefaultPageId) ?? Pages.FirstOrDefault();
}
