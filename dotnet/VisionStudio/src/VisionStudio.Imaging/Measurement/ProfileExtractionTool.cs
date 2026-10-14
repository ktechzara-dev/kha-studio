using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Overlays;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Measurement;

/// <summary>
/// Extracts a 1D pixel intensity cross-section profile along an arbitrary line segment.
/// </summary>
[VisionTool("Measurement.Profile", "Profile Extraction", "Measurement", Description = "Extracts 1D pixel intensity profile along a line")]
public sealed class ProfileExtractionTool : VisionToolBase
{
    public const string ParamX1 = "StartX";
    public const string ParamY1 = "StartY";
    public const string ParamX2 = "EndX";
    public const string ParamY2 = "EndY";

    public const string PortMean = "MeanIntensity";
    public const string PortMin = "MinIntensity";
    public const string PortMax = "MaxIntensity";
    public const string PortAnnotatedImage = "AnnotatedImage";

    private readonly ToolParameter _pX1;
    private readonly ToolParameter _pY1;
    private readonly ToolParameter _pX2;
    private readonly ToolParameter _pY2;

    public ProfileExtractionTool() : base("Measurement.Profile", "Profile Extraction", "Measurement", "1D intensity profile extractor")
    {
        _pX1 = AddParameter(new ToolParameter(ParamX1, "Start X", ParameterType.Double, 20.0));
        _pY1 = AddParameter(new ToolParameter(ParamY1, "Start Y", ParameterType.Double, 50.0));
        _pX2 = AddParameter(new ToolParameter(ParamX2, "End X", ParameterType.Double, 180.0));
        _pY2 = AddParameter(new ToolParameter(ParamY2, "End Y", ParameterType.Double, 50.0));

        AddInput(new ToolPort("ImageIn", "Image In", PortType.Image, PortDirection.Input));

        AddOutput(new ToolPort(PortMean, "Mean Intensity", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortMin, "Min Intensity", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortMax, "Max Intensity", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortAnnotatedImage, "Annotated Image", PortType.Image, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var inputFrame = ctx.GetImage("ImageIn");
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

        var p1 = new Point2f((float)_pX1.GetValue<double>(), (float)_pY1.GetValue<double>());
        var p2 = new Point2f((float)_pX2.GetValue<double>(), (float)_pY2.GetValue<double>());

        var dx = p2.X - p1.X;
        var dy = p2.Y - p1.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        var steps = (int)Math.Ceiling(length);

        if (steps < 1) return ToolResult.Failed("Line segment length too short.");

        var samples = new List<double>(steps + 1);
        for (int i = 0; i <= steps; i++)
        {
            var t = (double)i / steps;
            var x = Math.Clamp((int)Math.Round(p1.X + t * dx), 0, gray.Cols - 1);
            var y = Math.Clamp((int)Math.Round(p1.Y + t * dy), 0, gray.Rows - 1);
            samples.Add(gray.Get<byte>(y, x));
        }

        var mean = samples.Average();
        var min = samples.Min();
        var max = samples.Max();

        var annotated = src.Clone();
        var overlays = new List<IOverlayPrimitive>
        {
            new LineOverlay(p1, p2, new Scalar(0, 255, 255), Thickness: 2),
            new CrossOverlay(p1, 6f, new Scalar(0, 0, 255), Thickness: 2),
            new CrossOverlay(p2, 6f, new Scalar(0, 0, 255), Thickness: 2)
        };
        foreach (var ov in overlays) ov.Render(annotated);

        var outFrame = new ImageFrame(annotated, inputFrame.CameraId, inputFrame.FrameId, inputFrame.Format);
        var outputs = new Dictionary<string, object?>
        {
            [PortMean] = mean,
            [PortMin] = min,
            [PortMax] = max,
            [PortAnnotatedImage] = outFrame
        };

        return ToolResult.Ok(outputs, overlays, message: $"Extracted {samples.Count} profile samples (Mean={mean:F1})");
    }
}
