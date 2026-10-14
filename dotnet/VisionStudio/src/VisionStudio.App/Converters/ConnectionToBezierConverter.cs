using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace VisionStudio.App.Converters;

/// <summary>
/// Converts start and end points of a connection into a smooth cubic Bézier curve geometry.
/// </summary>
public sealed class ConnectionToBezierConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not Point p1 || values[1] is not Point p2)
        {
            return Geometry.Empty;
        }

        var dx = Math.Max(40.0, Math.Abs(p2.X - p1.X) * 0.5);
        var cp1 = new Point(p1.X + dx, p1.Y);
        var cp2 = new Point(p2.X - dx, p2.Y);

        var geometry = new PathGeometry();
        var figure = new PathFigure
        {
            StartPoint = p1,
            IsFilled = false,
            IsClosed = false
        };

        figure.Segments.Add(new BezierSegment(cp1, cp2, p2, isStroked: true));
        geometry.Figures.Add(figure);
        geometry.Freeze();

        return geometry;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
