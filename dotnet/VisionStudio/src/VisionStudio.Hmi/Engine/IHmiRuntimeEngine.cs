using VisionStudio.Hmi.Model;

namespace VisionStudio.Hmi.Engine;

/// <summary>
/// Execution and data binding engine driving both WPF operator panels and Web HMI clients.
/// </summary>
public interface IHmiRuntimeEngine
{
    HmiProject CurrentProject { get; }
    void LoadProject(HmiProject project);

    HmiRole CurrentRole { get; set; }
    string CurrentUsername { get; set; }
    string CurrentPageId { get; set; }

    void SetTag(string key, object value);
    object? GetTag(string key);

    void SetInspectionResult(string stationId, bool pass, double cycleTimeMs,
        IReadOnlyDictionary<string, object>? measurements = null, byte[]? jpegImage = null);

    void LogMessage(string level, string message);

    HmiPageSnapshot GetPageSnapshot(string? pageId = null);
    HmiWidgetSnapshot EvaluateWidget(HmiWidget widget);

    Task ExecuteActionAsync(string widgetId, string actionName, string payload);

    event Action<HmiPageSnapshot>? PageUpdated;
    event Action<string, string>? ActionTriggered;
    event Action<string, string, string>? LogEmitted;
}
