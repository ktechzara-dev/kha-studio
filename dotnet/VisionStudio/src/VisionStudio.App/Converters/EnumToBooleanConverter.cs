using System.Globalization;
using System.Windows.Data;

namespace VisionStudio.App.Converters;

public sealed class EnumToBooleanConverter : IValueConverter
{
    public static EnumToBooleanConverter Instance { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null) return false;

        var valStr = value.ToString();
        var paramStr = parameter.ToString();

        return string.Equals(valStr, paramStr, StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b && b && parameter != null)
        {
            return parameter;
        }
        return Binding.DoNothing;
    }
}
