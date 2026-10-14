namespace VisionStudio.Hmi.Model;

/// <summary>
/// A screen page within an HMI project containing layout grids and widgets.
/// </summary>
public sealed class HmiPage
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "Main Screen";
    public string Icon { get; set; } = "📊";
    public int Order { get; set; } = 0;
    public HmiRole MinimumRole { get; set; } = HmiRole.Operator;

    /// <summary>
    /// Default 12-column grid layout system.
    /// </summary>
    public int GridColumns { get; set; } = 12;

    /// <summary>
    /// Default 8-row grid layout system.
    /// </summary>
    public int GridRows { get; set; } = 8;

    /// <summary>
    /// List of widgets positioned on this page.
    /// </summary>
    public List<HmiWidget> Widgets { get; set; } = new();
}
