using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using VisionStudio.Core.Tools;

namespace VisionStudio.App.Converters;

/// <summary>
/// Converts ToolStatus enum values to dark-theme industrial colors.
/// </summary>
public sealed class StatusToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush OkBrush = new(System.Windows.Media.Color.FromRgb(16, 185, 129)); // Emerald #10b981
    private static readonly SolidColorBrush ErrorBrush = new(System.Windows.Media.Color.FromRgb(239, 68, 68)); // Rose #ef4444
    private static readonly SolidColorBrush WarningBrush = new(System.Windows.Media.Color.FromRgb(245, 158, 11)); // Amber #f59e0b
    private static readonly SolidColorBrush IdleBrush = new(System.Windows.Media.Color.FromRgb(71, 85, 105)); // Slate #475569
    private static readonly SolidColorBrush SkippedBrush = new(System.Windows.Media.Color.FromRgb(100, 116, 139)); // Slate-500

    static StatusToBrushConverter()
    {
        OkBrush.Freeze();
        ErrorBrush.Freeze();
        WarningBrush.Freeze();
        IdleBrush.Freeze();
        SkippedBrush.Freeze();
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ToolStatus status)
        {
            return status switch
            {
                ToolStatus.Ok => OkBrush,
                ToolStatus.Error => ErrorBrush,
                ToolStatus.Warning => WarningBrush,
                ToolStatus.Skipped => SkippedBrush,
                _ => IdleBrush
            };
        }

        return IdleBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
