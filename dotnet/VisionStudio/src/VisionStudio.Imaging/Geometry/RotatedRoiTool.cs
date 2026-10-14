using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Overlays;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Geometry;

/// <summary>
/// Defines an oriented rectangular ROI with arbitrary rotation angle and extracts the straightened image patch.
/// </summary>
[VisionTool("Geometry.RotatedRoi", "Rotated ROI", "Geometry / ROI", Description = "Defines an oriented rectangular ROI with arbitrary rotation and extracts straightened region")]
public sealed class RotatedRoiTool : VisionToolBase
{
    public const string ParamCenterX = "CenterX";
    public const string ParamCenterY = "CenterY";
    public const string ParamWidth = "Width";
    public const string ParamHeight = "Height";
    public const string ParamAngle = "AngleDeg";

    public const string PortInImage = "ImageIn";
    public const string PortStraightenedImage = "StraightenedImage";
    public const string PortMask = "Mask";
    public const string PortMaskedImage = "MaskedImage";

    private readonly ToolParameter _pCx;
    private readonly ToolParameter _pCy;
    private readonly ToolParameter _pW;
    private readonly ToolParameter _pH;
    private readonly ToolParameter _pAngle;

    public RotatedRoiTool() : base("Geometry.RotatedRoi", "Rotated ROI", "Geometry / ROI", "Oriented rectangular ROI extraction")
    {
        _pCx = AddParameter(new ToolParameter(ParamCenterX, "Center X", ParameterType.Integer, 160));
        _pCy = AddParameter(new ToolParameter(ParamCenterY, "Center Y", ParameterType.Integer, 120));
        _pW = AddParameter(new ToolParameter(ParamWidth, "Width", ParameterType.Integer, 160, min: 2));
        _pH = AddParameter(new ToolParameter(ParamHeight, "Height", ParameterType.Integer, 100, min: 2));
        _pAngle = AddParameter(new ToolParameter(ParamAngle, "Angle (deg)", ParameterType.Double, 0.0, min: -360.0, max: 360.0));

        AddInput(new ToolPort(PortInImage, "Image In", PortType.Image, PortDirection.Input));
        AddOutput(new ToolPort(PortStraightenedImage, "Straightened Image", PortType.Image, PortDirection.Output));
        AddOutput(new ToolPort(PortMask, "Mask", PortType.Image, PortDirection.Output));
        AddOutput(new ToolPort(PortMaskedImage, "Masked Image", PortType.Image, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var inputFrame = ctx.GetImage(PortInImage);
        if (inputFrame == null) return ToolResult.Failed("Missing input image.");

        var src = inputFrame.Mat;
        var cx = _pCx.GetValue<int>();
        var cy = _pCy.GetValue<int>();
        var w = Math.Max(2, _pW.GetValue<int>());
        var h = Math.Max(2, _pH.GetValue<int>());
        var angle = _pAngle.GetValue<double>();

        var rotatedRect = new RotatedRect(new Point2f(cx, cy), new Size2f(w, h), (float)angle);
        var pts = rotatedRect.Points();

        // 1. Generate full-size binary mask
        var mask = new Mat(src.Rows, src.Cols, MatType.CV_8UC1, new Scalar(0));
        var cvPoints = pts.Select(p => (Point)p).ToArray();
        Cv2.FillPoly(mask, new[] { cvPoints }, new Scalar(255));

        // 2. Extract straightened patch using affine transformation
        using var rotMat = Cv2.GetRotationMatrix2D(new Point2f(cx, cy), angle, 1.0);
        rotMat.Set(0, 2, rotMat.Get<double>(0, 2) + (w / 2.0 - cx));
        rotMat.Set(1, 2, rotMat.Get<double>(1, 2) + (h / 2.0 - cy));

        var straightened = new Mat();
        Cv2.WarpAffine(src, straightened, rotMat, new Size(w, h), InterpolationFlags.Linear, BorderTypes.Constant, new Scalar(0, 0, 0));

        // 3. Masked original image
        var maskedImage = new Mat();
        src.CopyTo(maskedImage, mask);

        var overlays = new List<IOverlayPrimitive>
        {
            new RotatedRectOverlay(rotatedRect, new Scalar(0, 255, 255), Thickness: 2),
            new CrossOverlay(new Point2f(cx, cy), 8, new Scalar(0, 200, 255), Thickness: 2)
        };

        var outStraightened = new ImageFrame(straightened, inputFrame.CameraId, inputFrame.FrameId, inputFrame.Format);
        var outMask = new ImageFrame(mask, inputFrame.CameraId, inputFrame.FrameId, PixelFormat.Mono8);
        var outMasked = new ImageFrame(maskedImage, inputFrame.CameraId, inputFrame.FrameId, inputFrame.Format);

        var outputs = new Dictionary<string, object?>
        {
            [PortStraightenedImage] = outStraightened,
            [PortMask] = outMask,
            [PortMaskedImage] = outMasked
        };

        return ToolResult.Ok(outputs, overlays, message: $"Rotated ROI Center=({cx},{cy}), Size=({w}x{h}), Angle={angle:F1}°");
    }
}
