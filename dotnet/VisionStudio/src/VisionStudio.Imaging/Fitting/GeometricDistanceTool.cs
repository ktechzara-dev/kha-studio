using OpenCvSharp;
using VisionStudio.Core.Overlays;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Fitting;

/// <summary>
/// Measures analytic 2D geometric distances (Point-to-Point, Point-to-Line, Line-to-Line), angles, and intersection points.
/// </summary>
[VisionTool("Fitting.GeometricDistance", "Geometric Distance & Angle", "Geometry Fitting", Description = "Computes point-to-point, point-to-line, angle, and line intersection")]
public sealed class GeometricDistanceTool : VisionToolBase
{
    public const string ParamMode = "MeasurementMode";
    public const string ParamX1 = "X1";
    public const string ParamY1 = "Y1";
    public const string ParamX2 = "X2";
    public const string ParamY2 = "Y2";
    public const string ParamX3 = "X3";
    public const string ParamY3 = "Y3";
    public const string ParamX4 = "X4";
    public const string ParamY4 = "Y4";

    public const string PortDistance = "Distance";
    public const string PortAngle = "AngleDegrees";
    public const string PortIntersectX = "IntersectX";
    public const string PortIntersectY = "IntersectY";

    private readonly ToolParameter _pMode;
    private readonly ToolParameter _pX1;
    private readonly ToolParameter _pY1;
    private readonly ToolParameter _pX2;
    private readonly ToolParameter _pY2;
    private readonly ToolParameter _pX3;
    private readonly ToolParameter _pY3;
    private readonly ToolParameter _pX4;
    private readonly ToolParameter _pY4;

