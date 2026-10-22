using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Overlays;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Measurement;

/// <summary>
/// Transition direction for caliper edge finding.
/// </summary>
public enum CaliperTransition
{
    Any,
    DarkToLight,
    LightToDark
}

/// <summary>
/// Sub-pixel 1D edge caliper detecting edge pairs and width along an arbitrary scan line with under 0.1 px accuracy.
/// </summary>
[VisionTool("Measurement.Caliper", "1D Caliper (Edge Pair)", "Measurement", Description = "Measures width between edge pairs along a scan line with sub-pixel resolution")]
public sealed class CaliperTool : VisionToolBase
{
    public const string ParamX1 = "StartX";
    public const string ParamY1 = "StartY";
    public const string ParamX2 = "EndX";
    public const string ParamY2 = "EndY";
    public const string ParamThreshold = "EdgeThreshold";
    public const string ParamTransition = "Transition";
    public const string ParamScaleMm = "ScaleMmPerPixel";

    public const string PortInImage = "ImageIn";
    public const string PortWidthPx = "WidthPixels";
    public const string PortWidthMm = "WidthMm";
    public const string PortEdge1X = "Edge1X";
    public const string PortEdge1Y = "Edge1Y";
    public const string PortEdge2X = "Edge2X";
    public const string PortEdge2Y = "Edge2Y";
    public const string PortAnnotatedImage = "AnnotatedImage";

    private readonly ToolParameter _pX1;
    private readonly ToolParameter _pY1;
    private readonly ToolParameter _pX2;
    private readonly ToolParameter _pY2;
    private readonly ToolParameter _pThresh;
    private readonly ToolParameter _pTrans;
    private readonly ToolParameter _pScale;

