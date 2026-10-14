using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Overlays;
using VisionStudio.Core.Parameters;
using VisionStudio.Core.Ports;
using VisionStudio.Core.Tools;

namespace VisionStudio.Imaging.Geometry;

/// <summary>
/// Defines a dedicated circular or annular (ring) Region of Interest and extracts the masked region.
/// </summary>
[VisionTool("Geometry.CircularRoi", "Circular ROI", "Geometry / ROI", Description = "Defines a circle or annular ring ROI and extracts the masked region")]
public sealed class CircularRoiTool : VisionToolBase
{
    public const string ParamCenterX = "CenterX";
    public const string ParamCenterY = "CenterY";
    public const string ParamOuterRadius = "OuterRadius";
    public const string ParamInnerRadius = "InnerRadius";

    public const string PortInImage = "ImageIn";
    public const string PortMask = "Mask";
    public const string PortMaskedImage = "MaskedImage";
    public const string PortCroppedImage = "CroppedImage";
    public const string PortArea = "Area";

    private readonly ToolParameter _pCx;
    private readonly ToolParameter _pCy;
    private readonly ToolParameter _pOuterR;
    private readonly ToolParameter _pInnerR;

    public CircularRoiTool() : base("Geometry.CircularRoi", "Circular ROI", "Geometry / ROI", "Circle or annular ring ROI extraction")
    {
        _pCx = AddParameter(new ToolParameter(ParamCenterX, "Center X", ParameterType.Integer, 160));
        _pCy = AddParameter(new ToolParameter(ParamCenterY, "Center Y", ParameterType.Integer, 120));
        _pOuterR = AddParameter(new ToolParameter(ParamOuterRadius, "Outer Radius", ParameterType.Integer, 80, min: 1));
        _pInnerR = AddParameter(new ToolParameter(ParamInnerRadius, "Inner Radius (Annulus)", ParameterType.Integer, 0, min: 0));

        AddInput(new ToolPort(PortInImage, "Image In", PortType.Image, PortDirection.Input));
        AddOutput(new ToolPort(PortMask, "Mask", PortType.Image, PortDirection.Output));
        AddOutput(new ToolPort(PortMaskedImage, "Masked Image", PortType.Image, PortDirection.Output));
        AddOutput(new ToolPort(PortCroppedImage, "Cropped Image", PortType.Image, PortDirection.Output));
        AddOutput(new ToolPort(PortArea, "Area (px²)", PortType.Number, PortDirection.Output));
    }

    public override ToolResult Execute(ToolContext ctx, CancellationToken ct)
    {
        var inputFrame = ctx.GetImage(PortInImage);
        if (inputFrame == null) return ToolResult.Failed("Missing input image.");

        var src = inputFrame.Mat;
        var cx = _pCx.GetValue<int>();
        var cy = _pCy.GetValue<int>();
        var rOut = Math.Max(1, _pOuterR.GetValue<int>());
        var rIn = Math.Max(0, _pInnerR.GetValue<int>());

        if (rIn >= rOut)
        {
            rIn = Math.Max(0, rOut - 1);
        }

        var mask = new Mat(src.Rows, src.Cols, MatType.CV_8UC1, new Scalar(0));
        var centerPt = new Point(cx, cy);

        // Fill outer circle
        Cv2.Circle(mask, centerPt, rOut, new Scalar(255), -1);
        if (rIn > 0)
        {
            // Subtract inner circle for annular ring
            Cv2.Circle(mask, centerPt, rIn, new Scalar(0), -1);
        }

        var masked = new Mat();
        src.CopyTo(masked, mask);

        // Bounding rect for cropped image
        var bounding = new Rect(cx - rOut, cy - rOut, rOut * 2, rOut * 2);
        bounding = bounding.Intersect(new Rect(0, 0, src.Cols, src.Rows));

        Mat cropped;
        if (bounding.Width > 0 && bounding.Height > 0)
        {
            cropped = new Mat(masked, bounding).Clone();
        }
        else
        {
            cropped = new Mat(1, 1, src.Type(), new Scalar(0));
        }

        double area = Math.PI * (rOut * rOut - rIn * rIn);

        var overlays = new List<IOverlayPrimitive>
        {
            new CircleOverlay(new Point2f(cx, cy), rOut, new Scalar(0, 255, 0), Thickness: 2),
            new CrossOverlay(new Point2f(cx, cy), 6, new Scalar(0, 200, 255), Thickness: 2)
        };

        if (rIn > 0)
        {
            overlays.Add(new CircleOverlay(new Point2f(cx, cy), rIn, new Scalar(0, 165, 255), Thickness: 2));
        }

        var outMask = new ImageFrame(mask, inputFrame.CameraId, inputFrame.FrameId, PixelFormat.Mono8);
        var outMasked = new ImageFrame(masked, inputFrame.CameraId, inputFrame.FrameId, inputFrame.Format);
        var outCropped = new ImageFrame(cropped, inputFrame.CameraId, inputFrame.FrameId, inputFrame.Format);

        var outputs = new Dictionary<string, object?>
        {
            [PortMask] = outMask,
            [PortMaskedImage] = outMasked,
            [PortCroppedImage] = outCropped,
            [PortArea] = area
        };

        return ToolResult.Ok(outputs, overlays, message: $"Circular ROI Center=({cx},{cy}), R_out={rOut}, R_in={rIn}, Area={area:F0} px²");
    }
}
