namespace VisionStudio.Hmi.Model;

/// <summary>
/// User role levels for operator HMI security and widget visibility.
/// </summary>
public enum HmiRole
{
    Operator = 0,
    Technician = 1,
    Engineer = 2,
    Admin = 3
}

/// <summary>
/// Visual presentation themes for HMI screens.
/// </summary>
public enum HmiTheme
{
    Dark = 0,
    IndustrialLight = 1,
    HighContrast = 2,
    NeonCyan = 3
}

/// <summary>
/// Widget component types available in the HMI designer.
/// </summary>
public enum HmiWidgetType
{
    LiveImage,
    OkNgBadge,
    NumericDisplay,
    Gauge,
    TrendChart,
    Histogram,
    Table,
    Button,
    TextInput,
    RecipeSelector,
    UserLogin,
    LogViewer
}

/// <summary>
/// Source origin for data binding.
/// </summary>
public enum HmiBindingSource
{
    PipelineOutput,
    DeviceTag,
    Metric,
    System
}
