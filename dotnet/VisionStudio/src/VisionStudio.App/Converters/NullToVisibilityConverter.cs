using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace VisionStudio.App.Converters;

/// <summary>
/// Converts null / not-null object reference to Visibility.
/// Parameter "Inverse": null -> Visible, not-null -> Collapsed.
/// Default: null -> Collapsed, not-null -> Visible.
/// </summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public static readonly NullToVisibilityConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isNull = value is null;
        if (parameter is string paramStr && paramStr.Equals("Inverse", StringComparison.OrdinalIgnoreCase))
        {
            return isNull ? Visibility.Visible : Visibility.Collapsed;
        }

        return isNull ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
