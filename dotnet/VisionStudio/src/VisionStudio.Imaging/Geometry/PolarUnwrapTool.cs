using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Geometry;

/// <summary>
/// Unwraps circular/annular objects (bottle caps, bearings, circular scales) into rectangular strips using polar transformation.
/// </summary>
[VisionTool("Geometry.PolarUnwrap", "Polar Unwrap", "Geometry / ROI", Description = "Unwraps circular objects to horizontal planar strips")]
public sealed class PolarUnwrapTool : VisionToolBase
{
    public const string ParamMaxRadius = "MaxRadius";
    public const string ParamWidth = "OutputWidth";
    public const string ParamHeight = "OutputHeight";

    public const string PortInImage = "ImageIn";
    public const string PortOutImage = "ImageOut";

    private readonly ToolParameter _pMaxR;
    private readonly ToolParameter _pOutW;
    private readonly ToolParameter _pOutH;

    public PolarUnwrapTool() : base("Geometry.PolarUnwrap", "Polar Unwrap", "Geometry / ROI", "Unwraps circular features")
    {
        _pMaxR = AddParameter(new ToolParameter(ParamMaxRadius, "Max Radius", ParameterType.Double, 120.0, min: 1.0));
        _pOutW = AddParameter(new ToolParameter(ParamWidth, "Output Width (Angles)", ParameterType.Integer, 360, min: 36));
        _pOutH = AddParameter(new ToolParameter(ParamHeight, "Output Height (Radius)", ParameterType.Integer, 120, min: 10));

        AddInput(new ToolPort(PortInImage, "Image In", PortType.Image, PortDirection.Input));
        AddOutput(new ToolPort(PortOutImage, "Image Out", PortType.Image, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var inputFrame = ctx.GetImage(PortInImage);
        if (inputFrame == null) return ToolResult.Failed("Missing input image.");

        var src = inputFrame.Mat;
        var center = new Point2f(src.Cols / 2.0f, src.Rows / 2.0f);
        var maxR = _pMaxR.GetValue<double>();
        if (maxR <= 0) maxR = Math.Min(src.Cols, src.Rows) / 2.0;

        var outW = _pOutW.GetValue<int>();
        var outH = _pOutH.GetValue<int>();

        var dst = new Mat();
        Cv2.WarpPolar(
            src,
            dst,
            new Size(outW, outH),
            center,
            maxR,
            InterpolationFlags.Linear,
            WarpPolarMode.Linear);

        var outFrame = new ImageFrame(dst, inputFrame.CameraId, inputFrame.FrameId, inputFrame.Format);
        return ToolResult.Ok(new Dictionary<string, object?> { [PortOutImage] = outFrame }, message: $"Unwrapped polar region (R={maxR:F0})");
    }
}
