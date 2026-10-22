using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Overlays;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Measurement;

/// <summary>
/// Executes a rake bundle of parallel sub-pixel calipers across a strip ROI, computing statistical widths (Mean, Min, Max, StdDev).
/// </summary>
[VisionTool("Measurement.MultiCaliper", "Multi-Caliper (Rake)", "Measurement", Description = "Rake caliper executing parallel scan lines for dimensional statistics")]
public sealed class MultiCaliperTool : VisionToolBase
{
    public const string ParamCaliperCount = "CaliperCount";
    public const string ParamSpacing = "Spacing";
    public const string ParamStartX = "StartX";
    public const string ParamStartY = "StartY";
    public const string ParamEndX = "EndX";
    public const string ParamEndY = "EndY";

    public const string PortMeanWidth = "MeanWidthPx";
    public const string PortMinWidth = "MinWidthPx";
    public const string PortMaxWidth = "MaxWidthPx";
    public const string PortStdDev = "StdDevPx";
    public const string PortAnnotatedImage = "AnnotatedImage";

    private readonly ToolParameter _pCount;
    private readonly ToolParameter _pSpacing;
    private readonly ToolParameter _pX1;
    private readonly ToolParameter _pY1;
    private readonly ToolParameter _pX2;
    private readonly ToolParameter _pY2;

    public MultiCaliperTool() : base("Measurement.MultiCaliper", "Multi-Caliper (Rake)", "Measurement", "Rake parallel caliper inspection")
    {
        _pCount = AddParameter(new ToolParameter(ParamCaliperCount, "Number of Calipers", ParameterType.Integer, 5, min: 2, max: 50));
        _pSpacing = AddParameter(new ToolParameter(ParamSpacing, "Rake Spacing (px)", ParameterType.Double, 10.0, min: 1.0, max: 200.0));
        _pX1 = AddParameter(new ToolParameter(ParamStartX, "Base Start X", ParameterType.Double, 400.0, min: 0.0, max: 10000.0, unit: "px"));
        _pY1 = AddParameter(new ToolParameter(ParamStartY, "Base Start Y", ParameterType.Double, 120.0, min: 0.0, max: 10000.0, unit: "px"));
        _pX2 = AddParameter(new ToolParameter(ParamEndX, "Base End X", ParameterType.Double, 400.0, min: 0.0, max: 10000.0, unit: "px"));
        _pY2 = AddParameter(new ToolParameter(ParamEndY, "Base End Y", ParameterType.Double, 480.0, min: 0.0, max: 10000.0, unit: "px"));

        AddInput(new ToolPort("ImageIn", "Image In", PortType.Image, PortDirection.Input));

        AddOutput(new ToolPort(PortMeanWidth, "Mean Width (px)", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortMinWidth, "Min Width (px)", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortMaxWidth, "Max Width (px)", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortStdDev, "Std Dev (px)", PortType.Number, PortDirection.Output));
        AddOutput(new ToolPort(PortAnnotatedImage, "Annotated Image", PortType.Image, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var inputFrame = ctx.GetImage("ImageIn");
        if (inputFrame == null) return ToolResult.Failed("Missing input image.");

        var count = _pCount.GetValue<int>();
        var spacing = _pSpacing.GetValue<double>();
        var x1 = _pX1.GetValue<double>();
        var y1 = _pY1.GetValue<double>();
        var x2 = _pX2.GetValue<double>();
        var y2 = _pY2.GetValue<double>();

        var dx = x2 - x1;
        var dy = y2 - y1;
        var len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1.0) return ToolResult.Failed("Invalid base caliper length.");

        // Normal vector perpendicular to scan direction
        var nx = -dy / len;
        var ny = dx / len;

        var singleTool = new CaliperTool();
        var widths = new List<double>();
        var overlays = new List<IOverlayPrimitive>();

        for (int i = 0; i < count; i++)
        {
            var offset = (i - count / 2.0) * spacing;
            var sx = x1 + nx * offset;
            var sy = y1 + ny * offset;
            var ex = x2 + nx * offset;
            var ey = y2 + ny * offset;

            singleTool.Parameters.First(p => p.Name == CaliperTool.ParamX1).Value = sx;
            singleTool.Parameters.First(p => p.Name == CaliperTool.ParamY1).Value = sy;
            singleTool.Parameters.First(p => p.Name == CaliperTool.ParamX2).Value = ex;
            singleTool.Parameters.First(p => p.Name == CaliperTool.ParamY2).Value = ey;

            using var res = singleTool.Execute(ctx, ct);
            if (res.Status == ToolStatus.Ok)
            {
                var w = res.GetOutput<double>(CaliperTool.PortWidthPx);
                if (w > 0) widths.Add(w);
                overlays.AddRange(res.Overlays);
            }
        }

        var mean = widths.Count > 0 ? widths.Average() : 0.0;
        var min = widths.Count > 0 ? widths.Min() : 0.0;
        var max = widths.Count > 0 ? widths.Max() : 0.0;
        var stdDev = widths.Count > 1
            ? Math.Sqrt(widths.Select(w => (w - mean) * (w - mean)).Average())
            : 0.0;

        var annotated = inputFrame.Mat.Clone();
        foreach (var ov in overlays) ov.Render(annotated);

        var outFrame = new ImageFrame(annotated, inputFrame.CameraId, inputFrame.FrameId, inputFrame.Format);
        var outputs = new Dictionary<string, object?>
        {
            [PortMeanWidth] = mean,
            [PortMinWidth] = min,
            [PortMaxWidth] = max,
            [PortStdDev] = stdDev,
            [PortAnnotatedImage] = outFrame
        };

        return ToolResult.Ok(outputs, overlays, message: $"Multi-Caliper: Mean={mean:F2}px, Min={min:F2}, Max={max:F2}, σ={stdDev:F2} (N={widths.Count})");
    }
}