    public GeometricDistanceTool() : base("Fitting.GeometricDistance", "Geometric Distance & Angle", "Geometry Fitting", "Geometric metrology tool")
    {
        _pMode = AddParameter(new ToolParameter(
            ParamMode, "Mode", ParameterType.Enum, "PointToPoint",
            enumOptions: new[] { "PointToPoint", "PointToLine", "AngleBetweenLines", "LineIntersection" }));

        _pX1 = AddParameter(new ToolParameter(ParamX1, "P1 X", ParameterType.Double, 10.0));
        _pY1 = AddParameter(new ToolParameter(ParamY1, "P1 Y", ParameterType.Double, 10.0));
        _pX2 = AddParameter(new ToolParameter(ParamX2, "P2 X", ParameterType.Double, 110.0));
        _pY2 = AddParameter(new ToolParameter(ParamY2, "P2 Y", ParameterType.Double, 10.0));
        _pX3 = AddParameter(new ToolParameter(ParamX3, "P3 X", ParameterType.Double, 10.0));
        _pY3 = AddParameter(new ToolParameter(ParamY3, "P3 Y", ParameterType.Double, 110.0));
        _pX4 = AddParameter(new ToolParameter(ParamX4, "P4 X", ParameterType.Double, 110.0));
        _pY4 = AddParameter(new ToolParameter(ParamY4, "P4 Y", ParameterType.Double, 110.0));

        AddOutput(new ToolPort(PortDistance, "Distance (px)", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortAngle, "Angle (deg)", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortIntersectX, "Intersection X", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortIntersectY, "Intersection Y", PortType.Number, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var mode = _pMode.GetValue<string>() ?? "PointToPoint";

        var p1 = new Point2f((float)_pX1.GetValue<double>(), (float)_pY1.GetValue<double>());
        var p2 = new Point2f((float)_pX2.GetValue<double>(), (float)_pY2.GetValue<double>());
        var p3 = new Point2f((float)_pX3.GetValue<double>(), (float)_pY3.GetValue<double>());
        var p4 = new Point2f((float)_pX4.GetValue<double>(), (float)_pY4.GetValue<double>());

        double distance = 0.0;
        double angle = 0.0;
        double ix = 0.0, iy = 0.0;

        var overlays = new List<IOverlayPrimitive>();

        switch (mode.ToLowerInvariant())
        {
            case "pointtopoint":
                var dx = p2.X - p1.X;
                var dy = p2.Y - p1.Y;
                distance = Math.Sqrt(dx * dx + dy * dy);
                angle = Math.Atan2(dy, dx) * 180.0 / Math.PI;

                overlays.Add(new LineOverlay(p1, p2, new Scalar(0, 255, 255), Thickness: 2));
                overlays.Add(new CrossOverlay(p1, 8f, new Scalar(0, 0, 255), Thickness: 2));
                overlays.Add(new CrossOverlay(p2, 8f, new Scalar(0, 0, 255), Thickness: 2));
                overlays.Add(new TextOverlay($"D={distance:F2}px", new Point2f((p1.X + p2.X) / 2f, (p1.Y + p2.Y) / 2f - 6), new Scalar(0, 255, 255), FontScale: 0.5));
                break;

            case "pointtoline":
                // Distance from P1 to line defined by P3 -> P4
                var ldx = p4.X - p3.X;
                var ldy = p4.Y - p3.Y;
                var lineLen = Math.Sqrt(ldx * ldx + ldy * ldy);
                if (lineLen > 1e-6)
                {
                    distance = Math.Abs(ldx * (p3.Y - p1.Y) - (p3.X - p1.X) * ldy) / lineLen;
                }

                overlays.Add(new LineOverlay(p3, p4, new Scalar(255, 255, 0), Thickness: 2));
                overlays.Add(new CrossOverlay(p1, 8f, new Scalar(0, 0, 255), Thickness: 2));
                overlays.Add(new TextOverlay($"Dist={distance:F2}px", new Point2f(p1.X + 5, p1.Y - 5), new Scalar(0, 255, 255), FontScale: 0.5));
                break;

            case "anglebetweenlines":
                // Angle between line L1 (P1->P2) and line L2 (P3->P4)
                var a1 = Math.Atan2(p2.Y - p1.Y, p2.X - p1.X) * 180.0 / Math.PI;
                var a2 = Math.Atan2(p4.Y - p3.Y, p4.X - p3.X) * 180.0 / Math.PI;
                angle = Math.Abs(a1 - a2);
                if (angle > 180.0) angle = 360.0 - angle;

                overlays.Add(new LineOverlay(p1, p2, new Scalar(0, 255, 0), Thickness: 2));
                overlays.Add(new LineOverlay(p3, p4, new Scalar(0, 165, 255), Thickness: 2));
                overlays.Add(new TextOverlay($"Angle={angle:F1}°", new Point2f(p1.X, p1.Y - 8), new Scalar(255, 255, 255), FontScale: 0.6));
                break;

            case "lineintersection":
                // Intersection of L1 (P1->P2) and L2 (P3->P4)
                var d = (p1.X - p2.X) * (p3.Y - p4.Y) - (p1.Y - p2.Y) * (p3.X - p4.X);
                if (Math.Abs(d) > 1e-6)
                {
                    ix = ((p1.X * p2.Y - p1.Y * p2.X) * (p3.X - p4.X) - (p1.X - p2.X) * (p3.X * p4.Y - p3.Y * p4.X)) / d;
                    iy = ((p1.X * p2.Y - p1.Y * p2.X) * (p3.Y - p4.Y) - (p1.Y - p2.Y) * (p3.X * p4.Y - p3.Y * p4.X)) / d;

                    var interPt = new Point2f((float)ix, (float)iy);
                    overlays.Add(new LineOverlay(p1, p2, new Scalar(0, 255, 0), Thickness: 1));
                    overlays.Add(new LineOverlay(p3, p4, new Scalar(0, 165, 255), Thickness: 1));
                    overlays.Add(new CrossOverlay(interPt, 14f, new Scalar(0, 0, 255), Thickness: 2));
                    overlays.Add(new TextOverlay($"Intersect ({ix:F1}, {iy:F1})", new Point2f(interPt.X + 8, interPt.Y - 8), new Scalar(0, 0, 255), FontScale: 0.5));
                }
                break;
        }

        var outputs = new Dictionary<string, object?>
        {
            [PortDistance] = distance,
            [PortAngle] = angle,
            [PortIntersectX] = ix,
            [PortIntersectY] = iy
        };

        return ToolResult.Ok(outputs, overlays, message: $"Metrology {mode}: Dist={distance:F2}px, Angle={angle:F1}°");
    }
}
