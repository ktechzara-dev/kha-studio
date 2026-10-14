using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Overlays;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Geometry;

/// <summary>
/// Rectifies perspective distortion or projects skewed planar regions to an orthographic frame.
/// </summary>
[VisionTool("Geometry.PerspectiveWarp", "Perspective Warp", "Geometry / ROI", Description = "Warps quadrilateral region to rectified rectangle")]
public sealed class PerspectiveWarpTool : VisionToolBase
{
    public const string ParamWidth = "OutputWidth";
    public const string ParamHeight = "OutputHeight";

    public const string PortInImage = "ImageIn";
    public const string PortOutImage = "ImageOut";

    private readonly ToolParameter _pOutW;
    private readonly ToolParameter _pOutH;

    public PerspectiveWarpTool() : base("Geometry.PerspectiveWarp", "Perspective Warp", "Geometry / ROI", "Rectifies planar quadrilateral")
    {
        _pOutW = AddParameter(new ToolParameter(ParamWidth, "Output Width", ParameterType.Integer, 300, min: 10));
        _pOutH = AddParameter(new ToolParameter(ParamHeight, "Output Height", ParameterType.Integer, 200, min: 10));

        AddInput(new ToolPort(PortInImage, "Image In", PortType.Image, PortDirection.Input));
        AddOutput(new ToolPort(PortOutImage, "Image Out", PortType.Image, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var inputFrame = ctx.GetImage(PortInImage);
        if (inputFrame == null) return ToolResult.Failed("Missing input image.");

        var src = inputFrame.Mat;
        var outW = _pOutW.GetValue<int>();
        var outH = _pOutH.GetValue<int>();

        // Default to trapezoid centered in image
        var srcPts = new[]
        {
            new Point2f(src.Cols * 0.15f, src.Rows * 0.15f), // Top-Left
            new Point2f(src.Cols * 0.85f, src.Rows * 0.15f), // Top-Right
            new Point2f(src.Cols * 0.95f, src.Rows * 0.85f), // Bottom-Right
            new Point2f(src.Cols * 0.05f, src.Rows * 0.85f)  // Bottom-Left
        };

        var dstPts = new[]
        {
            new Point2f(0, 0),
            new Point2f(outW, 0),
            new Point2f(outW, outH),
            new Point2f(0, outH)
        };

        using var matrix = Cv2.GetPerspectiveTransform(srcPts, dstPts);
        var dst = new Mat();
        Cv2.WarpPerspective(src, dst, matrix, new Size(outW, outH));

        var outFrame = new ImageFrame(dst, inputFrame.CameraId, inputFrame.FrameId, inputFrame.Format);
        var overlay = new PolygonOverlay(srcPts, new Scalar(0, 255, 255), Thickness: 2);

        return ToolResult.Ok(new Dictionary<string, object?> { [PortOutImage] = outFrame }, new[] { overlay }, message: $"Warped perspective to {outW}x{outH}");
    }
}
