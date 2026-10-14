using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Geometry;

/// <summary>
/// Rotates an image around a pivot center by an arbitrary angle.
/// </summary>
[VisionTool("Geometry.Rotate", "Rotate", "Geometry / ROI", Description = "Rotates an image around a pivot point")]
public sealed class RotateTool : VisionToolBase
{
    public const string ParamAngle = "Angle";
    public const string ParamScale = "Scale";
    public const string PortInImage = "ImageIn";
    public const string PortOutImage = "ImageOut";

    private readonly ToolParameter _pAngle;
    private readonly ToolParameter _pScale;

    public RotateTool() : base("Geometry.Rotate", "Rotate", "Geometry / ROI", "Rotates an image")
    {
        _pAngle = AddParameter(new ToolParameter(ParamAngle, "Angle (deg)", ParameterType.Double, 0.0, min: -360.0, max: 360.0, unit: "deg"));
        _pScale = AddParameter(new ToolParameter(ParamScale, "Scale", ParameterType.Double, 1.0, min: 0.1, max: 10.0));

        AddInput(new ToolPort(PortInImage, "Image In", PortType.Image, PortDirection.Input));
        AddOutput(new ToolPort(PortOutImage, "Image Out", PortType.Image, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var inputFrame = ctx.GetImage(PortInImage);
        if (inputFrame == null) return ToolResult.Failed("Missing input image.");

        var src = inputFrame.Mat;
        var angle = _pAngle.GetValue<double>();
        var scale = _pScale.GetValue<double>();

        if (Math.Abs(angle) < 1e-4 && Math.Abs(scale - 1.0) < 1e-4)
        {
            var cloned = new ImageFrame(src.Clone(), inputFrame.CameraId, inputFrame.FrameId, inputFrame.Format);
            return ToolResult.Ok(new Dictionary<string, object?> { [PortOutImage] = cloned }, message: "Rotation 0 deg (passthrough)");
        }

        var center = new Point2f(src.Cols / 2.0f, src.Rows / 2.0f);
        using var rotMatrix = Cv2.GetRotationMatrix2D(center, angle, scale);

        var dst = new Mat();
        Cv2.WarpAffine(src, dst, rotMatrix, new Size(src.Cols, src.Rows), InterpolationFlags.Linear, BorderTypes.Constant, new Scalar(0, 0, 0));

        var outFrame = new ImageFrame(dst, inputFrame.CameraId, inputFrame.FrameId, inputFrame.Format);
        return ToolResult.Ok(new Dictionary<string, object?> { [PortOutImage] = outFrame }, message: $"Rotated {angle:F1}°");
    }
}
