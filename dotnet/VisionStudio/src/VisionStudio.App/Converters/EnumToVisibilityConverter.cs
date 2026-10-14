using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace VisionStudio.App.Converters;

public sealed class EnumToVisibilityConverter : IValueConverter
{
    public static EnumToVisibilityConverter Instance { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null) return Visibility.Collapsed;

        var valStr = value.ToString();
        var paramStr = parameter.ToString();

        return string.Equals(valStr, paramStr, StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