    public CaliperTool() : base("Measurement.Caliper", "1D Caliper (Edge Pair)", "Measurement", "High precision sub-pixel caliper")
    {
        _pX1 = AddParameter(new ToolParameter(ParamX1, "Start X", ParameterType.Double, 400.0, min: 0.0, max: 10000.0, unit: "px"));
        _pY1 = AddParameter(new ToolParameter(ParamY1, "Start Y", ParameterType.Double, 120.0, min: 0.0, max: 10000.0, unit: "px"));
        _pX2 = AddParameter(new ToolParameter(ParamX2, "End X", ParameterType.Double, 400.0, min: 0.0, max: 10000.0, unit: "px"));
        _pY2 = AddParameter(new ToolParameter(ParamY2, "End Y", ParameterType.Double, 480.0, min: 0.0, max: 10000.0, unit: "px"));
        _pThresh = AddParameter(new ToolParameter(ParamThreshold, "Min Derivative Threshold", ParameterType.Double, 20.0, min: 1.0, max: 255.0));
        _pTrans = AddParameter(new ToolParameter(
            ParamTransition, "Transition Mode", ParameterType.Enum, "Any",
            enumOptions: new[] { "Any", "DarkToLight", "LightToDark" }));
        _pScale = AddParameter(new ToolParameter(ParamScaleMm, "Scale (mm/px)", ParameterType.Double, 0.1, min: 0.0001, max: 100.0, unit: "mm/px"));

        AddInput(new ToolPort(PortInImage, "Image In", PortType.Image, PortDirection.Input));

        AddOutput(new ToolPort(PortWidthPx, "Width (px)", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortWidthMm, "Width (mm)", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortEdge1X, "Edge 1 X", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortEdge1Y, "Edge 1 Y", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortEdge2X, "Edge 2 X", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortEdge2Y, "Edge 2 Y", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortAnnotatedImage, "Annotated Image", PortType.Image, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var inputFrame = ctx.GetImage(PortInImage);
        if (inputFrame == null) return ToolResult.Failed("Missing input image.");

        var src = inputFrame.Mat;
        using var gray = new Mat();
        if (src.Channels() > 1)
        {
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
        }
        else
        {
            src.CopyTo(gray);
        }

        var p1 = new Point2d(_pX1.GetValue<double>(), _pY1.GetValue<double>());
        var p2 = new Point2d(_pX2.GetValue<double>(), _pY2.GetValue<double>());
        var thresh = _pThresh.GetValue<double>();
        var transMode = _pTrans.GetValue<string>() ?? "Any";
        var scaleMm = _pScale.GetValue<double>();

        var dx = p2.X - p1.X;
        var dy = p2.Y - p1.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length < 3.0)
        {
            return ToolResult.Failed("Caliper scan line is too short (< 3 pixels).");
        }

        var ux = dx / length;
        var uy = dy / length;

        // 1. Sample 1D intensity profile with sub-pixel bilinear interpolation
        var numSamples = (int)Math.Ceiling(length);
        var profile = new double[numSamples + 1];

        for (int i = 0; i <= numSamples; i++)
        {
            var sx = p1.X + i * ux;
            var sy = p1.Y + i * uy;
            profile[i] = SampleBilinear(gray, sx, sy);
        }

        // 2. Compute 1D central differences (derivative)
        var deriv = new double[numSamples + 1];
        for (int i = 1; i < numSamples; i++)
        {
            deriv[i] = (profile[i + 1] - profile[i - 1]) / 2.0;
        }

        // 3. Find sub-pixel peaks
        var detectedEdges = new List<(double Position, double Strength, bool IsDarkToLight)>();

        for (int i = 2; i < numSamples - 1; i++)
        {
            var val = deriv[i];
            var absVal = Math.Abs(val);
            if (absVal < thresh) continue;

            var prev = Math.Abs(deriv[i - 1]);
            var next = Math.Abs(deriv[i + 1]);

            if (absVal >= prev && absVal >= next && (prev != next || absVal > prev))
            {
                var isDarkToLight = val > 0;
                if (transMode.Equals("DarkToLight", StringComparison.OrdinalIgnoreCase) && !isDarkToLight) continue;
                if (transMode.Equals("LightToDark", StringComparison.OrdinalIgnoreCase) && isDarkToLight) continue;

                // Parabolic peak interpolation: offset = (y_prev - y_next) / (2 * (y_prev - 2*y_mid + y_next))
                var denom = 2.0 * (prev - 2.0 * absVal + next);
                var subOffset = Math.Abs(denom) > 1e-6 ? (prev - next) / denom : 0.0;
                subOffset = Math.Clamp(subOffset, -0.5, 0.5);

                var exactPos = i + subOffset;
                detectedEdges.Add((exactPos, absVal, isDarkToLight));
            }
        }

        var annotated = new Mat();
        if (src.Channels() == 1)
        {
            Cv2.CvtColor(src, annotated, ColorConversionCodes.GRAY2BGR);
        }
        else
        {
            src.CopyTo(annotated);
        }

        var overlays = new List<IOverlayPrimitive>();
        
        // Scan axis guideline in bright cyan
        var lineOv = new LineOverlay(new Point2f((float)p1.X, (float)p1.Y), new Point2f((float)p2.X, (float)p2.Y), new Scalar(255, 180, 0), Thickness: 2);
        overlays.Add(lineOv);
        lineOv.Render(annotated);

        double widthPx = 0.0;
        double widthMm = 0.0;
        double e1x = 0, e1y = 0, e2x = 0, e2y = 0;

        if (detectedEdges.Count >= 2)
        {
            // Pick first and last edge along scan line
            var edge1 = detectedEdges.First();
            var edge2 = detectedEdges.Last();

            widthPx = Math.Abs(edge2.Position - edge1.Position);
            widthMm = widthPx * scaleMm;

            e1x = p1.X + edge1.Position * ux;
            e1y = p1.Y + edge1.Position * uy;
            e2x = p1.X + edge2.Position * ux;
            e2y = p1.Y + edge2.Position * uy;

            var pt1 = new Point2f((float)e1x, (float)e1y);
            var pt2 = new Point2f((float)e2x, (float)e2y);

            // Neon green measuring span
            var spanLine = new LineOverlay(pt1, pt2, new Scalar(0, 255, 0), Thickness: 3);
            var c1 = new CrossOverlay(pt1, 12f, new Scalar(0, 255, 0), Thickness: 2);
            var c2 = new CrossOverlay(pt2, 12f, new Scalar(0, 255, 0), Thickness: 2);
            var textOv = new TextOverlay($"📏 Caliper: {widthPx:F2} px ({widthMm:F3} mm)", 
                new Point2f((float)((e1x + e2x) / 2.0), (float)((e1y + e2y) / 2.0 - 12)), 
                new Scalar(0, 255, 255), FontScale: 0.6);

            overlays.Add(spanLine);
            overlays.Add(c1);
            overlays.Add(c2);
            overlays.Add(textOv);

            spanLine.Render(annotated);
            c1.Render(annotated);
            c2.Render(annotated);
            textOv.Render(annotated);
        }
        else
        {
            var warnOv = new TextOverlay($"⚠️ Caliper: {detectedEdges.Count} biên (Ngưỡng={thresh:F0})",
                new Point2f((float)((p1.X + p2.X) / 2.0), (float)((p1.Y + p2.Y) / 2.0 - 10)),
                new Scalar(0, 165, 255), FontScale: 0.55);
            overlays.Add(warnOv);
            warnOv.Render(annotated);
        }

        var outFrame = new ImageFrame(annotated, inputFrame.CameraId, inputFrame.FrameId, PixelFormat.Bgr24);
        var outputs = new Dictionary<string, object?>
        {
            [PortWidthPx] = widthPx,
            [PortWidthMm] = widthMm,
            [PortEdge1X] = e1x,
            [PortEdge1Y] = e1y,
            [PortEdge2X] = e2x,
            [PortEdge2Y] = e2y,
            [PortAnnotatedImage] = outFrame
        };

        return ToolResult.Ok(outputs, overlays, message: $"Caliper: {widthPx:F3} px ({widthMm:F3} mm) - {detectedEdges.Count} edges found");
    }

    private static double SampleBilinear(Mat gray, double x, double y)
    {
        var x0 = (int)Math.Floor(x);
        var y0 = (int)Math.Floor(y);
        var x1 = x0 + 1;
        var y1 = y0 + 1;

        if (x0 < 0 || y0 < 0 || x1 >= gray.Cols || y1 >= gray.Rows)
        {
            x0 = Math.Clamp(x0, 0, gray.Cols - 1);
            y0 = Math.Clamp(y0, 0, gray.Rows - 1);
            return gray.Get<byte>(y0, x0);
        }

        var fx = x - x0;
        var fy = y - y0;

        var v00 = gray.Get<byte>(y0, x0);
        var v10 = gray.Get<byte>(y0, x1);
        var v01 = gray.Get<byte>(y1, x0);
        var v11 = gray.Get<byte>(y1, x1);

        var top = v00 * (1.0 - fx) + v10 * fx;
        var bottom = v01 * (1.0 - fx) + v11 * fx;
        return top * (1.0 - fy) + bottom * fy;
    }
}
